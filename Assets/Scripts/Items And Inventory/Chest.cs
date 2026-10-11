using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 宝箱。三个状态：关闭 / 打开中 / 打开。
///
/// - 玩家靠近「关闭」状态的宝箱，头顶才显示「按 W 互动」；已经打开的宝箱不再提示；
/// - 按 W → 播放「打开中」动画 → 动画最后一帧触发 <see cref="OnOpenAnimationFinished"/>（动画事件）
///   生成掉落物 → 动画放完进入「打开」状态；
/// - 掉落物列表有两份：
///   firstOpenDrops  只在第一次打开时掉（会写进存档，读档 / 重开场景都不会重复拿）；
///   everyOpenDrops  每次打开都掉。
///
/// 注意：动画事件只能发给挂着 Animator 的那个物体，所以本组件挂在宝箱的 Animator 子物体上
/// （层级是 /chest/Animator），不是最外层的 /chest。
/// </summary>
public class Chest : MonoBehaviour, ISaveManager
{
    public enum ChestState { Closed, Opening, Opened }

    [Header("标识")]
    [Tooltip("存档里区分不同宝箱用的 ID。留空 = 用「场景名/最外层物体名」自动生成，一般留空就行")]
    [SerializeField] private string chestId;

    [Header("掉落物")]
    [Tooltip("只在第一次打开宝箱时掉落（独有奖励）")]
    [SerializeField] private ItemData[] firstOpenDrops;

    [Tooltip("每次打开宝箱都会掉落（常规战利品）")]
    [SerializeField] private ItemData[] everyOpenDrops;

    [Tooltip("打开宝箱时生成的怪物（放敌人预制体）。和 everyOpenDrops 一样，每次打开都会生成")]
    [SerializeField] private GameObject[] spawnOnOpen;

    [Tooltip("生成怪物时在宝箱周围多大的范围内散开")]
    [SerializeField] private float spawnRadius = 1.2f;

    [Tooltip("掉落物预制体（带 ItemObject 的那个）")]
    [SerializeField] private GameObject dropPrefab;

    [Tooltip("一次最多掉几件，0 = 不限")]
    [SerializeField] private int dropLimit = 0;

    // 说明：这里不按 ItemData.dropChance 抽，那份概率是挂在物品上的（武器都是 0），
    // 照搬的话武器永远掉不出来。宝箱是「列表里写了就掉」，想随机就靠 dropLimit 从池子里抽。

    [Header("交互")]
    [Tooltip("玩家离多近才会出现「按 W 互动」")]
    [SerializeField] private float interactionRadius = 2.5f;

    [SerializeField] private KeyCode interactKey = KeyCode.W;

    [Tooltip("提示文字，{0} 会替换成按键名")]
    [SerializeField] private string promptFormat = "按 {0} 互动";

    [Tooltip("头顶的提示，留空会自动在下面找一个叫 Prompt 的子物体")]
    [SerializeField] private GameObject promptRoot;

    [Header("动画 / 音效")]
    [Tooltip("触发「打开中」的参数名（Trigger）")]
    [SerializeField] private string openTriggerName = "Open";

    [Tooltip("「打开」状态的名称，读档时直接跳到这个状态")]
    [SerializeField] private string openedStateName = "chest_open";

    [Tooltip("开箱音效在 AudioManager 里的下标，-1 = 不播")]
    [SerializeField] private int openSfxIndex = 22;

    [Header("存档")]
    [Tooltip("开箱后立刻写一次存档（一次性奖励建议开着）")]
    [SerializeField] private bool saveImmediately = true;

    public ChestState State { get; private set; } = ChestState.Closed;
    public bool IsOpened => State == ChestState.Opened;

    private Animator animator;

    // 以前开过（从存档读出来的），用来决定第一次奖励还给不给
    private bool openedBefore;

    private bool promptVisible;

    #region 生命周期

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (promptRoot == null)
        {
            Transform found = transform.Find("Prompt");

            if (found != null)
                promptRoot = found.gameObject;
        }

