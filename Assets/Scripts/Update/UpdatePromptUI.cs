using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 热更提示弹窗：「有可更新内容」+ 一个按钮（更新 / 重试）。
///
/// 整个 UI 是运行时搭出来的，不往场景里放东西；它挂在 UpdateManager 那个常驻对象下面，
/// 所以切场景不会被销毁。全屏遮罩会挡住下面的菜单按钮 —— 弹窗出来的时候玩家不会误点进游戏。
/// 外观（颜色 / 尺寸）都在下面这几行常量里，想改直接改。
/// </summary>
public class UpdatePromptUI : MonoBehaviour
{
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color PanelColor = new Color(0.13f, 0.13f, 0.16f, 0.98f);
    private static readonly Color TitleColor = new Color(1f, 0.92f, 0.65f, 1f);
    private static readonly Color BodyColor = new Color(0.93f, 0.93f, 0.95f, 1f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.45f, 0.75f, 1f);

    private const float PanelWidth = 780f;
    private const float PanelHeight = 340f;

    private TMP_Text titleText;
    private TMP_Text messageText;
    private Button actionButton;
    private TMP_Text actionLabel;

    private Action onClick;
    private float autoCloseTime = -1f;

    /// <summary>建一个弹窗（默认是藏着的）。_font 传 null 就用 TMP 默认字体。</summary>
    public static UpdatePromptUI Create(TMP_FontAsset _font, int _sortingOrder)
    {
        GameObject root = new GameObject("UpdatePrompt",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;   // 比游戏里的 UI 高，保证盖在最上面

        // 和游戏里的 Canvas 用同一套缩放，字看起来才一样大
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0f;

        UpdatePromptUI prompt = root.AddComponent<UpdatePromptUI>();
        prompt.Build(_font);
        prompt.Hide();

        return prompt;
    }

    #region 对外接口

    /// <summary>显示一个可以点的弹窗。</summary>
    public void Show(string _title, string _message, string _buttonLabel, Action _onClick)
    {
        gameObject.SetActive(true);

        titleText.text = _title;
        messageText.text = _message;

        onClick = _onClick;
        autoCloseTime = -1f;

        actionButton.gameObject.SetActive(true);
        actionButton.interactable = true;
        actionLabel.text = _buttonLabel;
    }

    /// <summary>只显示状态、不给按钮（更新中 / 更新完成用）。</summary>
    public void ShowProgress(string _title, string _message)
    {
        gameObject.SetActive(true);

        titleText.text = _title;
        messageText.text = _message;

        onClick = null;
        autoCloseTime = -1f;

        actionButton.gameObject.SetActive(false);
    }

    /// <summary>结果提示；_autoCloseAfter &gt; 0 时过几秒自己收起来。</summary>
    public void ShowResult(string _title, string _message, float _autoCloseAfter)
    {
        ShowProgress(_title, _message);

        if (_autoCloseAfter > 0f)
            autoCloseTime = Time.unscaledTime + _autoCloseAfter;
    }

    public void Hide() => gameObject.SetActive(false);

    #endregion

    private void Update()
    {
        if (autoCloseTime > 0f && Time.unscaledTime >= autoCloseTime)
            Hide();
    }

    private void OnButtonClicked()
    {
        // 点完先把回调清掉：连点两下不会重复触发一次更新
        Action action = onClick;
        onClick = null;

        action?.Invoke();
    }

    #region 搭 UI

    private void Build(TMP_FontAsset _font)
    {
        TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;

        // 全屏遮罩：半透明黑，顺便挡住下面的菜单点击
        GameObject dim = CreateUI("Dim", transform, typeof(Image));
        Stretch((RectTransform)dim.transform);

        Image dimImage = dim.GetComponent<Image>();
        dimImage.color = DimColor;
        dimImage.raycastTarget = true;

        // 居中卡片
        GameObject panel = CreateUI("Panel", dim.transform, typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        // 标题
        titleText = CreateText("Title", panel.transform, font, 46f);
        RectTransform titleRect = (RectTransform)titleText.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -34f);
        titleRect.sizeDelta = new Vector2(-80f, 64f);
        titleText.color = TitleColor;
        titleText.alignment = TextAlignmentOptions.Center;

        // 说明文字
        messageText = CreateText("Message", panel.transform, font, 30f);
        RectTransform messageRect = (RectTransform)messageText.transform;
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.offsetMin = new Vector2(48f, 132f);
        messageRect.offsetMax = new Vector2(-48f, -110f);
        messageText.color = BodyColor;
        messageText.alignment = TextAlignmentOptions.Center;

        // 「更新」按钮
        GameObject button = CreateUI("UpdateButton", panel.transform, typeof(Image), typeof(Button));
        RectTransform buttonRect = (RectTransform)button.transform;
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 36f);
        buttonRect.sizeDelta = new Vector2(300f, 78f);

        Image buttonImage = button.GetComponent<Image>();
        buttonImage.color = ButtonColor;

        actionButton = button.GetComponent<Button>();
        actionButton.targetGraphic = buttonImage;
        actionButton.onClick.AddListener(OnButtonClicked);

        actionLabel = CreateText("Label", button.transform, font, 34f);
        Stretch((RectTransform)actionLabel.transform);
        actionLabel.color = Color.white;
        actionLabel.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject CreateUI(string _name, Transform _parent, params Type[] _components)
    {
        GameObject go = new GameObject(_name, _components);
        go.layer = 5;   // UI 层
        go.transform.SetParent(_parent, false);

        return go;
    }

    private static TMP_Text CreateText(string _name, Transform _parent, TMP_FontAsset _font, float _size)
    {
        GameObject go = CreateUI(_name, _parent, typeof(TextMeshProUGUI));

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _size;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.sizeDelta = Vector2.zero;
    }

    #endregion
}
