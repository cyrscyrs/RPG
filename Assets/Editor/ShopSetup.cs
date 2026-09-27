using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店一键搭建：生成商店界面预制体、商店对话资产，并把场景里的 Shop 配好。
/// 菜单都在 Tools/商店/ 下。
/// </summary>
public static class ShopSetup
{
    private const string Folder = "Assets/Prefabs/UI";
    private const string PanelPath = Folder + "/ShopPanel.prefab";
    private const string SlotPath = Folder + "/ShopSlot.prefab";
    private const string DialoguePath = "Assets/Dialogue/Shop_Welcome.asset";

    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    private static readonly Color SlotColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.4f, 1f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.28f, 0.36f, 1f);

    [MenuItem("Tools/商店/生成商店界面预制体")]
    private static void BuildPrefabs()
    {
        EnsureFolder();
        TMP_FontAsset font = Font();

        GameObject slotRoot = Slot(font);
        GameObject slotAsset = PrefabUtility.SaveAsPrefabAsset(slotRoot, SlotPath);
        Object.DestroyImmediate(slotRoot);

        GameObject panelRoot = Panel(font, slotAsset.GetComponent<ShopSlot>());
        PrefabUtility.SaveAsPrefabAsset(panelRoot, PanelPath);
        Object.DestroyImmediate(panelRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"商店：界面预制体已生成。\n{PanelPath}\n{SlotPath}");
    }

    [MenuItem("Tools/商店/生成商店对话")]
    private static void BuildDialogue()
    {
        EnsureFolder();
        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DialoguePath);

        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<NpcDialogue>();
            dialogue.dialogueId = "shop_welcome";
            AssetDatabase.CreateAsset(dialogue, DialoguePath);
        }

        dialogue.onceOnly = false;
        dialogue.hideChosenChoices = false;
        dialogue.nodes = new[]
        {
            new DialogueNode { nodeId = "n1", speaker = "", text = "欢迎光临！", nextNodeId = "n2", choices = new DialogueChoice[0] },
            new DialogueNode
            {
                nodeId = "n2", speaker = "", text = "", nextNodeId = "",
                choices = new[]
                {
                    new DialogueChoice { choiceId = "enter", label = "进入商店", targetNodeId = "", returnToChoices = false, actionId = ShopUI.OpenActionId },
                    new DialogueChoice { choiceId = "leave", label = "离开", targetNodeId = "", returnToChoices = false }
                }
            }
        };

        EditorUtility.SetDirty(dialogue);
        AssetDatabase.SaveAssets();
        Debug.Log($"商店：对话资产已生成/更新 {DialoguePath}（欢迎光临！ + 进入商店 / 离开）");
    }

    [MenuItem("Tools/商店/给场景里的 Shop 配好")]
    private static void SetupSceneShop()
    {
        GameObject shopObject = GameObject.Find("Shop");

        if (shopObject == null)
        {
            EditorUtility.DisplayDialog("商店", "场景里没找到叫 Shop 的物体。", "好");
            return;
        }

        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DialoguePath);

        if (dialogue == null)
        {
            EditorUtility.DisplayDialog("商店", "先生成商店对话（Tools/商店/生成商店对话）。", "好");
            return;
        }

        DialogueNpc npc = shopObject.GetComponent<DialogueNpc>();
        if (npc == null) npc = Undo.AddComponent<DialogueNpc>(shopObject);

        ShopKeeper keeper = shopObject.GetComponent<ShopKeeper>();
        if (keeper == null) keeper = Undo.AddComponent<ShopKeeper>(shopObject);

        // 对话
        SerializedObject npcSerialized = new SerializedObject(npc);
        npcSerialized.FindProperty("npcName").stringValue = "商人";
        npcSerialized.FindProperty("interactionRadius").floatValue = 2.5f;
        SerializedProperty list = npcSerialized.FindProperty("dialogues");
        list.arraySize = 0;
        npcSerialized.FindProperty("defaultDialogue").objectReferenceValue = dialogue;
        npcSerialized.ApplyModifiedProperties();

        // 商品：默认给几件材料 + 装备
        SerializedObject keeperSerialized = new SerializedObject(keeper);
        keeperSerialized.FindProperty("shopName").stringValue = "商店";
        SerializedProperty goods = keeperSerialized.FindProperty("goods");
        goods.arraySize = 0;

        AddGoods(goods, "Assets/Data/Items/Materials/Iron.asset", 40);
        AddGoods(goods, "Assets/Data/Items/Materials/Wood.asset", 30);
        AddGoods(goods, "Assets/Data/Items/Materials/Animal_skin.asset", 50);
        AddGoods(goods, "Assets/Data/Items/Equipments/Weapon/Wooden_sword.asset", 300);
        AddGoods(goods, "Assets/Data/Items/Equipments/Weapon/Stolen_sword.asset", 800);

        keeperSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(shopObject);
        Selection.activeGameObject = shopObject;

        Debug.Log("商店：「Shop」已配好（对话 + 示例商品），记得保存场景，并把 ShopPanel 预制体拖进 Canvas。");
    }

    private static void AddGoods(SerializedProperty _goods, string _path, int _price)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(_path);

        if (item == null)
            return;

        _goods.InsertArrayElementAtIndex(_goods.arraySize);
        SerializedProperty entry = _goods.GetArrayElementAtIndex(_goods.arraySize - 1);
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("price").intValue = _price;
    }

    #region 界面搭建

    private static GameObject Slot(TMP_FontAsset _font)
    {
        GameObject root = New("ShopSlot", null);
        RT(root).sizeDelta = new Vector2(640f, 88f);
        Image background = root.AddComponent<Image>();
        background.color = SlotColor;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        root.AddComponent<LayoutElement>().preferredHeight = 88f;

        Image icon = Img("Icon", root.transform, new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(12f, 0f));
        TextMeshProUGUI nameText = Txt("Name", root.transform, "物品", 26f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(380f, 44f), new Vector2(90f, 0f), _font, TextColor);
        TextMeshProUGUI priceText = Txt("Price", root.transform, "100", 24f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(140f, 44f), new Vector2(-16f, 0f), _font, AccentColor);

        ShopSlot slot = root.AddComponent<ShopSlot>();
        SerializedObject serialized = new SerializedObject(slot);
        serialized.FindProperty("iconImage").objectReferenceValue = icon;
        serialized.FindProperty("nameText").objectReferenceValue = nameText;
        serialized.FindProperty("priceText").objectReferenceValue = priceText;
        serialized.FindProperty("background").objectReferenceValue = background;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static GameObject Panel(TMP_FontAsset _font, ShopSlot _slotPrefab)
    {
        GameObject root = New("ShopPanel", null);
        Stretch(RT(root));
        ShopUI ui = root.AddComponent<ShopUI>();
        root.AddComponent<UI_SelfManagedPanel>();

        GameObject window = New("Window", root.transform);
        Stretch(RT(window));
        window.AddComponent<Image>().color = PanelColor;

        Txt("Title", window.transform, "商店", 40f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(600f, 60f), new Vector2(0f, -40f), _font, AccentColor);
        TextMeshProUGUI currency = Txt("Currency", window.transform, "金币：0", 28f, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(320f, 50f), new Vector2(-140f, -45f), _font, TextColor);

        Button close = Btn("CloseButton", window.transform, "×", 36f, new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-50f, -50f), _font);
        UnityEventTools.AddPersistentListener(close.onClick, ui.Close);

        // 左侧列表
        GameObject scrollGo = New("Scroll", window.transform);
        Stretch(RT(scrollGo), 70f, 70f, 120f, 240f);
        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        GameObject viewport = New("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);
        GameObject contentGo = New("Content", viewport.transform);
        RectTransform content = RT(contentGo);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(640f, 88f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperCenter;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        TextMeshProUGUI empty = Txt("EmptyHint", window.transform, "这个商店还没有上架商品。", 28f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(700f, 80f), Vector2.zero, _font, TextColor);
        empty.gameObject.SetActive(false);

        // 下方购买栏
        GameObject buyBar = New("BuyBar", window.transform);
        RectTransform barRect = RT(buyBar);
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 30f);
        barRect.sizeDelta = new Vector2(-140f, 170f);
        buyBar.AddComponent<Image>().color = new Color(0.09f, 0.1f, 0.14f, 1f);

        Image selectedIcon = Img("Icon", buyBar.transform, new Vector2(0f, 0.5f), new Vector2(96f, 96f), new Vector2(30f, 0f));
        TextMeshProUGUI selectedName = Txt("Name", buyBar.transform, "物品", 28f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(420f, 44f), new Vector2(150f, 34f), _font, TextColor);
        TextMeshProUGUI selectedPrice = Txt("Price", buyBar.transform, "单价 100", 24f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(420f, 40f), new Vector2(150f, -10f), _font, AccentColor);

        Button minus = Btn("MinusButton", buyBar.transform, "－", 32f, new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(-120f, 0f), _font);
        TextMeshProUGUI quantity = Txt("Quantity", buyBar.transform, "1", 32f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(120f, 60f), new Vector2(0f, 0f), _font, TextColor);
        Button plus = Btn("PlusButton", buyBar.transform, "＋", 32f, new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(120f, 0f), _font);

        minus.gameObject.AddComponent<ShopHoldButton>();
        SerializedObject minusHold = new SerializedObject(minus.gameObject.GetComponent<ShopHoldButton>());
        minusHold.FindProperty("direction").intValue = -1;
        minusHold.ApplyModifiedPropertiesWithoutUndo();

        plus.gameObject.AddComponent<ShopHoldButton>();
        SerializedObject plusHold = new SerializedObject(plus.gameObject.GetComponent<ShopHoldButton>());
        plusHold.FindProperty("direction").intValue = 1;
        plusHold.ApplyModifiedPropertiesWithoutUndo();

        Button buy = Btn("BuyButton", buyBar.transform, "购买", 28f, new Vector2(1f, 0.5f), new Vector2(220f, 80f), new Vector2(-40f, 0f), _font);

        SerializedObject serialized = new SerializedObject(ui);
        Wire(serialized, "windowRoot", window);
        Wire(serialized, "titleText", window.transform.Find("Title").GetComponent<TextMeshProUGUI>());
        Wire(serialized, "currencyText", currency);
        Wire(serialized, "listRoot", content);
        Wire(serialized, "slotPrefab", _slotPrefab);
        Wire(serialized, "emptyHint", empty.gameObject);
        Wire(serialized, "closeButton", close);
        Wire(serialized, "buyBarRoot", buyBar);
        Wire(serialized, "selectedIcon", selectedIcon);
        Wire(serialized, "selectedName", selectedName);
        Wire(serialized, "selectedPrice", selectedPrice);
        Wire(serialized, "quantityText", quantity);
        Wire(serialized, "minusButton", minus);
        Wire(serialized, "plusButton", plus);
        Wire(serialized, "buyButton", buy);
        Wire(serialized, "buyButtonLabel", buy.GetComponentInChildren<TextMeshProUGUI>());
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static void Wire(SerializedObject _serialized, string _field, Object _value)
    {
        SerializedProperty property = _serialized.FindProperty(_field);

        if (property != null)
            property.objectReferenceValue = _value;
    }

    #endregion

    #region 小工具

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Dialogue")) AssetDatabase.CreateFolder("Assets", "Dialogue");
    }

    private static TMP_FontAsset Font()
    {
        TMP_FontAsset fallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate == null) continue;
            if (candidate.HasCharacter('商')) return candidate;
            if (fallback == null && candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic && candidate.name.ToLowerInvariant().Contains("cjk")) fallback = candidate;
        }

        return fallback != null ? fallback : TMP_Settings.defaultFontAsset;
    }

    private static GameObject New(string _name, Transform _parent)
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

    private static Image Img(string _name, Transform _parent, Vector2 _anchor, Vector2 _size, Vector2 _offset)
    {
        GameObject go = New(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;
        SetRect(RT(go), _anchor, _size, _offset);
        return image;
    }

    private static TextMeshProUGUI Txt(string _name, Transform _parent, string _text, float _size, TextAlignmentOptions _align, Vector2 _anchor, Vector2 _rectSize, Vector2 _offset, TMP_FontAsset _font, Color _color)
    {
        GameObject go = New(_name, _parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = _text;
        text.fontSize = _size;
        text.alignment = _align;
        text.color = _color;
        text.raycastTarget = false;
        if (_font != null) text.font = _font;
        SetRect(RT(go), _anchor, _rectSize, _offset);
        return text;
    }

    private static Button Btn(string _name, Transform _parent, string _label, float _size, Vector2 _anchor, Vector2 _rectSize, Vector2 _offset, TMP_FontAsset _font)
    {
        GameObject go = New(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        SetRect(RT(go), _anchor, _rectSize, _offset);

        TextMeshProUGUI text = Txt("Label", go.transform, _label, _size, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), _rectSize, Vector2.zero, _font, TextColor);
        Stretch(text.rectTransform);

        return button;
    }

    #endregion
}