        SetPromptVisible(false);
    }

    private void Update()
    {
        if (State != ChestState.Closed)
        {
            SetPromptVisible(false);
            return;
        }

        // 菜单 / 对话暂停时不提示也不能开
        if (Time.timeScale <= 0f)
        {
            SetPromptVisible(false);
            return;
        }

        Player player = PlayerManager.instance != null ? PlayerManager.instance.player : null;

        if (player == null || player.stats.isDead)
        {
            SetPromptVisible(false);
            return;
        }

        bool inRange = Vector2.Distance(player.transform.position, transform.position) <= interactionRadius;

        SetPromptVisible(inRange);

        if (inRange && Input.GetKeyDown(interactKey))
            Open();
    }

    #endregion

    #region 开箱

    /// <summary>打开宝箱：播「打开中」动画，掉落物由动画最后一帧的动画事件生成。</summary>
    public void Open()
    {
        if (State != ChestState.Closed)
            return;

        State = ChestState.Opening;

        SetPromptVisible(false);

        if (animator != null && !string.IsNullOrEmpty(openTriggerName))
            animator.SetTrigger(openTriggerName);

        if (openSfxIndex >= 0 && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(openSfxIndex, transform);
    }

    /// <summary>
    /// 动画事件调用：动画放到最后一帧时生成掉落物，并把状态切到「打开」。
    /// 在 chest_opening 动画的最后一帧上挂这个事件即可。
    /// </summary>
    public void OnOpenAnimationFinished()
    {
        if (State == ChestState.Opened)
            return;

        SpawnDrops();

        SpawnMonsters();

        State = ChestState.Opened;

        if (saveImmediately && SaveManager.instance != null)
            SaveManager.instance.SaveGame();
    }

    /// <summary>在宝箱周围生成配置好的怪物（敌人预制体）。</summary>
    private void SpawnMonsters()
    {
        if (spawnOnOpen == null)
            return;

        foreach (GameObject prefab in spawnOnOpen)
        {
            if (prefab == null)
                continue;

            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, spawnRadius);
            Vector3 position = transform.position + new Vector3(offset.x, offset.y, 0f);

            Instantiate(prefab, position, Quaternion.identity);
        }
    }

    private void SpawnDrops()
    {
        if (dropPrefab == null)
        {
            Debug.LogWarning($"宝箱「{name}」没有配置掉落物预制体，这一箱不会掉东西。", this);
            return;
        }

        bool giveFirstTimeLoot = !openedBefore;
        openedBefore = true;

        List<ItemData> loot = new List<ItemData>();

        if (giveFirstTimeLoot)
            CollectDrops(firstOpenDrops, loot);

        CollectDrops(everyOpenDrops, loot);

        if (loot.Count <= 0)
            return;

        int count = dropLimit > 0 ? Mathf.Min(dropLimit, loot.Count) : loot.Count;

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, loot.Count);
            ItemData item = loot[index];

            loot.RemoveAt(index);

            DropOne(item);
        }
    }

    /// <summary>按 dropChance 逐条判定要掉哪些（和敌人掉落的规则一致）。</summary>
    private static void CollectDrops(ItemData[] _pool, List<ItemData> _result)
    {
        if (_pool == null)
            return;

        foreach (ItemData item in _pool)
        {
            if (item == null)
                continue;

            _result.Add(item);
        }
    }

    private void DropOne(ItemData _item)
    {
        GameObject newDrop = Instantiate(dropPrefab, transform.position, Quaternion.identity);

        Vector2 velocity = new Vector2(Random.Range(-5f, 5f), Random.Range(15f, 20f));

        ItemObject itemObject = newDrop.GetComponent<ItemObject>();

        if (itemObject != null)
            itemObject.SetupItem(_item, velocity);
    }

    #endregion

    #region 提示

    private void SetPromptVisible(bool _visible)
    {
        if (promptVisible == _visible)
            return;

        promptVisible = _visible;

        if (promptRoot == null)
            return;

        promptRoot.SetActive(_visible);

        if (_visible && promptFormat != null)
        {
            TMPro.TextMeshProUGUI label = promptRoot.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

            if (label != null)
                label.text = string.Format(promptFormat, interactKey.ToString());
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }

    #endregion

    #region 存档

    private string ResolveId()
    {
        if (!string.IsNullOrEmpty(chestId))
            return chestId;

        Transform root = transform.root != null ? transform.root : transform;
        Vector3 position = root.position;

        // 带上位置：场景里很可能有多个宝箱，名字重复（比如都叫 chests）时靠名字区分不出来
        return SceneManager.GetActiveScene().name + "/" + root.name
            + "@" + Mathf.RoundToInt(position.x * 10f) + "," + Mathf.RoundToInt(position.y * 10f);
    }

    public void LoadData(GameData _data)
    {
        if (_data == null || _data.openedChests == null)
            return;

        if (!_data.openedChests.TryGetValue(ResolveId(), out bool opened) || !opened)
            return;

        openedBefore = true;
        State = ChestState.Opened;

        // 直接把动画跳到「打开」的最后一帧
        if (animator != null && !string.IsNullOrEmpty(openedStateName))
            animator.Play(openedStateName, 0, 1f);

        SetPromptVisible(false);
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        // 没开过的宝箱不必占存档位置
        if (State != ChestState.Opened)
            return;

        if (_data.openedChests == null)
            _data.openedChests = new SerializableDictionary<string, bool>();

        _data.openedChests[ResolveId()] = true;
    }

    #endregion
}
