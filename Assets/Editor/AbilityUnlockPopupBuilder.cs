using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成「解锁新能力」弹窗，并写进 Canvas 预制体（所有场景共用）。
/// 菜单：Tools/UI/生成能力解锁弹窗
///
/// 结构：AbilityUnlockPopup（常驻，带 UI_SelfManagedPanel + AbilityUnlockUI）
///       └ Window（真正显示的内容，默认隐藏）
///         ├ Dim       全屏压暗 + 挡点击
///         └ Box       中间的深色面板
///           ├ Title / Name / Description
///           └ CloseButton
/// </summary>
public static class AbilityUnlockPopupBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Canvas.prefab";
    private const string RootName = "AbilityUnlockPopup";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);
    private static readonly Color BoxColor = new Color(0.07f, 0.08f, 0.12f, 0.98f);
    private static readonly Color TitleColor = new Color(0.95f, 0.82f, 0.4f, 1f);
    private static readonly Color NameColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Color TextColor = new Color(0.84f, 0.85f, 0.9f, 1f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);

    [MenuItem("Tools/UI/生成能力解锁弹窗")]
    private static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("能力解锁弹窗", $"找不到 {CanvasPrefabPath}", "好");
            return;
        }

        GameObject canvas = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);

        Transform old = canvas.transform.Find(RootName);

        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        TMP_FontAsset font = FindFont(canvas);

        // ---------- 根节点（常驻） ----------
        GameObject root = NewUI(RootName, canvas.transform);
        Stretch(RT(root));
        root.AddComponent<UI_SelfManagedPanel>();
        AbilityUnlockUI ui = root.AddComponent<AbilityUnlockUI>();

        // ---------- Window ----------
        GameObject window = NewUI("Window", root.transform);
        Stretch(RT(window));

        GameObject dim = NewUI("Dim", window.transform);
        Stretch(RT(dim));
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = DimColor;      // 挡点击，弹窗时点不到后面

        GameObject box = NewUI("Box", window.transform);
        Place(RT(box), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 400f));
        box.AddComponent<Image>().color = BoxColor;

        TextMeshProUGUI title = NewText("Title", box.transform, "解锁新能力！", 40f, TextAlignmentOptions.Center, TitleColor, font);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(700f, 56f));

        TextMeshProUGUI abilityName = NewText("AbilityName", box.transform, "蹬墙跳", 56f, TextAlignmentOptions.Center, NameColor, font);
        Place(abilityName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(700f, 76f));

        TextMeshProUGUI description = NewText("Description", box.transform, "说明", 28f, TextAlignmentOptions.Center, TextColor, font);
        Place(description.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(700f, 100f));

        Button close = NewButton("CloseButton", box.transform, "确定", 28f, font);
        Place(RT(close.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(240f, 64f));

        window.SetActive(false);

        // ---------- 接线 ----------
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("windowRoot").objectReferenceValue = window;
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("abilityNameText").objectReferenceValue = abilityName;
        serialized.FindProperty("descriptionText").objectReferenceValue = description;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 排在 DarkScreen 前面：淡入淡出时盖住它，其它面板仍然在它上面
        Transform dark = canvas.transform.Find("DarkScreen");

        if (dark != null)
            root.transform.SetSiblingIndex(dark.GetSiblingIndex());
        else
            root.transform.SetAsLastSibling();

        PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPrefabPath);
        PrefabUtility.UnloadPrefabContents(canvas);

        AssetDatabase.SaveAssets();

        Debug.Log("能力解锁弹窗：已写进 " + CanvasPrefabPath + "（拾取蹬墙跳 / 二段跳解锁道具时会弹出）。");
    }

    #region 小工具

    private static TMP_FontAsset FindFont(GameObject _canvas)
    {
        TextMeshProUGUI existing = _canvas.GetComponentInChildren<TextMeshProUGUI>(true);

        if (existing != null && existing.font != null)
            return existing.font;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

            if (candidate != null && candidate.HasCharacter('解'))
                return candidate;
        }

        return null;
    }

    private static GameObject NewUI(string _name, Transform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;

        if (_parent != null)
            go.transform.SetParent(_parent, false);

        return go;
    }

    private static RectTransform RT(GameObject _go) => (RectTransform)_go.transform;

    private static void Stretch(RectTransform _rect, float _left = 0f, float _right = 0f, float _top = 0f, float _bottom = 0f)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.offsetMin = new Vector2(_left, _bottom);
        _rect.offsetMax = new Vector2(-_right, -_top);
    }

    private static void Place(RectTransform _rect, Vector2 _anchor, Vector2 _pivot, Vector2 _anchoredPosition, Vector2 _size)
    {
        _rect.anchorMin = _anchor;
        _rect.anchorMax = _anchor;
        _rect.pivot = _pivot;
        _rect.anchoredPosition = _anchoredPosition;
        _rect.sizeDelta = _size;
    }

    private static TextMeshProUGUI NewText(string _name, Transform _parent, string _text, float _size,
        TextAlignmentOptions _alignment, Color _color, TMP_FontAsset _font)
    {
        GameObject go = NewUI(_name, _parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = _text;
        tmp.fontSize = _size;
        tmp.alignment = _alignment;
        tmp.color = _color;
        tmp.raycastTarget = false;

        if (_font != null)
            tmp.font = _font;

        return tmp;
    }

    private static Button NewButton(string _name, Transform _parent, string _label, float _size, TMP_FontAsset _font)
    {
        GameObject go = NewUI(_name, _parent);

        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI label = NewText("Label", go.transform, _label, _size, TextAlignmentOptions.Center, NameColor, _font);
        Stretch(label.rectTransform, 8f, 8f, 4f, 4f);

        return button;
    }

    #endregion
}
