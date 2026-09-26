using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 怪物图鉴的一键填充工具：把 Assets/Prefabs/Enemies 下的敌人预制体扫一遍，
/// 自动加进场景里那个 MonsterBestiary 的怪物列表（已经有的不会重复加）。
///
/// 图片和掉落物留空，运行时会自动从敌人预制体上取；名字会自动去掉 Enemy_ 前缀，
/// 描述和「没击杀过显示 ？？？」需要自己在 Inspector 里补。
/// </summary>
public static class MonsterBestiaryAutoFill
{
    private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";

    [MenuItem("Tools/怪物图鉴/从敌人预制体自动填充")]
    private static void FillFromEnemyPrefabs()
    {
        MonsterBestiary bestiary = Object.FindObjectOfType<MonsterBestiary>();

        if (bestiary == null)
        {
            EditorUtility.DisplayDialog("怪物图鉴",
                "当前打开的场景里没有 MonsterBestiary 组件。\n\n" +
                "先在挂 UI 的物体上加一个 MonsterBestiaryUI（它会自动带出 MonsterBestiary），再执行这个菜单。", "好");
            return;
        }

        SerializedObject serialized = new SerializedObject(bestiary);
        serialized.Update();

        SerializedProperty list = serialized.FindProperty("monsters");

        if (list == null)
            return;

        List<string> existing = new List<string>();

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            GameObject prefab = element.FindPropertyRelative("enemyPrefab").objectReferenceValue as GameObject;
            string id = prefab != null ? prefab.name : element.FindPropertyRelative("monsterId").stringValue;

            if (!string.IsNullOrEmpty(id))
                existing.Add(id);
        }

        Undo.RecordObject(bestiary, "自动填充怪物图鉴");

        int added = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null || prefab.GetComponentInChildren<Enemy>(true) == null)
                continue;

            if (existing.Contains(prefab.name))
                continue;

            list.InsertArrayElementAtIndex(list.arraySize);

            SerializedProperty element = list.GetArrayElementAtIndex(list.arraySize - 1);
            element.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("monsterId").stringValue = "";
            element.FindPropertyRelative("displayName").stringValue = prefab.name.Replace("Enemy_", "");
            element.FindPropertyRelative("description").stringValue = "";
            element.FindPropertyRelative("icon").objectReferenceValue = null;
            element.FindPropertyRelative("drops").arraySize = 0;
            element.FindPropertyRelative("hideUntilFirstKill").boolValue = false;

            existing.Add(prefab.name);
            added++;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(bestiary);

        Debug.Log($"怪物图鉴：新增 {added} 只怪物，列表现在共 {list.arraySize} 条。描述文字请在 Inspector 里自己填。");
    }
}
