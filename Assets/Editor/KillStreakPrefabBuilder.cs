using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 连续杀敌加速的预制体生成器。
/// 菜单：Tools/击杀连击/生成预制体
///
/// 生成后：
/// - Assets/Graphics/UI/Buff_KillStreak.png                  图标贴图（换美术就换这个）
/// - Assets/Graphics/UI/Buff_KillStreak_Bar.png             时间条的纯白底图
/// - Assets/Resources/KillStreak/KillStreakBuffUI.prefab    头顶图标（大小 / 间距 / 颜色 / 贴图都在这）
/// - Assets/Resources/KillStreak/KillStreakBuffManager.prefab  数值（3/6/9 只、33/66/100%、10 秒窗口与持续）
///
/// 放在 Resources 下是为了运行时不用往场景里摆就能直接用上：改完预制体直接 Play 就能看到效果。
/// 想在场景里调也行，用「Tools/击杀连击/把预制体放进当前场景」。
/// </summary>
public static class KillStreakPrefabBuilder
{
    private const string UiArtFolder = "Assets/Graphics/UI";
    private const string IconPngPath = UiArtFolder + "/Buff_KillStreak.png";
    private const string BarPngPath = UiArtFolder + "/Buff_KillStreak_Bar.png";

    private const string ResourcesFolder = "Assets/Resources";
    private const string PrefabFolder = ResourcesFolder + "/KillStreak";
    private const string UiPrefabPath = PrefabFolder + "/KillStreakBuffUI.prefab";
    private const string ManagerPrefabPath = PrefabFolder + "/KillStreakBuffManager.prefab";

    private const string BuildMenu = "Tools/击杀连击/生成预制体";
    private const string PlaceMenu = "Tools/击杀连击/把预制体放进当前场景";
    private const string SyncMenu = "Tools/击杀连击/把外观同步到图标子物体";

    #region 菜单

    [MenuItem(BuildMenu)]
    private static void BuildPrefabs()
    {
        EnsureFolders();

        Sprite iconSprite = CreateSpriteAsset(IconPngPath, KillStreakIconArt.CreateIconTexture(), KillStreakIconArt.IconSize);
        Sprite barSprite = CreateSpriteAsset(BarPngPath, KillStreakIconArt.CreateBarTexture(), KillStreakIconArt.BarSize);

        GameObject uiRoot = BuildBuffUI(iconSprite, barSprite);
        GameObject uiAsset = PrefabUtility.SaveAsPrefabAsset(uiRoot, UiPrefabPath);
        Object.DestroyImmediate(uiRoot);

        GameObject managerRoot = BuildManager(uiAsset.GetComponent<KillStreakBuffUI>());
        PrefabUtility.SaveAsPrefabAsset(managerRoot, ManagerPrefabPath);
        Object.DestroyImmediate(managerRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"击杀连击：预制体已生成。\n{UiPrefabPath}\n{ManagerPrefabPath}\n" +
                  "改数值改管理器的预制体，改外观改 UI 的预制体，运行时用的就是它们。");
    }

    [MenuItem(PlaceMenu)]
    private static void PlaceIntoScene()
    {
        GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
        GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);

        if (uiPrefab == null || managerPrefab == null)
        {
            EditorUtility.DisplayDialog("击杀连击", "还没生成预制体。\n先执行「Tools/击杀连击/生成预制体」。", "好");
            return;
        }

        GameObject ui = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab);
        Undo.RegisterCreatedObjectUndo(ui, "放置连击 Buff 图标");

        GameObject managerObject = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
        Undo.RegisterCreatedObjectUndo(managerObject, "放置连击管理器");

        KillStreakBuffManager manager = managerObject.GetComponent<KillStreakBuffManager>();

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("buffUI").objectReferenceValue = ui.GetComponent<KillStreakBuffUI>();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = managerObject;

        Debug.Log("击杀连击：预制体已放进场景。改场景里的实例只影响这个场景，改 Resources 下的预制体全局生效。记得保存场景。");
    }

    /// <summary>
    /// 把 UI 预制体上填的 iconSprite / 颜色 / 大小写进 Icon_0..N 和时间条。
    /// 图是画在子物体上的，只改组件字段不会自动改子物体，改完外观嫌场景里没变就跑这个。
    /// </summary>
    [MenuItem(SyncMenu)]
    private static void SyncAppearance()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("击杀连击", "还没生成预制体。\n先执行「Tools/击杀连击/生成预制体」。", "好");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(UiPrefabPath);
        bool saved = false;

        try
        {
            KillStreakBuffUI ui = root.GetComponent<KillStreakBuffUI>();

            if (ui != null)
            {
                ui.BuildLayout();          // 缺图标就补，已有的按名字复用
                ui.ApplyAppearanceNow();   // 把外观真正写进各个 Image
                saved = true;
            }
        }
        finally
        {
            if (saved)
                PrefabUtility.SaveAsPrefabAsset(root, UiPrefabPath);

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("击杀连击：外观已同步到图标子物体。场景里的实例只要没单独改过，会跟着预制体一起更新。");
    }

    #endregion

    #region 搭建

    private static GameObject BuildBuffUI(Sprite _iconSprite, Sprite _barSprite)
    {
        GameObject root = new GameObject("KillStreakBuffUI",
            typeof(RectTransform), typeof(Canvas), typeof(KillStreakBuffUI));

        KillStreakBuffUI ui = root.GetComponent<KillStreakBuffUI>();
        ui.ApplyDefaultCanvasSettings();

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1.4f, 0.7f);

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("iconSprite").objectReferenceValue = _iconSprite;
        serialized.FindProperty("barSprite").objectReferenceValue = _barSprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 图标和时间条交给脚本按当前的尺寸设置摆好，和运行时兜底用的是同一段逻辑
        ui.BuildLayout();

        return root;
    }

    private static GameObject BuildManager(KillStreakBuffUI _uiPrefab)
    {
        GameObject root = new GameObject("KillStreakBuffManager");
        KillStreakBuffManager manager = root.AddComponent<KillStreakBuffManager>();

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("buffUIPrefab").objectReferenceValue = _uiPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    #endregion

    #region 小工具

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Graphics"))
            AssetDatabase.CreateFolder("Assets", "Graphics");

        if (!AssetDatabase.IsValidFolder(UiArtFolder))
            AssetDatabase.CreateFolder("Assets/Graphics", "UI");

        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder(ResourcesFolder, "KillStreak");
    }

    /// <summary>把现画出来的贴图写成 PNG 资源，并按 2D/UI 的 Sprite 导入回来。</summary>
    private static Sprite CreateSpriteAsset(string _path, Texture2D _texture, int _pixelsPerUnit)
    {
        byte[] png = _texture.EncodeToPNG();
        Object.DestroyImmediate(_texture);

        File.WriteAllBytes(_path, png);

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(_path) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = _pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(_path);
    }

    #endregion
}
