using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成对话界面的预制体。
/// 菜单：Tools/对话/生成对话界面预制体
///
/// 生成后：
/// - Assets/Prefabs/UI/DialoguePanel.prefab        对话框 + 头顶提示 + 选项容器（挂到 Canvas 里用）
/// - Assets/Prefabs/UI/DialogueChoiceButton.prefab 选项按钮（改样式改这个）
/// </summary>
public static class DialoguePrefabBuilder
{
    private const string FolderPath = "Assets/Prefabs/UI";
    private const string PanelPath = FolderPath + "/DialoguePanel.prefab";
    private const string ButtonPath = FolderPath + "/DialogueChoiceButton.prefab";

    private static readonly Color BoxColor = new Color(0.05f, 0.06f, 0.09f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.4f, 1f);
    private static readonly Color PromptColor = new Color(0.05f, 0.06f, 0.09f, 0.85f);

    [MenuItem("Tools/对话/生成对话界面预制体")]
    private static void BuildPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        TMP_FontAsset font = FindChineseFont();

        GameObject buttonRoot = BuildChoiceButton(font);
        GameObject buttonAsset = PrefabUtility.SaveAsPrefabAsset(buttonRoot, ButtonPath);
        Object.DestroyImmediate(buttonRoot);

        GameObject panelRoot = BuildPanel(font, buttonAsset.GetComponent<Button>());
        PrefabUtility.SaveAsPrefabAsset(panelRoot, PanelPath);
        Object.DestroyImmediate(panelRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"对话系统：界面预制体已生成。\n{PanelPath}\n{ButtonPath}\n" +
                  "把 DialoguePanel 拖到 Canvas 里，再给 NPC 挂 DialogueNpc 组件并配好对话资产即可。");
    }

    private static GameObject BuildChoiceButton(TMP_FontAsset _font)
    {
        GameObject root = NewUI("DialogueChoiceButton", null);
        RT(root).sizeDelta = new Vector2(620f, 64f);

        Image image = root.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;

        root.AddComponent<LayoutElement>().preferredHeight = 64f;

        TextMeshProUGUI label = NewText("Label", root.transform, "选项", 26f, TextAlignmentOptions.Center, TextColor, _font);
        Stretch(label.rectTransform, 20f, 20f, 4f, 4f);

        return root;
    }

    private static GameObject BuildPanel(TMP_FontAsset _font, Button _choiceButtonPrefab)
    {
        GameObject root = NewUI("DialoguePanel", null);
        Stretch(RT(root));

        DialogueManager manager = root.AddComponent<DialogueManager>();
        DialogueUI ui = root.AddComponent<DialogueUI>();

        // 自己管理显示：UI.cs 切换菜单时不会把它整个关掉（否则按 W 的提示就不显示了）
        root.AddComponent<UI_SelfManagedPanel>();

        // ---------- 头顶提示 ----------
        GameObject prompt = NewUI("Prompt", root.transform);
        SetRect(RT(prompt), new Vector2(0.5f, 0.5f), new Vector2(280f, 56f), Vector2.zero);
        prompt.AddComponent<Image>().color = PromptColor;

        TextMeshProUGUI promptLabel = NewText("Label", prompt.transform, "按 W 对话", 26f, TextAlignmentOptions.Center, TextColor, _font);
        Stretch(promptLabel.rectTransform, 10f, 10f, 4f, 4f);

        prompt.SetActive(false);

        // ---------- 对话框（屏幕上方） ----------
        GameObject box = NewUI("Box", root.transform);
        RectTransform boxRect = RT(box);
        boxRect.anchorMin = new Vector2(0f, 1f);
        boxRect.anchorMax = new Vector2(1f, 1f);
        boxRect.pivot = new Vector2(0.5f, 1f);
        boxRect.anchoredPosition = new Vector2(0f, -40f);
        boxRect.sizeDelta = new Vector2(-240f, 240f);

        box.AddComponent<Image>().color = BoxColor;

        TextMeshProUGUI speaker = NewText("Speaker", box.transform, "铁匠", 30f, TextAlignmentOptions.Left, AccentColor, _font);
        SetRect(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 44f), new Vector2(36f, -16f));

        TextMeshProUGUI body = NewText("Body", box.transform, "对话内容", 30f, TextAlignmentOptions.TopLeft, TextColor, _font);
        Stretch(body.rectTransform, 36f, 36f, 70f, 24f);

        TextMeshProUGUI continueHint = NewText("ContinueHint", box.transform, "▼", 28f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(continueHint.rectTransform, new Vector2(1f, 0f), new Vector2(60f, 40f), new Vector2(-24f, 16f));

        box.SetActive(false);

        // ---------- 选项 ----------
        GameObject choices = NewUI("Choices", root.transform);
        RectTransform choicesRect = RT(choices);
        choicesRect.anchorMin = new Vector2(0.5f, 1f);
        choicesRect.anchorMax = new Vector2(0.5f, 1f);
        choicesRect.pivot = new Vector2(0.5f, 1f);
        choicesRect.anchoredPosition = new Vector2(0f, -300f);
        choicesRect.sizeDelta = new Vector2(620f, 200f);

        VerticalLayoutGroup choiceLayout = choices.AddComponent<VerticalLayoutGroup>();
        choiceLayout.spacing = 12f;
        choiceLayout.childAlignment = TextAnchor.UpperCenter;
        choiceLayout.childControlWidth = true;
        choiceLayout.childControlHeight = true;
        choiceLayout.childForceExpandWidth = true;
        choiceLayout.childForceExpandHeight = false;

        choices.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        choices.SetActive(false);

        // ---------- 接线 ----------
        SerializedObject uiSerialized = new SerializedObject(ui);
        uiSerialized.FindProperty("promptRoot").objectReferenceValue = prompt;
        uiSerialized.FindProperty("promptText").objectReferenceValue = promptLabel;
        uiSerialized.FindProperty("promptRect").objectReferenceValue = RT(prompt);
        uiSerialized.FindProperty("boxRoot").objectReferenceValue = box;
        uiSerialized.FindProperty("speakerText").objectReferenceValue = speaker;
        uiSerialized.FindProperty("bodyText").objectReferenceValue = body;
        uiSerialized.FindProperty("continueHint").objectReferenceValue = continueHint.gameObject;
        uiSerialized.FindProperty("choiceRoot").objectReferenceValue = choicesRect;
        uiSerialized.FindProperty("choiceButtonPrefab").objectReferenceValue = _choiceButtonPrefab;
        uiSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject managerSerialized = new SerializedObject(manager);
        managerSerialized.FindProperty("ui").objectReferenceValue = ui;
        managerSerialized.ApplyModifiedPropertiesWithoutUndo();

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

            if (candidate.HasCharacter('对'))
                return candidate;

            if (dynamicFallback == null && candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic && LooksLikeCjkFont(candidate.name))
                dynamicFallback = candidate;
        }

        if (dynamicFallback != null)
            return dynamicFallback;

        Debug.LogWarning("对话系统：没有找到带中文字形的 TMP 字体，生成的预制体会用默认字体，中文可能显示成方块。");
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

    #endregion
}
