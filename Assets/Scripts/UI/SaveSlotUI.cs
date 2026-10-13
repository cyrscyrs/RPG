using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单的「选择存档」界面（3 个槽位）。
///
/// - **继续游戏**：选一个已有存档的槽位读档进游戏；空槽位显示「无」、点不动。
/// - **新游戏**：选一个槽位存新档；槽位里已经有存档时，先弹窗问「是否覆盖」，
///   选「是」覆盖并进游戏，选「否」只关掉弹窗、回到槽位列表。
///
/// 整个界面是运行时搭的，挂在主菜单的 Canvas 下面（不往场景里放东西）。
/// 外观想改就改下面那几行颜色 / 尺寸常量。
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    public enum Mode
    {
        LoadGame,   // 继续游戏：只能选有存档的槽位
        NewGame     // 新游戏：所有槽位都能选（有存档的要先确认覆盖）
    }

    #region 外观

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.15f, 0.98f);
    private static readonly Color TitleColor = new Color(1f, 0.92f, 0.65f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.93f, 0.95f, 1f);
    private static readonly Color MutedTextColor = new Color(0.58f, 0.6f, 0.66f, 1f);
    private static readonly Color SlotUsedColor = new Color(0.17f, 0.3f, 0.25f, 1f);
    private static readonly Color SlotEmptyColor = new Color(0.21f, 0.22f, 0.27f, 1f);
    private static readonly Color SlotDisabledColor = new Color(0.15f, 0.15f, 0.18f, 0.85f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.45f, 0.75f, 1f);

    private const float PanelWidth = 960f;
    private const float PanelHeight = 720f;
    private const float RowHeight = 150f;
    private const float RowGap = 26f;

    #endregion

    private TMP_FontAsset font;
    private Mode mode;
    private Action<int> onSlotChosen;

    private TMP_Text titleText;
    private SlotRow[] rows;
    private GameObject confirmRoot;
    private TMP_Text confirmText;
    private int pendingOverwriteSlot = -1;

    private class SlotRow
    {
        public Button button;
        public Image background;
        public TMP_Text nameText;
        public TMP_Text infoText;
    }

    #region 对外接口

    /// <summary>建一个界面（默认是藏着的）。_font 传 null 会用 TMP 默认字体。</summary>
    public static SaveSlotUI Create(Transform _parent, TMP_FontAsset _font)
    {
        GameObject root = new GameObject("SaveSlotPanel", typeof(RectTransform), typeof(Image));

        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(_parent, false);
        Stretch(rect);
        root.layer = 5;

        Image dim = root.GetComponent<Image>();
        dim.color = DimColor;
        dim.raycastTarget = true;   // 挡住下面的主菜单按钮

        SaveSlotUI ui = root.AddComponent<SaveSlotUI>();
        ui.font = _font;
        ui.Build();

        root.transform.SetAsLastSibling();
        ui.Close();

        return ui;
    }

    /// <summary>「继续游戏」：打开后只能选有存档的槽位。</summary>
    public void OpenLoad(Action<int> _onSlotChosen) => Open(Mode.LoadGame, _onSlotChosen);

    /// <summary>「新游戏」：打开后每个槽位都能选，已有存档的先问要不要覆盖。</summary>
    public void OpenNewGame(Action<int> _onSlotChosen) => Open(Mode.NewGame, _onSlotChosen);

    public void Close()
    {
        onSlotChosen = null;
        HideConfirm();

        gameObject.SetActive(false);
    }

    #endregion

    private void Open(Mode _mode, Action<int> _onSlotChosen)
    {
        mode = _mode;
        onSlotChosen = _onSlotChosen;

        HideConfirm();

        titleText.text = _mode == Mode.LoadGame ? "选择存档（继续游戏）" : "选择存档位（新游戏）";

        for (int i = 0; i < rows.Length; i++)
            RefreshRow(i);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>把一个槽位的信息刷到界面上（存档界面每次打开都重读一遍）。</summary>
    private void RefreshRow(int _index)
    {
        SlotRow row = rows[_index];
        SaveManager.SlotInfo info = SaveManager.instance != null
            ? SaveManager.instance.GetSlotInfo(_index)
            : default;

        row.nameText.text = $"存档 {_index + 1}";

        if (!info.hasSave)
        {
            // 没存档：继续游戏时不能选，新游戏时正好拿来存新档
            bool pickable = mode == Mode.NewGame;

            row.infoText.text = "无";
            row.infoText.color = MutedTextColor;
            row.nameText.color = pickable ? TextColor : MutedTextColor;
            row.button.interactable = pickable;
            row.background.color = pickable ? SlotEmptyColor : SlotDisabledColor;

            return;
        }

        row.infoText.text =
            $"创建时间：{info.createdAt}             " +
            $"最近保存：{info.lastSavedAt}\n" +
            $"货币：{info.currency}";
        row.infoText.color = TextColor;
        row.nameText.color = TextColor;
        row.button.interactable = true;
        row.background.color = SlotUsedColor;
    }

    private void OnSlotClicked(int _index)
    {
        AudioManager.PlayClickSfx();

        // 新游戏 + 槽位非空：先问要不要覆盖，玩家点「是」才真的往下走
        if (mode == Mode.NewGame && SaveManager.instance != null &&
            SaveManager.instance.GetSlotInfo(_index).hasSave)
        {
            ShowConfirm(_index);
            return;
        }

        Choose(_index);
    }

    private void Choose(int _index)
    {
        Action<int> callback = onSlotChosen;
        onSlotChosen = null;

        gameObject.SetActive(false);

        callback?.Invoke(_index);
    }

    private void ShowConfirm(int _index)
    {
        pendingOverwriteSlot = _index;

        confirmText.text = $"{_index + 1} 号存档已有存档。\n覆盖后原来的进度就没了，要覆盖吗？";
        confirmRoot.SetActive(true);
        confirmRoot.transform.SetAsLastSibling();
    }

    private void HideConfirm()
    {
        pendingOverwriteSlot = -1;

        if (confirmRoot != null)
            confirmRoot.SetActive(false);
    }

    private void OnConfirmYes()
    {
        AudioManager.PlayClickSfx();

        int slot = pendingOverwriteSlot;
        HideConfirm();

        if (slot >= 0)
            Choose(slot);
    }

    private void OnConfirmNo()
    {
        AudioManager.PlayClickSfx();

        // 只关掉弹窗，回到槽位列表
        HideConfirm();
    }

    private void OnBackClicked()
    {
        AudioManager.PlayClickSfx();
        Close();
    }

    #region 搭 UI

    private void Build()
    {
        GameObject panel = CreateUI("Panel", transform, typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = true;

        titleText = CreateText("Title", panelRect, 46f);
        RectTransform titleRect = (RectTransform)titleText.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -34f);
        titleRect.sizeDelta = new Vector2(-80f, 64f);
        titleText.color = TitleColor;
        titleText.alignment = TextAlignmentOptions.Center;

        // 3 个槽位，从上往下排
        rows = new SlotRow[SaveManager.SlotCount];

        float firstRowTop = -120f;

        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;   // 闭包捕获：不能在 lambda 里直接用 i

            GameObject rowObject = CreateUI($"Slot{i}", panelRect, typeof(Image), typeof(Button));
            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, firstRowTop - i * (RowHeight + RowGap));
            rowRect.sizeDelta = new Vector2(-64f, RowHeight);

            Image rowImage = rowObject.GetComponent<Image>();
            rowImage.color = SlotEmptyColor;

            Button button = rowObject.GetComponent<Button>();
            button.targetGraphic = rowImage;
            button.onClick.AddListener(() => OnSlotClicked(index));

            TMP_Text name = CreateText("Name", rowRect, 34f);
            RectTransform nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(30f, -16f);
            nameRect.sizeDelta = new Vector2(420f, 44f);
            name.color = TextColor;
            name.alignment = TextAlignmentOptions.Left;

            TMP_Text info = CreateText("Info", rowRect, 26f);
            RectTransform infoRect = (RectTransform)info.transform;
            infoRect.anchorMin = Vector2.zero;
            infoRect.anchorMax = Vector2.one;
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.offsetMin = new Vector2(30f, 14f);
            infoRect.offsetMax = new Vector2(-30f, -62f);
            info.color = TextColor;
            info.alignment = TextAlignmentOptions.TopLeft;

            rows[i] = new SlotRow
            {
                button = button,
                background = rowImage,
                nameText = name,
                infoText = info
            };
        }

        // 返回
        GameObject back = CreateUI("Back", panelRect, typeof(Image), typeof(Button));
        RectTransform backRect = (RectTransform)back.transform;
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 30f);
        backRect.sizeDelta = new Vector2(260f, 72f);

        Image backImage = back.GetComponent<Image>();
        backImage.color = ButtonColor;

        Button backButton = back.GetComponent<Button>();
        backButton.targetGraphic = backImage;
        backButton.onClick.AddListener(OnBackClicked);

        TMP_Text backLabel = CreateText("Label", backRect, 32f);
        Stretch((RectTransform)backLabel.transform);
        backLabel.color = Color.white;
        backLabel.alignment = TextAlignmentOptions.Center;
        backLabel.text = "返回";

        BuildConfirm();
    }

    /// <summary>覆盖存档的确认弹窗：一句提示 + 「是 / 否」。</summary>
    private void BuildConfirm()
    {
        confirmRoot = CreateUI("Confirm", transform, typeof(Image));
        Stretch((RectTransform)confirmRoot.transform);
        confirmRoot.layer = 5;

        Image dim = confirmRoot.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        dim.raycastTarget = true;

        GameObject box = CreateUI("Box", confirmRoot.transform, typeof(Image));
        RectTransform boxRect = (RectTransform)box.transform;
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(760f, 340f);

        Image boxImage = box.GetComponent<Image>();
        boxImage.color = PanelColor;
        boxImage.raycastTarget = true;

        confirmText = CreateText("Message", boxRect, 32f);
        RectTransform messageRect = (RectTransform)confirmText.transform;
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.offsetMin = new Vector2(40f, 140f);
        messageRect.offsetMax = new Vector2(-40f, -40f);
        confirmText.color = TextColor;
        confirmText.alignment = TextAlignmentOptions.Center;

        CreateConfirmButton("Yes", "是", new Vector2(-110f, 40f), OnConfirmYes);
        CreateConfirmButton("No", "否", new Vector2(110f, 40f), OnConfirmNo);

        confirmRoot.SetActive(false);
    }

    private void CreateConfirmButton(string _name, string _label, Vector2 _position, UnityEngine.Events.UnityAction _onClick)
    {
        Transform parent = confirmRoot.transform.Find("Box");

        GameObject go = CreateUI(_name, parent, typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = _position;
        rect.sizeDelta = new Vector2(180f, 72f);

        Image image = go.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(_onClick);

        TMP_Text text = CreateText("Label", rect, 32f);
        Stretch((RectTransform)text.transform);
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.text = _label;
    }

    private GameObject CreateUI(string _name, Transform _parent, params Type[] _components)
    {
        GameObject go = new GameObject(_name, _components);
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);

        return go;
    }

    private TMP_Text CreateText(string _name, Transform _parent, float _size)
    {
        TMP_Text text = CreateUI(_name, _parent, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();

        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
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
