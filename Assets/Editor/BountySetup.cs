using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>赏金猎人相关资源的一键生成：任务界面预制体、示例任务、对话资产、场景 NPC 配置。</summary>
public static class BountySetup
{
    private const string PanelFolder = "Assets/Prefabs/UI";
    private const string PanelPath = PanelFolder + "/BountyPanel.prefab";
    private const string SlotPath = PanelFolder + "/BountyQuestSlot.prefab";
    private const string QuestFolder = "Assets/Bounty";
    private const string DialoguePath = "Assets/Dialogue/Bounty_Welcome.asset";

    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    private static readonly Color SlotColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.72f, 0.4f, 1f);
    private static readonly Color ButtonColor = new Color(0.24f, 0.28f, 0.36f, 1f);

    [MenuItem("Tools/赏金/生成任务界面预制体")]
    private static void BuildPrefabs()
    {
        EnsureFolders();
        TMP_FontAsset font = Font();

        GameObject slotRoot = BuildSlot(font);
        GameObject slotAsset = PrefabUtility.SaveAsPrefabAsset(slotRoot, SlotPath);
        Object.DestroyImmediate(slotRoot);

        GameObject panelRoot = BuildPanel(font, slotAsset.GetComponent<BountyQuestSlot>());
        PrefabUtility.SaveAsPrefabAsset(panelRoot, PanelPath);
        Object.DestroyImmediate(panelRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"赏金：任务界面已生成。\n{PanelPath}\n{SlotPath}");
    }

    [MenuItem("Tools/赏金/生成示例任务")]
    private static void BuildQuests()
    {
        EnsureFolders();

        CreateQuest("hunt_skeleton_5", BountyObjectiveType.Hunt, "骷髅悬赏", "skeleton", null, 5, 300,
            "铁匠说最近墓地的骷髅太猖狂了，去清理几只。", "骨头可以卖钱，顺便赚点外快。");
        CreateQuest("hunt_slime_8", BountyObjectiveType.Hunt, "史莱姆清理", "slime", null, 8, 250,
            "路上的史莱姆越来越多，清掉一批。", "它们身上的黏液是好材料。");
        CreateQuest("hunt_archer_3", BountyObjectiveType.Hunt, "弓手威胁", "archer", null, 3, 400,
            "林子里的弓手老是偷袭旅人。", "注意别被射到。");
        CreateQuest("collect_iron_10", BountyObjectiveType.Collect, "收购铁矿", null, "Iron", 10, 200,
            "收 10 块铁矿，铁匠要用。", "矿石在哪都能挖到。");
        CreateQuest("collect_skin_6", BountyObjectiveType.Collect, "收购兽皮", null, "Animal skin", 6, 180,
            "收 6 张兽皮。", "怪物身上就能剥到。");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"赏金：示例任务已生成到 {QuestFolder}");
    }

    [MenuItem("Tools/赏金/生成赏金猎人对话")]
    private static void BuildDialogue()
    {
        EnsureFolders();

        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DialoguePath);
        bool created = dialogue == null;

        if (created)
        {
            dialogue = ScriptableObject.CreateInstance<NpcDialogue>();
            dialogue.dialogueId = "bounty_welcome";
            AssetDatabase.CreateAsset(dialogue, DialoguePath);
        }

        dialogue.onceOnly = false;
        dialogue.hideChosenChoices = false;
        dialogue.nodes = new[]
        {
            new DialogueNode { nodeId = "n1", speaker = "", text = "想赚点外快吗，随时可以来找我", nextNodeId = "n2", choices = new DialogueChoice[0] },
            new DialogueNode
            {
                nodeId = "n2", speaker = "", text = "", nextNodeId = "",
                choices = new[]
                {
                    new DialogueChoice { choiceId = "take", label = "接取任务", targetNodeId = "", returnToChoices = false, actionId = BountyBoardUI.OpenActionId },
                    new DialogueChoice { choiceId = "leave", label = "离开", targetNodeId = "", returnToChoices = false }
                }
            }
        };

        EditorUtility.SetDirty(dialogue);
        AssetDatabase.SaveAssets();
        Debug.Log($"赏金：对话已生成/更新 {DialoguePath}");
    }

    [MenuItem("Tools/赏金/给场景里的赏金猎人配好")]
    private static void SetupScene()
    {
        GameObject npcObject = Selection.activeGameObject;

        if (npcObject == null)
        {
            foreach (string name in new[] { "NPC_BountyHunter", "BountyHunter", "赏金猎人", "NPC_Hunter" })
            {
                npcObject = GameObject.Find(name);
                if (npcObject != null) break;
            }
        }

        if (npcObject == null)
        {
            EditorUtility.DisplayDialog("赏金", "先在 Hierarchy 里选中赏金猎人那个物体，再执行这个菜单。", "好");
            return;
        }

        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DialoguePath);

        if (dialogue == null)
        {
            EditorUtility.DisplayDialog("赏金", "先生成对话（Tools/赏金/生成赏金猎人对话）。", "好");
            return;
        }

        DialogueNpc npc = npcObject.GetComponent<DialogueNpc>() ?? Undo.AddComponent<DialogueNpc>(npcObject);

        SerializedObject npcSerialized = new SerializedObject(npc);
        npcSerialized.FindProperty("npcName").stringValue = "赏金猎人";
        npcSerialized.FindProperty("interactionRadius").floatValue = 2.5f;
        npcSerialized.FindProperty("dialogues").arraySize = 0;
        npcSerialized.FindProperty("defaultDialogue").objectReferenceValue = dialogue;
        npcSerialized.ApplyModifiedProperties();

        // 任务板挂在常驻物体上（优先 Inventory 所在的物体）
        BountyBoard board = Object.FindObjectOfType<BountyBoard>();

        if (board == null)
        {
            Inventory inventory = Object.FindObjectOfType<Inventory>();
            GameObject host = inventory != null ? inventory.gameObject : npcObject;
            board = Undo.AddComponent<BountyBoard>(host);
            Debug.Log($"赏金：已在「{host.name}」上加上 BountyBoard。");
        }

        SerializedObject boardSerialized = new SerializedObject(board);
        SerializedProperty pool = boardSerialized.FindProperty("pool");
        pool.arraySize = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:BountyQuest", new[] { QuestFolder }))
        {
            BountyQuest quest = AssetDatabase.LoadAssetAtPath<BountyQuest>(AssetDatabase.GUIDToAssetPath(guid));

            if (quest == null) continue;

            pool.InsertArrayElementAtIndex(pool.arraySize);
            pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = quest;
        }

        boardSerialized.FindProperty("offerCount").intValue = 3;
        boardSerialized.FindProperty("refreshCost").intValue = 100;
        boardSerialized.FindProperty("maxAccepted").intValue = 5;
        boardSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(npcObject);
        Selection.activeGameObject = npcObject;

        Debug.Log($"赏金：「{npcObject.name}」已配好（对话 + 任务板 + {pool.arraySize} 个任务），记得保存场景，并把 BountyPanel 预制体拖进 Canvas。");
    }

    #region 任务与工具

    private static void CreateQuest(string _fileName, BountyObjectiveType _type, string _title, string _monsterId, string _itemPath,
                                    int _amount, int _currency, string _objective, string _content)
    {
        string path = $"{QuestFolder}/{_fileName}.asset";

        if (AssetDatabase.LoadAssetAtPath<BountyQuest>(path) != null)
            return;

        BountyQuest quest = ScriptableObject.CreateInstance<BountyQuest>();
        quest.questId = _fileName;
        quest.objectiveType = _type;
        quest.title = _title;
        quest.targetMonsterId = _monsterId;
        quest.targetItem = string.IsNullOrEmpty(_itemPath) ? null : AssetDatabase.LoadAssetAtPath<ItemData>(_itemPath);
        quest.requiredAmount = _amount;
        quest.objective = _type == BountyObjectiveType.Hunt
            ? $"击杀 {_amount} 只目标怪物"
            : $"收集 {_amount} 个 {(quest.targetItem != null ? quest.targetItem.itemName : "？")}";
        quest.content = _content;
        quest.rewards = new[]
        {
            new BountyReward { currency = _currency },
            new BountyReward { item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/Materials/Iron.asset"), amount = 2 }
        };

        AssetDatabase.CreateAsset(quest, path);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PanelFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Dialogue")) AssetDatabase.CreateFolder("Assets", "Dialogue");
        if (!AssetDatabase.IsValidFolder(QuestFolder)) AssetDatabase.CreateFolder("Assets", "Bounty");
    }

    private static TMP_FontAsset Font()
    {
        TMP_FontAsset fallback = null;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate == null) continue;
            if (candidate.HasCharacter('赏')) return candidate;
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

    private static TextMeshProUGUI Txt(string _name, Transform _parent, string _text, float _size, TextAlignmentOptions _align, Color _color, TMP_FontAsset _font)
    {
        GameObject go = New(_name, _parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = _text;
        text.fontSize = _size;
        text.alignment = _align;
        text.color = _color;
        text.raycastTarget = false;
        if (_font != null) text.font = _font;
        return text;
    }

    private static Button Btn(string _name, Transform _parent, string _label, float _size, TMP_FontAsset _font)
    {
        GameObject go = New(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = Txt("Label", go.transform, _label, _size, TextAlignmentOptions.Center, TextColor, _font);
        Stretch(text.rectTransform, 6f, 6f, 2f, 2f);

        return button;
    }

    #endregion

    #region 预制体搭建

    private static GameObject BuildSlot(TMP_FontAsset _font)
    {
        GameObject root = New("BountyQuestSlot", null);
        RT(root).sizeDelta = new Vector2(900f, 150f);

        Image background = root.AddComponent<Image>();
        background.color = SlotColor;
        root.AddComponent<LayoutElement>().preferredHeight = 150f;

        TextMeshProUGUI title = Txt("Title", root.transform, "任务名", 28f, TextAlignmentOptions.Left, AccentColor, _font);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 36f), new Vector2(20f, -14f));

        TextMeshProUGUI info = Txt("Info", root.transform, "【猎杀】击杀 5 只骷髅", 22f, TextAlignmentOptions.TopLeft, TextColor, _font);
        SetRect(info.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 70f), new Vector2(20f, -52f));

        TextMeshProUGUI progress = Txt("Progress", root.transform, "进度：0 / 5", 22f, TextAlignmentOptions.Left, TextColor, _font);
        SetRect(progress.rectTransform, new Vector2(0f, 0f), new Vector2(300f, 30f), new Vector2(20f, 30f));

        TextMeshProUGUI reward = Txt("Reward", root.transform, "奖励：300 金币", 22f, TextAlignmentOptions.Left, AccentColor, _font);
        SetRect(reward.rectTransform, new Vector2(0f, 0f), new Vector2(420f, 30f), new Vector2(20f, 6f));

        Button action = Btn("ActionButton", root.transform, "接取任务", 24f, _font);
        SetRect(RT(action.gameObject), new Vector2(1f, 0.5f), new Vector2(200f, 64f), new Vector2(-24f, 0f));

        BountyQuestSlot slot = root.AddComponent<BountyQuestSlot>();
        SerializedObject serialized = new SerializedObject(slot);
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("infoText").objectReferenceValue = info;
        serialized.FindProperty("progressText").objectReferenceValue = progress;
        serialized.FindProperty("rewardText").objectReferenceValue = reward;
        serialized.FindProperty("actionButton").objectReferenceValue = action;
        serialized.FindProperty("actionLabel").objectReferenceValue = action.GetComponentInChildren<TextMeshProUGUI>();
        serialized.FindProperty("background").objectReferenceValue = background;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject BuildPanel(TMP_FontAsset _font, BountyQuestSlot _slotPrefab)
    {
        GameObject root = New("BountyPanel", null);
        Stretch(RT(root));
        BountyBoardUI ui = root.AddComponent<BountyBoardUI>();
        root.AddComponent<UI_SelfManagedPanel>();

        GameObject window = New("Window", root.transform);
        Stretch(RT(window));
        window.AddComponent<Image>().color = PanelColor;

        TextMeshProUGUI title = Txt("Title", window.transform, "赏金任务", 40f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 60f), new Vector2(0f, -40f));

        TextMeshProUGUI currency = Txt("Currency", window.transform, "金币：0", 26f, TextAlignmentOptions.Right, TextColor, _font);
        SetRect(currency.rectTransform, new Vector2(1f, 1f), new Vector2(400f, 40f), new Vector2(-150f, -50f));

        Button close = Btn("CloseButton", window.transform, "×", 36f, _font);
        SetRect(RT(close.gameObject), new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-50f, -50f));
        UnityEventTools.AddPersistentListener(close.onClick, ui.Close);

        // 列表
        GameObject scrollGo = New("Scroll", window.transform);
        Stretch(RT(scrollGo), 70f, 70f, 120f, 150f);
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
        grid.cellSize = new Vector2(900f, 150f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperCenter;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        TextMeshProUGUI empty = Txt("EmptyHint", window.transform, "现在没有任务。", 26f, TextAlignmentOptions.Center, TextColor, _font);
        SetRect(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700f, 60f), Vector2.zero);
        empty.gameObject.SetActive(false);

        Button refresh = Btn("RefreshButton", window.transform, "刷新（100 金币）", 26f, _font);
        SetRect(RT(refresh.gameObject), new Vector2(0.5f, 0f), new Vector2(340f, 72f), new Vector2(0f, 40f));

        // 奖励弹窗
        GameObject rewardPopup = New("RewardPopup", window.transform);
        Stretch(RT(rewardPopup));
        rewardPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        GameObject rewardBox = New("Box", rewardPopup.transform);
        SetRect(RT(rewardBox), new Vector2(0.5f, 0.5f), new Vector2(620f, 320f), Vector2.zero);
        rewardBox.AddComponent<Image>().color = new Color(0.09f, 0.1f, 0.14f, 1f);

        TextMeshProUGUI rewardText = Txt("Text", rewardBox.transform, "任务完成！", 28f, TextAlignmentOptions.Center, AccentColor, _font);
        SetRect(rewardText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560f, 200f), new Vector2(0f, 20f));

        Button rewardClose = Btn("Close", rewardBox.transform, "好", 26f, _font);
        SetRect(RT(rewardClose.gameObject), new Vector2(0.5f, 0f), new Vector2(200f, 64f), new Vector2(0f, 30f));

        rewardPopup.SetActive(false);

        SerializedObject serialized = new SerializedObject(ui);
        Wire(serialized, "windowRoot", window);
        Wire(serialized, "titleText", title);
        Wire(serialized, "currencyText", currency);
        Wire(serialized, "listRoot", content);
        Wire(serialized, "slotPrefab", _slotPrefab);
        Wire(serialized, "emptyHint", empty.gameObject);
        Wire(serialized, "closeButton", close);
        Wire(serialized, "refreshButton", refresh);
        Wire(serialized, "refreshLabel", refresh.GetComponentInChildren<TextMeshProUGUI>());
        Wire(serialized, "rewardPopup", rewardPopup);
        Wire(serialized, "rewardText", rewardText);
        Wire(serialized, "rewardCloseButton", rewardClose);
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
}
