using Cinemachine;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 生成 Boss 战场景。菜单：Tools/Boss战/生成Boss场景
///
/// 做法是把当前的 MainScene（含未保存的改动）另存一份成 BossScene，然后在这个副本里：
/// - 删掉 NPC、其他杂兵、存档点；
/// - 把地块裁剪成 Boss 竞技场那一块，左边补一堵看不见的墙；
/// - 把玩家放到竞技场左侧、Boss 挪到场地中间；
/// - 关掉 Boss 头顶的小血条，加上屏幕下方的大血条；
/// - 加一个 /BossFight 挂 BossFightController，负责入场演出和阵亡慢放。
///
/// 重跑会重建 BossScene（副本里的手工改动会丢），改数值请改场景里的 BossFight。
/// </summary>
public static class BossSceneBuilder
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string BossScenePath = "Assets/Scenes/BossScene.unity";

    private const string BossName = "死亡使者-戴斯";
    private const string WhiteSpritePath = "Assets/Graphics/UI/UI_White.png";
    private const string BossPath = "Enemies/Enemy_DeathBringer";
    private const string BossHeadHealthBarPath = BossPath + "/Entity_Status_UI";

    // 竞技场保留范围（Tilemap 格子坐标）：Boss 右边本来就是墙，左边留到 -31
    private const int KeepMinX = -31;
    private const int KeepMaxX = 9;
    private const int KeepMinY = -8;
    private const int KeepMaxY = 14;

    private const float PlayerSpawnX = -26f;
    private const float PlayerSpawnY = 2f;

    private const float BossX = -4f;
    private const float BossY = 1.25f;

    private const float FocusOrthoSize = 5.5f;
    private const float CameraBlendTime = 0.8f;

    [MenuItem("Tools/Boss战/生成Boss场景")]
    private static void BuildBossScene()
    {
        Scene mainScene = SceneManager.GetSceneByPath(MainScenePath);

        if (!mainScene.IsValid() || !mainScene.isLoaded)
            mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);

        if (!mainScene.IsValid() || !mainScene.isLoaded)
        {
            EditorUtility.DisplayDialog("Boss 战", "打不开 " + MainScenePath + "。", "好");
            return;
        }

        // ---------- 先把旧的 BossScene 收掉并删掉，保证是干净的重新生成 ----------
        // BossScene 生成后往往会被手工调过（升降墙、地块、数值），重新生成会盖掉它们
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BossScenePath) != null &&
            !EditorUtility.DisplayDialog(
                "Boss 战",
                "BossScene 已存在。重新生成会整个覆盖它：\n" +
                "手动摆过的升降墙、改过的地块、调过的数值都会丢。\n\n确定要重新生成吗？",
                "重新生成", "取消"))
            return;

        Scene existing = SceneManager.GetSceneByPath(BossScenePath);

        if (existing.IsValid() && existing.isLoaded)
            EditorSceneManager.CloseScene(existing, true);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BossScenePath) != null)
            AssetDatabase.DeleteAsset(BossScenePath);

        // 把当前（内存里的）MainScene 另存一份，不动原来的文件
        if (!EditorSceneManager.SaveScene(mainScene, BossScenePath, true))
        {
            Debug.LogError("Boss 战：另存 BossScene 失败。");
            return;
        }

        Scene boss = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(boss);

        // ---------- 裁剪内容 ----------
        DestroyExcept(boss, "Enemies", "Enemy_DeathBringer");
        DestroyObject(boss, "NPCs");
        DestroyObject(boss, "Checkpoints");
        DestroyObject(boss, "AreaSound-Wind");

        DestroyObject(boss, "Level/Spikes");
        DestroyObject(boss, "Level/FragileFloor");
        DestroyObject(boss, "Level/MovingWall");
        DestroyObject(boss, "Level/DestructibleWall");

        CropTilemap(Find(boss, "Level/Grid/Ground"));
        CropTilemap(Find(boss, "Level/Grid/Background"));

        // UI 在主场景里是关着的，Boss 场景要它开着才有 HUD 和 Boss 血条
        GameObject canvas = Find(boss, "Canvas");
        if (canvas != null)
            canvas.SetActive(true);

        // ---------- 摆位置 ----------
        GameObject playerGo = Find(boss, "Player");
        GameObject bossGo = Find(boss, BossPath);

        if (playerGo == null || bossGo == null)
        {
            Debug.LogError("Boss 战：场景里找不到 Player 或 Enemy_DeathBringer。");
            return;
        }

        bossGo.transform.position = new Vector3(BossX, BossY, 0f);
        playerGo.transform.position = new Vector3(PlayerSpawnX, PlayerSpawnY, 0f);

        // ---------- Boss 头顶血条关掉 ----------
        GameObject headBar = Find(boss, BossHeadHealthBarPath);
        if (headBar != null)
            headBar.SetActive(false);

        EntityFX bossFx = bossGo.GetComponent<EntityFX>();
        if (bossFx != null)
        {
            SerializedObject fxSerialized = new SerializedObject(bossFx);
            fxSerialized.FindProperty("hideHealthBar").boolValue = true;
            fxSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 演出摄像机 ----------
        GameObject cameraRoot = Find(boss, "Camera");
        GameObject focusPoint = new GameObject("BossFocusPoint");
        focusPoint.transform.SetParent(cameraRoot.transform, false);

        GameObject bossCameraObject = new GameObject("Boss Focus Camera");
        bossCameraObject.transform.SetParent(cameraRoot.transform, false);

        CinemachineVirtualCamera bossCamera = bossCameraObject.AddComponent<CinemachineVirtualCamera>();
        bossCamera.Priority = 20;
        bossCamera.Follow = focusPoint.transform;
        bossCamera.m_Lens.OrthographicSize = FocusOrthoSize;
        bossCamera.AddCinemachineComponent<CinemachineFramingTransposer>();
        bossCameraObject.SetActive(false);

        CinemachineBrain brain = cameraRoot.GetComponentInChildren<CinemachineBrain>(true);
        if (brain != null)
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, CameraBlendTime);

        // ---------- 屏幕中央的名字 + 屏幕下方的血条 ----------
        GameObject inGameUI = Find(boss, "Canvas/InGame_UI");

        if (inGameUI == null)
        {
            Debug.LogError("Boss 战：Canvas/InGame_UI 没找到，Boss 血条没法挂。");
            return;
        }

        TMP_FontAsset font = FindChineseFont();

        CanvasGroup titleGroup;
        BuildTitle(inGameUI.transform, font, out titleGroup);

        EnemyStats bossStats = bossGo.GetComponent<EnemyStats>();
        BossHealthBarUI healthBar = BuildHealthBar(inGameUI.transform, font, bossStats);
        healthBar.gameObject.SetActive(true);

        // ---------- 流程控制 ----------
        GameObject fight = new GameObject("BossFight");
        BossFightController controller = fight.AddComponent<BossFightController>();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("boss").objectReferenceValue = bossGo.GetComponent<Enemy_DeathBringer>();
        serialized.FindProperty("player").objectReferenceValue = playerGo.GetComponent<Player>();
        serialized.FindProperty("bossCamera").objectReferenceValue = bossCamera;
        serialized.FindProperty("bossFocusPoint").objectReferenceValue = focusPoint.transform;
        serialized.FindProperty("healthBar").objectReferenceValue = healthBar;
        serialized.FindProperty("titleGroup").objectReferenceValue = titleGroup;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Boss 的视觉中心相对根节点的偏移（动画子物体带出来的）
        Transform animator = bossGo.transform.Find("Animator");
        if (animator != null)
        {
            SerializedObject fightSerialized = new SerializedObject(controller);
            fightSerialized.FindProperty("focusPointOffset").vector2Value = animator.localPosition;
            fightSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 两侧升降墙的调度 ----------
        // 墙的起点 / 移动距离都是 MovingWall 上配好的，这里只接引用和时机
        BossArenaWalls arenaWalls = fight.AddComponent<BossArenaWalls>();
        SerializedObject wallSerialized = new SerializedObject(arenaWalls);
        wallSerialized.FindProperty("fight").objectReferenceValue = controller;

        SerializedProperty wallArray = wallSerialized.FindProperty("walls");
        var foundWalls = new System.Collections.Generic.List<Object>();

        foreach (string wallName in new[] { "Level/MovingWallLeft", "Level/MovingWallRight" })
        {
            GameObject wallObject = Find(boss, wallName);

            if (wallObject != null)
                foundWalls.Add(wallObject.GetComponent<MovingWall>());
        }

        wallArray.arraySize = foundWalls.Count;

        for (int i = 0; i < foundWalls.Count; i++)
            wallArray.GetArrayElementAtIndex(i).objectReferenceValue = foundWalls[i];

        wallSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (foundWalls.Count == 0)
            Debug.LogWarning("Boss 战：场景里没有 Level/MovingWallLeft 和 MovingWallRight，升降墙不会被调度。" +
                             "这两面墙是手摆的，重新生成场景后需要手动接一下。");

        EditorSceneManager.MarkSceneDirty(boss);
        EditorSceneManager.SaveScene(boss);

        Debug.Log($"Boss 战：{BossScenePath} 已生成并保存。\n" +
                  "试玩：让 BossScene 单独打开（关掉 MainScene）再按 Play，或者在 Build Settings 里把 Play Mode Start Scene 设成它。");
    }

    #region 场景搭建

    private static void BuildTitle(Transform _parent, TMP_FontAsset _font, out CanvasGroup _group)
    {
        GameObject root = NewUI("BossTitle", _parent);
        Stretch(RT(root));

        _group = root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        TextMeshProUGUI text = NewText("Name", root.transform, BossName, 92f,
            TextAlignmentOptions.Center, new Color(0.95f, 0.91f, 0.82f, 1f), _font);

        SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1700f, 180f), new Vector2(0f, 40f));
    }

    private static BossHealthBarUI BuildHealthBar(Transform _parent, TMP_FontAsset _font, EnemyStats _bossStats)
    {
        GameObject root = NewUI("BossHealthBar", _parent);
        Stretch(RT(root));

        CanvasGroup group = root.AddComponent<CanvasGroup>();

        // 血条用纯白底图：Image 用 Filled 类型时必须有 sprite，否则 fillAmount 不生效
        Sprite white = GetOrCreateWhiteSprite();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        BossHealthBarUI ui = root.AddComponent<BossHealthBarUI>();

        // 血条上方的名字
        TextMeshProUGUI nameText = NewText("Name", root.transform, BossName, 46f,
            TextAlignmentOptions.Center, new Color(0.95f, 0.91f, 0.82f, 1f), _font);

        SetRect(nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(1400f, 60f), new Vector2(0f, 196f));

        // 会从中间往两边展开的那根
        GameObject bar = NewUI("Bar", root.transform);
        RectTransform barRect = RT(bar);
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(900f, 26f);
        barRect.anchoredPosition = new Vector2(0f, 150f);

        Image border = NewImage("Border", bar.transform, new Color(0.84f, 0.71f, 0.4f, 0.85f));
        border.sprite = white;
        Stretch(RT(border.gameObject), -4f, -4f, -4f, -4f);

        Image background = NewImage("Background", bar.transform, new Color(0.05f, 0.05f, 0.07f, 0.92f));
        background.sprite = white;
        Stretch(RT(background.gameObject));

        // 掉血时慢慢追上来的残影
        Image delayFill = NewImage("DelayFill", bar.transform, new Color(1f, 0.85f, 0.5f, 0.75f));
        delayFill.sprite = white;
        Stretch(RT(delayFill.gameObject), 2f, 2f, 2f, 2f);
        delayFill.type = Image.Type.Filled;
        delayFill.fillMethod = Image.FillMethod.Horizontal;
        delayFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        delayFill.fillAmount = 1f;

        Image fill = NewImage("Fill", bar.transform, new Color(0.72f, 0.13f, 0.15f, 1f));
        fill.sprite = white;
        Stretch(RT(fill.gameObject), 2f, 2f, 2f, 2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("bossStats").objectReferenceValue = _bossStats;
        serialized.FindProperty("barRoot").objectReferenceValue = barRect;
        serialized.FindProperty("fillImage").objectReferenceValue = fill;
        serialized.FindProperty("delayFillImage").objectReferenceValue = delayFill;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("bossName").stringValue = BossName;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return ui;
    }

    /// <summary>把地块裁到竞技场范围，范围外的直接清掉。</summary>
    private static void CropTilemap(GameObject _mapObject)
    {
        if (_mapObject == null)
            return;

        Tilemap map = _mapObject.GetComponent<Tilemap>();

        if (map == null)
            return;

        BoundsInt bounds = map.cellBounds;
        TileBase[] tiles = map.GetTilesBlock(bounds);

        map.ClearAllTiles();

        for (int y = 0; y < bounds.size.y; y++)
        {
            for (int x = 0; x < bounds.size.x; x++)
            {
                TileBase tile = tiles[x + y * bounds.size.x];

                if (tile == null)
                    continue;

                int cellX = bounds.xMin + x;
                int cellY = bounds.yMin + y;

                if (cellX < KeepMinX || cellX > KeepMaxX || cellY < KeepMinY || cellY > KeepMaxY)
                    continue;

                map.SetTile(new Vector3Int(cellX, cellY, 0), tile);
            }
        }

        map.CompressBounds();
    }

    #endregion

    #region 场景小工具

    private static GameObject Find(Scene _scene, string _path)
    {
        if (!_scene.IsValid())
            return null;

        string[] parts = _path.Split('/');

        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            if (root.name != parts[0])
                continue;

            Transform current = root.transform;

            for (int i = 1; i < parts.Length && current != null; i++)
                current = current.Find(parts[i]);

            if (current != null)
                return current.gameObject;
        }

        return null;
    }

    private static void DestroyObject(Scene _scene, string _path)
    {
        GameObject go = Find(_scene, _path);

        if (go != null)
            Object.DestroyImmediate(go);
    }

    private static void DestroyExcept(Scene _scene, string _path, params string[] _keep)
    {
        GameObject parent = Find(_scene, _path);

        if (parent == null)
            return;

        var doomed = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in parent.transform)
        {
            bool keep = false;

            foreach (string name in _keep)
            {
                if (child.name == name)
                    keep = true;
            }

            if (!keep)
                doomed.Add(child.gameObject);
        }

        foreach (GameObject go in doomed)
            Object.DestroyImmediate(go);
    }

    #endregion

    #region UI 小工具

    private static GameObject NewUI(string _name, Transform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.transform.SetParent(_parent, false);

        return go;
    }

    private static RectTransform RT(GameObject _go) => _go.GetComponent<RectTransform>();

    private static void Stretch(RectTransform _rt, float _left = 0f, float _right = 0f, float _top = 0f, float _bottom = 0f)
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.offsetMin = new Vector2(_left, _bottom);
        _rt.offsetMax = new Vector2(-_right, -_top);
    }

    private static void SetRect(RectTransform _rt, Vector2 _anchor, Vector2 _size, Vector2 _offset)
    {
        _rt.anchorMin = _anchor;
        _rt.anchorMax = _anchor;
        _rt.pivot = _anchor;
        _rt.sizeDelta = _size;
        _rt.anchoredPosition = _offset;
    }

    private static TextMeshProUGUI NewText(string _name, Transform _parent, string _text, float _size,
        TextAlignmentOptions _align, Color _color, TMP_FontAsset _font)
    {
        GameObject go = NewUI(_name, _parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        text.text = _text;
        text.fontSize = _size;
        text.alignment = _align;
        text.color = _color;
        text.raycastTarget = false;

        if (_font != null)
            text.font = _font;

        return text;
    }

    private static Image NewImage(string _name, Transform _parent, Color _color)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();

        image.color = _color;
        image.raycastTarget = false;

        return image;
    }

    /// <summary>血条用的纯白贴图，没有就现生成一张 PNG 资源。</summary>
    private static Sprite GetOrCreateWhiteSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);

        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[64];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 8f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
    }

    private static TMP_FontAsset FindChineseFont()
    {
        TMP_FontAsset dynamicFallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

            if (candidate == null)
                continue;

            if (candidate.HasCharacter('死'))
                return candidate;

            if (dynamicFallback == null && candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                dynamicFallback = candidate;
        }

        if (dynamicFallback != null)
            return dynamicFallback;

        Debug.LogWarning("Boss 战：没找到带中文字形的 TMP 字体，Boss 名字可能显示成方块。");
        return TMP_Settings.defaultFontAsset;
    }

    #endregion
}
