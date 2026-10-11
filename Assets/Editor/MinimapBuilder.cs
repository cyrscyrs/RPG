using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成小地图界面，并写进 Canvas 预制体（三个场景共用同一份 UI）。
/// 菜单：Tools/小地图/生成小地图界面
///
/// 生成的结构：
/// Minimap                 挂在 Canvas 下，带 UI_SelfManagedPanel（切菜单时不会被关掉）
/// ├── Corner              右上角小地图：Frame(底) + Map(RawImage)
/// └── Full                M 键大地图（默认隐藏）：Dim + Title + Map + Hint
///
/// Map 上的 RawImage 贴图、图标、玩家方块都是 MinimapUI 在运行时建的，
/// 因为地图范围要按当前场景 Ground 层的包围盒来算。
/// </summary>
public static class MinimapBuilder
{
    private const string CanvasPrefabPath = "Assets/Prefabs/UI/Canvas.prefab";
    private const string RootName = "Minimap";

    private static readonly Color FrameColor = new Color(0.05f, 0.06f, 0.09f, 0.9f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.92f);
    private static readonly Color TitleColor = new Color(0.93f, 0.9f, 0.82f, 1f);
    private static readonly Color HintColor = new Color(0.72f, 0.74f, 0.8f, 1f);

    [MenuItem("Tools/小地图/生成小地图界面")]
    private static void BuildMinimap()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("小地图", $"找不到 {CanvasPrefabPath}", "好");
            return;
        }

        GameObject canvas = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);

        Transform old = canvas.transform.Find(RootName);

        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        TMP_FontAsset font = FindFont(canvas);

        GameObject minimap = NewUI(RootName, canvas.transform);
        Stretch(RT(minimap));
        minimap.AddComponent<UI_SelfManagedPanel>();
        MinimapUI ui = minimap.AddComponent<MinimapUI>();

        // ---------- 右上角小地图 ----------
        GameObject corner = NewUI("Corner", minimap.transform);
        RectTransform cornerRect = RT(corner);
        // 右上角往下让开一点：InGame_UI 的灵魂/货币计数就占着右上角（约 416x246）
        Place(cornerRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -262f), new Vector2(380f, 100f));

        GameObject frame = NewUI("Frame", corner.transform);
        Stretch(RT(frame));
        frame.AddComponent<Image>().color = FrameColor;

        GameObject cornerMapGo = NewUI("Map", corner.transform);
        Stretch(RT(cornerMapGo), 5f, 5f, 5f, 5f);
        RawImage cornerMap = cornerMapGo.AddComponent<RawImage>();
        cornerMap.color = Color.white;
        cornerMap.raycastTarget = false;

        // ---------- M 键大地图 ----------
        GameObject full = NewUI("Full", minimap.transform);
        Stretch(RT(full));

        GameObject dim = NewUI("Dim", full.transform);
        Stretch(RT(dim));
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = DimColor;   // 挡住后面的点击

        TextMeshProUGUI title = NewText("Title", full.transform, "地图", 44f, TextAlignmentOptions.Center, TitleColor, font);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 60f));

        GameObject fullMapGo = NewUI("Map", full.transform);
        RectTransform fullMapRect = RT(fullMapGo);
        Place(fullMapRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f));
        RawImage fullMap = fullMapGo.AddComponent<RawImage>();
        fullMap.color = Color.white;
        fullMap.raycastTarget = false;

        // 放大之后，取景窗口外面的图标要裁掉，不能画到地图框外面
        fullMapGo.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = NewText("Hint", full.transform, "滚轮缩放 · 按住左键拖动 · M 或 Esc 关闭", 26f, TextAlignmentOptions.Center, HintColor, font);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(700f, 40f));

        full.SetActive(false);

        // ---------- 接线 ----------
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("cornerRoot").objectReferenceValue = corner;
        serialized.FindProperty("cornerMap").objectReferenceValue = cornerMap;
        serialized.FindProperty("fullRoot").objectReferenceValue = full;
        serialized.FindProperty("fullMap").objectReferenceValue = fullMap;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 排在 DarkScreen 前面：淡入淡出时盖住小地图，其它菜单仍然在它上面
        Transform dark = canvas.transform.Find("DarkScreen");

        if (dark != null)
            minimap.transform.SetSiblingIndex(dark.GetSiblingIndex());
        else
            minimap.transform.SetAsLastSibling();

        PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPrefabPath);
        PrefabUtility.UnloadPrefabContents(canvas);

        AssetDatabase.SaveAssets();

        Debug.Log("小地图：界面已写进 " + CanvasPrefabPath +
                  "（右上角小地图 + M 键大地图）。回到场景后按 Tab 隐藏小地图、按 M 开地图。");
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

            if (candidate != null && candidate.HasCharacter('地'))
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

    #endregion
}
