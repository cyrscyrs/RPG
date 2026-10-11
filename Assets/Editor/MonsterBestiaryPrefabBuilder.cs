using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成怪物图鉴的预制体（面板 + 怪物格子 + 掉落行）。
/// 菜单：Tools/怪物图鉴/生成图鉴预制体
///
/// 生成后：
/// - Assets/Prefabs/UI/MonsterBestiary.prefab         整个图鉴面板（拖到 Canvas 里用）
/// - Assets/Prefabs/UI/MonsterBestiarySlot.prefab     一格怪物（改样式改这个）
/// - Assets/Prefabs/UI/MonsterBestiaryDropRow.prefab  掉落列表的一行
/// 界面样式想改就改这三个预制体，脚本只会往格子里填数据。
/// </summary>
public static class MonsterBestiaryPrefabBuilder
{
    private const string FolderPath = "Assets/Prefabs/UI";
    private const string PanelPath = FolderPath + "/MonsterBestiary.prefab";
    private const string SlotPath = FolderPath + "/MonsterBestiarySlot.prefab";
    private const string RowPath = FolderPath + "/MonsterBestiaryDropRow.prefab";

    private const string PlaceMenu = "Tools/怪物图鉴/把图鉴放进当前场景";

    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    private static readonly Color SlotColor = new Color(0.13f, 0.15f, 0.2f, 1f);
    private static readonly Color PopupColor = new Color(0.09f, 0.1f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.4f, 1f);
    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.12f);

    #region 菜单

    [MenuItem("Tools/怪物图鉴/生成图鉴预制体")]
    private static void BuildPrefabs()
    {
        EnsureFolders();

        TMP_FontAsset font = FindChineseFont();

        GameObject slotRoot = BuildSlot(font);
        GameObject slotAsset = PrefabUtility.SaveAsPrefabAsset(slotRoot, SlotPath);
        Object.DestroyImmediate(slotRoot);

        GameObject rowRoot = BuildDropRow(font);
        GameObject rowAsset = PrefabUtility.SaveAsPrefabAsset(rowRoot, RowPath);
        Object.DestroyImmediate(rowRoot);

        GameObject panelRoot = BuildPanel(font, slotAsset.GetComponent<MonsterBestiarySlot>(), rowAsset.GetComponent<MonsterBestiaryDropRow>());
        PrefabUtility.SaveAsPrefabAsset(panelRoot, PanelPath);
        Object.DestroyImmediate(panelRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"怪物图鉴：预制体已生成。\n{PanelPath}\n{SlotPath}\n{RowPath}\n" +
                  "把 MonsterBestiary 拖到 Canvas 里当作图鉴面板即可（挂在 UI 那个物体下面就会跟 C/B/K/O 菜单联动）。");
    }

    [MenuItem(PlaceMenu)]
    private static void PlaceIntoScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("怪物图鉴", "还没生成预制体。\n先执行「Tools/怪物图鉴/生成图鉴预制体」。", "好");
            return;
        }

        UI uiManager = Object.FindObjectOfType<UI>();
        Transform parent = uiManager != null ? uiManager.transform : null;

        if (parent == null)
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }

        if (parent == null)
        {
            EditorUtility.DisplayDialog("怪物图鉴", "当前场景里没有 Canvas，先把 Canvas 建出来再执行。", "好");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "放置怪物图鉴");

        if (uiManager != null)
            uiManager.SetBestiaryPanel(instance);

        Selection.activeGameObject = instance;

        Debug.Log($"怪物图鉴：已放进场景，父物体是「{parent.name}」。记得保存场景。");
    }

    /// <summary>
    /// 把已经生成过的面板预制体里的掉落列表原地升级成可滑动列表：
    /// 在 Box 和原来的 List 之间插一层 Scroll + Viewport（裁切），List 直接当内容用。
    /// 只动这一块的结构，其余样式一律不碰；重复执行也安全。
    /// </summary>
    [MenuItem("Tools/怪物图鉴/把掉落列表升级成滑动列表")]
    private static void UpgradeDropsListToScrollView()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);

        if (asset == null)
        {
            EditorUtility.DisplayDialog("怪物图鉴", "还没生成预制体。\n先执行「Tools/怪物图鉴/生成图鉴预制体」。", "好");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);

        Transform list = FindChild(root.transform, "Window/DropsPopup/Box/List");

        if (list == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            Debug.LogError($"怪物图鉴：在 {PanelPath} 里找不到 Window/DropsPopup/Box/List，没有修改。");
            return;
        }

        Transform existingScroll = list.parent != null ? list.parent.parent : null;

        if (list.parent != null && list.parent.GetComponent<RectMask2D>() != null
            && existingScroll != null && existingScroll.GetComponent<ScrollRect>() != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("怪物图鉴：掉落列表已经是滑动列表，不用再升级。");
            return;
        }

        RectTransform listRect = (RectTransform)list;
        Transform box = list.parent;

        // Scroll 直接接管原来 List 的占位
        GameObject scrollGo = NewUI("Scroll", box);
        RectTransform scrollRectT = RT(scrollGo);
        scrollRectT.anchorMin = listRect.anchorMin;
        scrollRectT.anchorMax = listRect.anchorMax;
        scrollRectT.pivot = listRect.pivot;
        scrollRectT.offsetMin = listRect.offsetMin;
        scrollRectT.offsetMax = listRect.offsetMax;

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        Image catcher = scrollGo.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        GameObject viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);

        // 原来的 List 挂到 Viewport 下当内容，改成「顶部对齐、向下撑高」
        list.SetParent(viewport.transform, false);
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;

        if (list.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        if (list.GetComponent<ContentSizeFitter>() == null)
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = listRect;

        MonsterBestiaryUI ui = root.GetComponent<MonsterBestiaryUI>();

        if (ui != null)
        {
            SerializedObject serialized = new SerializedObject(ui);
            SerializedProperty scrollProperty = serialized.FindProperty("dropsScroll");

            if (scrollProperty != null)
            {
                scrollProperty.objectReferenceValue = scroll;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"怪物图鉴：掉落列表已升级成可滑动列表（{PanelPath}）。");
    }

    private static Transform FindChild(Transform _root, string _path)
    {
        Transform current = _root;

        foreach (string part in _path.Split('/'))
            current = current != null ? current.Find(part) : null;

        return current;
    }
    #endregion

    #region 三个预制体的搭建

    private static GameObject BuildSlot(TMP_FontAsset _font)
    {
        GameObject root = NewUI("MonsterBestiarySlot", null);
        RT(root).sizeDelta = new Vector2(215f, 300f);

        root.AddComponent<Image>().color = SlotColor;

        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 图片（点它放大看描述）
        Button iconButton = NewButton("Icon", root.transform, null, 0f, _font);
        Image iconImage = iconButton.GetComponent<Image>();
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;

        LayoutElement iconLayout = iconButton.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredHeight = 150f;

        TextMeshProUGUI noIconHint = NewText("NoIconHint", iconButton.transform, "暂无图片", 22f, TextAlignmentOptions.Center, new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f), _font);
        Stretch(noIconHint.rectTransform);

        TextMeshProUGUI nameText = NewText("Name", root.transform, "怪物名", 26f, TextAlignmentOptions.Center, AccentColor, _font);
        nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

        TextMeshProUGUI killText = NewText("Kills", root.transform, "累计击杀：0", 22f, TextAlignmentOptions.Center, TextColor, _font);
        killText.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        Button dropsButton = NewButton("DropsButton", root.transform, "掉落物", 24f, _font);
        dropsButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        MonsterBestiarySlot slot = root.AddComponent<MonsterBestiarySlot>();

        SerializedObject serialized = new SerializedObject(slot);
        serialized.FindProperty("iconButton").objectReferenceValue = iconButton;
        serialized.FindProperty("iconImage").objectReferenceValue = iconImage;
        serialized.FindProperty("noIconHint").objectReferenceValue = noIconHint.gameObject;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("killText").objectReferenceValue = killText;
        serialized.FindProperty("dropsButton").objectReferenceValue = dropsButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject BuildDropRow(TMP_FontAsset _font)
    {
        GameObject root = NewUI("MonsterBestiaryDropRow", null);
        RT(root).sizeDelta = new Vector2(520f, 74f);

        root.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        root.AddComponent<LayoutElement>().preferredHeight = 74f;

        Image iconImage = NewImage("Icon", root.transform, Color.white);
        SetRect(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(12f, 0f));

        TextMeshProUGUI nameText = NewText("Name", root.transform, "物品名", 26f, TextAlignmentOptions.Left, TextColor, _font);
        SetRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(340f, 54f), new Vector2(82f, 0f));

        TextMeshProUGUI chanceText = NewText("Chance", root.transform, "50%", 24f, TextAlignmentOptions.Right, AccentColor, _font);
        SetRect(chanceText.rectTransform, new Vector2(1f, 0.5f), new Vector2(140f, 54f), new Vector2(-16f, 0f));

        MonsterBestiaryDropRow row = root.AddComponent<MonsterBestiaryDropRow>();

        SerializedObject serialized = new SerializedObject(row);
        serialized.FindProperty("iconImage").objectReferenceValue = iconImage;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("chanceText").objectReferenceValue = chanceText;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject BuildPanel(TMP_FontAsset _font, MonsterBestiarySlot _slotPrefab, MonsterBestiaryDropRow _rowPrefab)
    {
        GameObject root = NewUI("MonsterBestiary", null);
        Stretch(RT(root));   // 铺满父物体，方便直接放进 Canvas

        MonsterBestiaryUI ui = root.AddComponent<MonsterBestiaryUI>();
        MonsterBestiary bestiary = root.AddComponent<MonsterBestiary>();

        // ---------- 视觉根节点 ----------
        GameObject window = NewUI("Window", root.transform);
        Stretch(RT(window));
        window.AddComponent<Image>().color = PanelColor;

        TextMeshProUGUI title = NewText("Title", window.transform, "怪物图鉴", 46f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 70f), new Vector2(0f, -50f));

        TextMeshProUGUI hint = NewText("Hint", window.transform, "点击图片放大看描述 · 点击「掉落物」查看掉落列表", 24f,
            TextAlignmentOptions.Center, new Color(TextColor.r, TextColor.g, TextColor.b, 0.65f), _font);
        SetRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 34f), new Vector2(0f, -100f));

        Button closeButton = NewButton("CloseButton", window.transform, "×", 36f, _font);
        SetRect(RT(closeButton.gameObject), new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-52f, -52f));
        UnityEventTools.AddPersistentListener(closeButton.onClick, ui.ClosePanel);

        TextMeshProUGUI emptyHint = NewText("EmptyHint", window.transform,
            "还没有配置怪物。\n用菜单「Tools/怪物图鉴/从敌人预制体自动填充」可以一键生成。",
            28f, TextAlignmentOptions.Center, TextColor, _font);
        SetRect(emptyHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 120f), new Vector2(0f, -90f));
        emptyHint.gameObject.SetActive(false);

        // ---------- 怪物网格 ----------
        RectTransform content = BuildScrollView(window.transform);

        // ---------- 两个弹窗 ----------
        GameObject detailPopup = NewUI("DetailPopup", window.transform);
        Stretch(RT(detailPopup));
        detailPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        GameObject detailBox = NewUI("Box", detailPopup.transform);
        SetRect(RT(detailBox), new Vector2(0.5f, 0.5f), new Vector2(760f, 690f), Vector2.zero);
        detailBox.AddComponent<Image>().color = PopupColor;

        Button detailClose = NewButton("Close", detailBox.transform, "×", 32f, _font);
        SetRect(RT(detailClose.gameObject), new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-16f, -16f));
        UnityEventTools.AddPersistentListener(detailClose.onClick, ui.CloseDetailPopup);

        Image detailIcon = NewImage("Icon", detailBox.transform, Color.white);
        SetRect(detailIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(360f, 300f), new Vector2(0f, -30f));
        detailIcon.preserveAspect = true;

        TextMeshProUGUI detailName = NewText("Name", detailBox.transform, "", 38f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(detailName.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 50f), new Vector2(0f, -345f));

        TextMeshProUGUI detailKills = NewText("Kills", detailBox.transform, "", 24f, TextAlignmentOptions.Center, TextColor, _font);
        SetRect(detailKills.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 34f), new Vector2(0f, -400f));

        TextMeshProUGUI detailDescription = NewText("Description", detailBox.transform, "", 26f, TextAlignmentOptions.TopLeft, TextColor, _font);
        SetRect(detailDescription.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 210f), new Vector2(0f, -450f));

        detailPopup.SetActive(false);

        GameObject dropsPopup = NewUI("DropsPopup", window.transform);
        Stretch(RT(dropsPopup));
        dropsPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        GameObject dropsBox = NewUI("Box", dropsPopup.transform);
        SetRect(RT(dropsBox), new Vector2(0.5f, 0.5f), new Vector2(620f, 560f), Vector2.zero);
        dropsBox.AddComponent<Image>().color = PopupColor;

        Button dropsClose = NewButton("Close", dropsBox.transform, "×", 32f, _font);
        SetRect(RT(dropsClose.gameObject), new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-16f, -16f));
        UnityEventTools.AddPersistentListener(dropsClose.onClick, ui.CloseDropsPopup);

        TextMeshProUGUI dropsTitle = NewText("Title", dropsBox.transform, "", 34f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(dropsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(520f, 50f), new Vector2(0f, -30f));

        // 掉落列表做成可滑动的：条目多了就直接拖动 / 滚轮，不会被弹窗截断
        RectTransform dropsList = BuildVerticalScrollList(dropsBox.transform, 30f, 30f, 100f, 30f, out ScrollRect dropsScroll);

        dropsPopup.SetActive(false);

        // ---------- 接线 ----------
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("windowRoot").objectReferenceValue = window;
        serialized.FindProperty("content").objectReferenceValue = content;
        serialized.FindProperty("slotPrefab").objectReferenceValue = _slotPrefab;
        serialized.FindProperty("emptyHint").objectReferenceValue = emptyHint.gameObject;
        serialized.FindProperty("detailPopup").objectReferenceValue = detailPopup;
        serialized.FindProperty("detailIcon").objectReferenceValue = detailIcon;
        serialized.FindProperty("detailName").objectReferenceValue = detailName;
        serialized.FindProperty("detailKills").objectReferenceValue = detailKills;
        serialized.FindProperty("detailDescription").objectReferenceValue = detailDescription;
        serialized.FindProperty("dropsPopup").objectReferenceValue = dropsPopup;
        serialized.FindProperty("dropsTitle").objectReferenceValue = dropsTitle;
        serialized.FindProperty("dropsList").objectReferenceValue = dropsList;
        serialized.FindProperty("dropsScroll").objectReferenceValue = dropsScroll;
        serialized.FindProperty("dropRowPrefab").objectReferenceValue = _rowPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 数据组件也和界面组件接上（同物体，脚本也会自动找，这里只是保险）
        SerializedObject bestiarySerialized = new SerializedObject(ui);
        bestiarySerialized.FindProperty("bestiary").objectReferenceValue = bestiary;
        bestiarySerialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static RectTransform BuildScrollView(Transform _parent)
    {
        GameObject scrollGo = NewUI("Scroll", _parent);
        Stretch(RT(scrollGo), 70f, 70f, 140f, 70f);

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        GameObject viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);

        // 透明但能接收射线：鼠标停在格子之间的空白处也能滚轮滚动
        Image catcher = scrollGo.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        GameObject contentGo = NewUI("Content", viewport.transform);
        RectTransform content = RT(contentGo);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(215f, 300f);
        grid.spacing = new Vector2(22f, 22f);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;

        return content;
    }

    /// <summary>
    /// 竖排的可滑动列表：Scroll → Viewport（裁切）→ List（竖排 + 随内容长高）。
    /// 掉落物弹窗用它；条目比弹窗高时可以直接拖动 / 滚轮滑动。
    /// </summary>
    private static RectTransform BuildVerticalScrollList(Transform _parent, float _left, float _right, float _top, float _bottom, out ScrollRect _scroll)
    {
        GameObject scrollGo = NewUI("Scroll", _parent);
        Stretch(RT(scrollGo), _left, _right, _top, _bottom);

        _scroll = scrollGo.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 45f;

        // 透明但能接收射线：鼠标停在行与行之间的空白处也能滚轮滚动
        Image catcher = scrollGo.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        GameObject viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        _scroll.viewport = RT(viewport);

        GameObject contentGo = NewUI("List", viewport.transform);
        RectTransform content = RT(contentGo);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll.content = content;

        return content;
    }

    #endregion

    #region 小工具

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    private static TMP_FontAsset FindChineseFont()
    {
        TMP_FontAsset dynamicFallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

            if (candidate == null)
                continue;

            if (candidate.HasCharacter('怪'))
                return candidate;

            if (dynamicFallback == null && candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic && LooksLikeCjkFont(candidate.name))
                dynamicFallback = candidate;
        }

        if (dynamicFallback != null)
            return dynamicFallback;

        Debug.LogWarning("怪物图鉴：没有找到带中文字形的 TMP 字体，生成的预制体会用默认字体，中文可能显示成方块。" +
                         "请在预制体里手动把字体换成 Assets/Graphics/Font 里的中文字体。");

        return TMP_Settings.defaultFontAsset;
    }

    private static bool LooksLikeCjkFont(string _name)
    {
        if (string.IsNullOrEmpty(_name))
            return false;

        string name = _name.ToLowerInvariant();

        return name.Contains("cjk") || name.Contains("noto") || name.Contains("han")
            || name.Contains("xinwei") || name.Contains("song") || name.Contains("hei");
    }

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

    private static TextMeshProUGUI NewText(string _name, Transform _parent, string _text, float _size, TextAlignmentOptions _align, Color _color, TMP_FontAsset _font)
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

    private static Button NewButton(string _name, Transform _parent, string _label, float _fontSize, TMP_FontAsset _font)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(_label))
        {
            TextMeshProUGUI text = NewText("Label", go.transform, _label, _fontSize, TextAlignmentOptions.Center, TextColor, _font);
            Stretch(text.rectTransform);
        }

        return button;
    }

    #endregion
}
