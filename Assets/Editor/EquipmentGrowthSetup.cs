using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 装备成长曲线的配置生成工具。
/// 菜单「Tools/装备/生成成长曲线配置」会生成/补全 Assets/Data/EquipmentGrowthConfig.asset，
/// 并自动把 EquipmentLevelManager 挂到场景里的常驻物体上。
///
/// 强化消耗是「每个部位一套」：每种材料可以有不同的数量与每级成长，同类型的装备完全相同。
/// </summary>
public static class EquipmentGrowthSetup
{
    private const string FolderPath = "Assets/Data";
    private const string ConfigPath = FolderPath + "/EquipmentGrowthConfig.asset";

    [MenuItem("Tools/装备/生成成长曲线配置")]
    private static void CreateConfig()
    {
        EquipmentGrowthConfig config = AssetDatabase.LoadAssetAtPath<EquipmentGrowthConfig>(ConfigPath);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EquipmentGrowthConfig>();

            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets", "Data");

            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"装备成长：已生成配置 {ConfigPath}（等级上限 20、10/20 级特殊效果，数值都可以在这里改）。");
        }
        else
        {
            Debug.Log($"装备成长：{ConfigPath} 已经存在，只补全缺失的强化材料配置。");
        }

        EnsureUpgradeRequirements(config);

        EquipmentLevelManager manager = Object.FindObjectOfType<EquipmentLevelManager>();

        if (manager == null)
        {
            // 场景里还没有：自动找个常驻物体（挂 Inventory / GameManager 的那个）加上
            Inventory inventory = Object.FindObjectOfType<Inventory>();
            GameManager gameManager = Object.FindObjectOfType<GameManager>();

            GameObject host = inventory != null ? inventory.gameObject
                : (gameManager != null ? gameManager.gameObject : null);

            if (host == null)
            {
                Debug.Log("装备成长：场景里没找到合适的常驻物体，自己建个空物体挂上 EquipmentLevelManager，再把配置拖到它的 Config 字段。");
                return;
            }

            manager = Undo.AddComponent<EquipmentLevelManager>(host);
            Debug.Log($"装备成长：已在「{host.name}」上自动加上 EquipmentLevelManager。");
        }

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty configProperty = serialized.FindProperty("config");

        if (configProperty != null && configProperty.objectReferenceValue == null)
        {
            configProperty.objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            Selection.activeGameObject = manager.gameObject;

            Debug.Log("装备成长：已经把配置挂到场景里的 EquipmentLevelManager 上了。");
        }
    }

    #region 强化消耗

    /// <summary>给四个部位补上默认的多材料强化消耗（已经填过的部位不动）。</summary>
    private static void EnsureUpgradeRequirements(EquipmentGrowthConfig _config)
    {
        SerializedObject serialized = new SerializedObject(_config);
        SerializedProperty list = serialized.FindProperty("upgradeRequirements");

        if (list == null)
            return;

        if (list.arraySize == 0)
        {
            list.arraySize = 4;

            SetType(list.GetArrayElementAtIndex(0), EquipmentType.Weapon);
            SetType(list.GetArrayElementAtIndex(1), EquipmentType.Armor);
            SetType(list.GetArrayElementAtIndex(2), EquipmentType.Amulet);
            SetType(list.GetArrayElementAtIndex(3), EquipmentType.Flask);

            serialized.ApplyModifiedProperties();
            serialized.Update();
        }

        int filled = 0;

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            EquipmentType type = (EquipmentType)element.FindPropertyRelative("equipmentType").enumValueIndex;

            if (!FillMaterials(element, type))
                continue;

            // 每填一个部位就写回一次：嵌套数组在同一个 SerializedObject 里连着改会错位
            serialized.ApplyModifiedProperties();
            serialized.Update();

            filled++;
        }

        if (filled == 0)
            return;

        EditorUtility.SetDirty(_config);

        Debug.Log($"装备成长：给 {filled} 个部位补上了默认强化材料（每种材料数量不同：主材料随等级增长、副材料固定数量），可以在配置资产里改。");
    }

    private static void SetType(SerializedProperty _element, EquipmentType _type)
    {
        _element.FindPropertyRelative("equipmentType").enumValueIndex = (int)_type;
        _element.FindPropertyRelative("materials").arraySize = 0;
    }

    /// <summary>该部位还没有材料配置就填一套默认的；返回是否填了。</summary>
    private static bool FillMaterials(SerializedProperty _element, EquipmentType _type)
    {
        SerializedProperty materials = _element.FindPropertyRelative("materials");

        // 已经配好两种或以上就当用户自己调过，不再插手；只有 0 / 1 种时才补全
        if (materials == null || materials.arraySize >= 2)
            return false;

        List<MaterialPlan> plan = new List<MaterialPlan>();

        switch (_type)
        {
            case EquipmentType.Weapon:
                SetCurrency(_element, 100, 50);
                plan.Add(new MaterialPlan(FindMaterial("Iron", "铁"), 0, 2));       // 主材料：每级 2 个
                plan.Add(new MaterialPlan(FindMaterial("Wood", "木"), 3, 0));       // 副材料：固定 3 个
                break;

            case EquipmentType.Armor:
                SetCurrency(_element, 100, 50);
                plan.Add(new MaterialPlan(FindMaterial("Animal skin"), 0, 2));
                plan.Add(new MaterialPlan(FindMaterial("Cotton", "Yarn"), 3, 0));
                break;

            case EquipmentType.Amulet:
                SetCurrency(_element, 150, 60);
                plan.Add(new MaterialPlan(FindMaterial("Diamond", "钻石"), 0, 1));
                plan.Add(new MaterialPlan(FindMaterial("Gemstone", "Marble", "宝石"), 2, 0));
                break;

            case EquipmentType.Flask:
                SetCurrency(_element, 80, 40);
                plan.Add(new MaterialPlan(FindMaterial("Mushroom", "蘑菇"), 0, 1));
                plan.Add(new MaterialPlan(FindMaterial("Fern", "Flower", "Greenery"), 2, 0));
                break;
        }

        // 找不到的材料跳过，同一种材料只留一次
        List<MaterialPlan> final = new List<MaterialPlan>();

        foreach (MaterialPlan item in plan)
        {
            if (item.material == null)
                continue;

            bool duplicate = false;

            foreach (MaterialPlan added in final)
            {
                if (added.material == item.material)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                final.Add(item);
        }

        if (final.Count == 0)
            return false;

        materials.arraySize = final.Count;

        for (int i = 0; i < final.Count; i++)
        {
            SerializedProperty entry = materials.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("material").objectReferenceValue = final[i].material;
            entry.FindPropertyRelative("baseAmount").intValue = final[i].baseAmount;
            entry.FindPropertyRelative("amountPerLevel").intValue = final[i].perLevel;
        }

        return true;
    }

    private static void SetCurrency(SerializedProperty _element, int _base, int _perLevel)
    {
        _element.FindPropertyRelative("currencyBase").intValue = _base;
        _element.FindPropertyRelative("currencyPerLevel").intValue = _perLevel;
    }

    private static ItemData FindMaterial(params string[] _keywords)
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/Data/Items/Materials" }))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));

            if (item == null || string.IsNullOrEmpty(item.itemName))
                continue;

            string name = item.itemName.ToLowerInvariant();

            foreach (string keyword in _keywords)
            {
                if (name.Contains(keyword.ToLowerInvariant()))
                    return item;
            }
        }

        // 名字搜索没命中时，按「关键字.asset」直接找文件（例如 Animal skin → Animal_skin.asset）
        foreach (string keyword in _keywords)
        {
            ItemData direct = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Materials/" + keyword.Replace(" ", "_") + ".asset");

            if (direct != null)
                return direct;
        }

        return null;
    }

    /// <summary>要填进配置里的一种材料。</summary>
    private class MaterialPlan
    {
        public readonly ItemData material;
        public readonly int baseAmount;
        public readonly int perLevel;

        public MaterialPlan(ItemData _material, int _baseAmount, int _perLevel)
        {
            material = _material;
            baseAmount = _baseAmount;
            perLevel = _perLevel;
        }
    }

    #endregion
}
