using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成装备强化界面的预制体。
/// 菜单：Tools/装备/生成强化界面预制体
///
/// 生成后：
/// - Assets/Prefabs/UI/EquipmentUpgradePanel.prefab 强化界面（拖到 Canvas 里用）
/// - Assets/Prefabs/UI/EquipmentUpgradeSlot.prefab  左侧装备格（改样式改这个）
/// </summary>
public static class EquipmentUpgradePrefabBuilder
{
    private const string FolderPath = "Assets/Prefabs/UI";
    private const string PanelPath = FolderPath + "/EquipmentUpgradePanel.prefab";
    private const string SlotPath = FolderPath + "/EquipmentUpgradeSlot.prefab";
    private const string RequirementRowPath = FolderPath + "/EquipmentUpgradeRequirementRow.prefab";

    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    private static readonly Color SlotColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.28f, 0.36f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.4f, 1f);

    [MenuItem("Tools/装备/生成强化界面预制体")]
    private static void BuildPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        TMP_FontAsset font = FindChineseFont();

        GameObject slotRoot = BuildSlot(font);
        GameObject slotAsset = PrefabUtility.SaveAsPrefabAsset(slotRoot, SlotPath);
        Object.DestroyImmediate(slotRoot);

        GameObject rowRoot = BuildRequirementRow(font);
        GameObject rowAsset = PrefabUtility.SaveAsPrefabAsset(rowRoot, RequirementRowPath);
        Object.DestroyImmediate(rowRoot);

        GameObject panelRoot = BuildPanel(font, slotAsset.GetComponent<EquipmentUpgradeSlot>(), rowAsset.GetComponent<EquipmentUpgradeRequirementRow>());
        PrefabUtility.SaveAsPrefabAsset(panelRoot, PanelPath);
        Object.DestroyImmediate(panelRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"装备强化：界面预制体已生成。\n{PanelPath}\n{SlotPath}\n" +
                  "把 EquipmentUpgradePanel 拖到 Canvas 里即可（和 DialoguePanel 一样挂哪个 Canvas 都行）。");
    }

    private static GameObject BuildSlot(TMP_FontAsset _font)
    {
        GameObject root = NewUI("EquipmentUpgradeSlot", null);
        RT(root).sizeDelta = new Vector2(900f, 96f);

        Image background = root.AddComponent<Image>();
        background.color = SlotColor;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        root.AddComponent<LayoutElement>().preferredHeight = 96f;

        Image icon = NewImage("Icon", root.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(68f, 68f), new Vector2(14f, 0f));

        TextMeshProUGUI nameText = NewText("Name", root.transform, "木剑 +10", 28f, TextAlignmentOptions.Left, AccentColor, _font);
        SetRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(560f, 42f), new Vector2(98f, 14f));

        TextMeshProUGUI levelText = NewText("Level", root.transform, "Lv.10 / 20", 22f, TextAlignmentOptions.Left,
            new Color(TextColor.r, TextColor.g, TextColor.b, 0.75f), _font);
        SetRect(levelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(560f, 32f), new Vector2(98f, -20f));

        EquipmentUpgradeSlot slot = root.AddComponent<EquipmentUpgradeSlot>();

        SerializedObject serialized = new SerializedObject(slot);
        serialized.FindProperty("iconImage").objectReferenceValue = icon;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("levelText").objectReferenceValue = levelText;
        serialized.FindProperty("background").objectReferenceValue = background;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject BuildPanel(TMP_FontAsset _font, EquipmentUpgradeSlot _slotPrefab, EquipmentUpgradeRequirementRow _rowPrefab)
    {
        GameObject root = NewUI("EquipmentUpgradePanel", null);
        Stretch(RT(root));

        EquipmentUpgradeUI ui = root.AddComponent<EquipmentUpgradeUI>();

        // 自己管理显示：UI.cs 切换菜单时不会把它整个关掉
        root.AddComponent<UI_SelfManagedPanel>();

        // ---------- 背景 ----------
        GameObject window = NewUI("Window", root.transform);
        Stretch(RT(window));
        window.AddComponent<Image>().color = PanelColor;

        TextMeshProUGUI title = NewText("Title", window.transform, "强化装备", 40f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 60f), new Vector2(0f, -40f));

        Button closeButton = NewButton("CloseButton", window.transform, "×", 36f, _font);
        SetRect(RT(closeButton.gameObject), new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-50f, -50f));

        // ---------- 左侧：装备列表 ----------
        GameObject left = NewUI("Left", window.transform);
        RectTransform leftRect = RT(left);
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0.55f, 1f);
        leftRect.offsetMin = new Vector2(70f, 60f);
        leftRect.offsetMax = new Vector2(-20f, -120f);

        GameObject scrollGo = NewUI("Scroll", left.transform);
        Stretch(RT(scrollGo));

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        GameObject viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);

        GameObject contentGo = NewUI("Content", viewport.transform);
        RectTransform content = RT(contentGo);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(900f, 96f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperCenter;

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;

        TextMeshProUGUI emptyHint = NewText("EmptyHint", left.transform, "现在没有可以强化的装备。", 28f, TextAlignmentOptions.Center, TextColor, _font);
        SetRect(emptyHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700f, 80f), Vector2.zero);
        emptyHint.gameObject.SetActive(false);

        // ---------- 右侧：预览与需求 ----------
        GameObject detail = NewUI("Detail", window.transform);
        RectTransform detailRect = RT(detail);
        detailRect.anchorMin = new Vector2(0.55f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(20f, 60f);
        detailRect.offsetMax = new Vector2(-70f, -120f);

        Image detailIcon = NewImage("Icon", detail.transform, Color.white);
        SetRect(detailIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(180f, 180f), new Vector2(0f, -10f));

        TextMeshProUGUI detailName = NewText("Name", detail.transform, "木剑 +10", 36f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(detailName.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 50f), new Vector2(0f, -205f));

        TextMeshProUGUI detailLevel = NewText("Level", detail.transform, "等级 0 / 20", 26f, TextAlignmentOptions.Center, TextColor, _font);
        SetRect(detailLevel.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 34f), new Vector2(0f, -255f));

        TextMeshProUGUI previewText = NewText("Preview", detail.transform, "攻击力 4 → 6", 26f, TextAlignmentOptions.TopLeft, TextColor, _font);
        SetRect(previewText.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 240f), new Vector2(0f, -300f));

        TextMeshProUGUI requirementText = NewText("RequirementTitle", detail.transform, "强化需要：", 26f, TextAlignmentOptions.TopLeft, TextColor, _font);
        SetRect(requirementText.rectTransform, new Vector2(0f, 1f), new Vector2(700f, 36f), new Vector2(30f, -545f));

        GameObject listGo = NewUI("RequirementList", detail.transform);
        RectTransform requirementList = RT(listGo);
        requirementList.anchorMin = new Vector2(0f, 1f);
        requirementList.anchorMax = new Vector2(0f, 1f);
        requirementList.pivot = new Vector2(0f, 1f);
        requirementList.anchoredPosition = new Vector2(30f, -585f);
        requirementList.sizeDelta = new Vector2(700f, 220f);

        VerticalLayoutGroup requirementLayout = listGo.AddComponent<VerticalLayoutGroup>();
        requirementLayout.spacing = 6f;
        requirementLayout.childAlignment = TextAnchor.UpperLeft;
        requirementLayout.childControlWidth = true;
        requirementLayout.childControlHeight = true;
        requirementLayout.childForceExpandWidth = true;
        requirementLayout.childForceExpandHeight = false;

        listGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button upgradeButton = NewButton("UpgradeButton", detail.transform, "强化", 30f, _font);
        RectTransform upgradeRect = RT(upgradeButton.gameObject);
        upgradeRect.anchorMin = new Vector2(0.5f, 0f);
        upgradeRect.anchorMax = new Vector2(0.5f, 0f);
        upgradeRect.pivot = new Vector2(0.5f, 0f);
        upgradeRect.sizeDelta = new Vector2(320f, 76f);
        upgradeRect.anchoredPosition = new Vector2(0f, 20f);

        TextMeshProUGUI upgradeLabel = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();

        // ---------- 接线 ----------
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("windowRoot").objectReferenceValue = window;
        serialized.FindProperty("listRoot").objectReferenceValue = content;
        serialized.FindProperty("slotPrefab").objectReferenceValue = _slotPrefab;
        serialized.FindProperty("emptyHint").objectReferenceValue = emptyHint.gameObject;
        serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
        serialized.FindProperty("detailRoot").objectReferenceValue = detail;
        serialized.FindProperty("detailIcon").objectReferenceValue = detailIcon;
        serialized.FindProperty("detailName").objectReferenceValue = detailName;
        serialized.FindProperty("detailLevel").objectReferenceValue = detailLevel;
        serialized.FindProperty("previewText").objectReferenceValue = previewText;
        serialized.FindProperty("requirementText").objectReferenceValue = requirementText;
        serialized.FindProperty("requirementListRoot").objectReferenceValue = requirementList;
        serialized.FindProperty("requirementRowPrefab").objectReferenceValue = _rowPrefab;
        serialized.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
        serialized.FindProperty("upgradeButtonLabel").objectReferenceValue = upgradeLabel;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    /// <summary>强化需求列表里的一行：图标 + 名称 + 拥有/需要。</summary>
    private static GameObject BuildRequirementRow(TMP_FontAsset _font)
    {
        GameObject root = NewUI("EquipmentUpgradeRequirementRow", null);
        RT(root).sizeDelta = new Vector2(700f, 56f);
        root.AddComponent<LayoutElement>().preferredHeight = 56f;

        Image icon = NewImage("Icon", root.transform, Color.white);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(12f, 0f));

        TextMeshProUGUI nameText = NewText("Name", root.transform, "Iron", 24f, TextAlignmentOptions.Left, TextColor, _font);
        SetRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(400f, 44f), new Vector2(72f, 0f));

        TextMeshProUGUI countText = NewText("Count", root.transform, "0 / 0", 24f, TextAlignmentOptions.Right, TextColor, _font);
        SetRect(countText.rectTransform, new Vector2(1f, 0.5f), new Vector2(180f, 44f), new Vector2(-16f, 0f));

        EquipmentUpgradeRequirementRow row = root.AddComponent<EquipmentUpgradeRequirementRow>();

        SerializedObject serialized = new SerializedObject(row);
        serialized.FindProperty("iconImage").objectReferenceValue = icon;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("countText").objectReferenceValue = countText;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    #region 小工具

    private static TMP_FontAsset FindChineseFont()
    {
        TMP_FontAsset dynamicFallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

            if (candidate == null)
                continue;

            if (candidate.HasCharacter('强'))
                return candidate;

            if (dynamicFallback == null && candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic && LooksLikeCjkFont(candidate.name))
                dynamicFallback = candidate;
        }

        return dynamicFallback != null ? dynamicFallback : TMP_Settings.defaultFontAsset;
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
        image.preserveAspect = true;

        return image;
    }

    private static Button NewButton(string _name, Transform _parent, string _label, float _fontSize, TMP_FontAsset _font)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = NewText("Label", go.transform, _label, _fontSize, TextAlignmentOptions.Center, TextColor, _font);
        Stretch(text.rectTransform, 8f, 8f, 4f, 4f);

        return button;
    }

    #endregion
}
