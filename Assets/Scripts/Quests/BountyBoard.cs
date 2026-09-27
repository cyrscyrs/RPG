using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 赏金任务板：管理当前挂出来的任务、接取状态、进度、刷新与提交。
///
/// - 任务池 pool 里填所有候选任务，每次从里面随机抽 offerCount 个挂出来；
/// - 猎杀型进度由敌人死亡上报（EnemyStats → BountyBoard.NotifyKill）；
///   收集型进度直接看背包/仓库里有多少（提交时才扣）；
/// - 任务全部完成后自动免费刷新；也可以花金币手动刷新；
/// - 已接取 / 进度 / 已提交 / 当前挂出列表都写进存档。
/// </summary>
public class BountyBoard : MonoBehaviour, ISaveManager
{
    public static BountyBoard Instance;

    [Header("任务池")]
    [Tooltip("所有候选任务，每次随机抽一部分挂出来")]
    [SerializeField] private List<BountyQuest> pool = new List<BountyQuest>();

    [Tooltip("同时挂出几个任务")]
    [SerializeField] private int offerCount = 3;

    [Tooltip("手动刷新一次花多少金币")]
    [SerializeField] private int refreshCost = 100;

    [Tooltip("同时最多接几个任务")]
    [SerializeField] private int maxAccepted = 5;

    [Tooltip("升完级/提交后立刻写一次存档")]
    [SerializeField] private bool saveImmediately = true;

    private readonly List<BountyQuest> offers = new List<BountyQuest>();
    private readonly HashSet<string> accepted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> submitted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> progress = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BountyQuest> Offers => offers;
    public int MaxAccepted => Mathf.Max(1, maxAccepted);
    public int AcceptedCount => accepted.Count;
    public bool CanAcceptMore => accepted.Count < MaxAccepted;
    public int RefreshCost => Mathf.Max(0, refreshCost);

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>开局没有挂出任务时先抽一批。</summary>
    private void Start()
    {
        if (offers.Count == 0)
            RollNewOffers(false);
    }

    #endregion

    #region 击杀上报

    /// <summary>敌人死亡时由 EnemyStats 调用（和图鉴同一套 ID 规则）。</summary>
    public static void NotifyKill(string _monsterId)
    {
        if (Instance != null)
            Instance.AddKillProgress(_monsterId);
    }

    private void AddKillProgress(string _monsterId)
    {
        string id = MonsterBestiary.CleanName(_monsterId);

        if (string.IsNullOrEmpty(id))
            return;

        foreach (BountyQuest quest in offers)
        {
            if (quest == null || quest.objectiveType != BountyObjectiveType.Hunt)
                continue;

            if (!accepted.Contains(quest.GetId()))
                continue;

            if (MonsterBestiary.CleanName(quest.targetMonsterId) != id)
                continue;

            int current = GetProgress(quest);
            progress[quest.GetId()] = Mathf.Min(quest.requiredAmount, current + 1);
        }
    }

    #endregion

    #region 接取 / 进度 / 提交

    public bool IsAccepted(BountyQuest _quest) => _quest != null && accepted.Contains(_quest.GetId());

    public bool IsSubmitted(BountyQuest _quest) => _quest != null && submitted.Contains(_quest.GetId());

    /// <summary>接取任务。</summary>
    public bool Accept(BountyQuest _quest)
    {
        if (_quest == null || IsSubmitted(_quest))
            return false;

        if (!offers.Contains(_quest))
            return false;

        if (!accepted.Add(_quest.GetId()))
            return false;

        if (accepted.Count > MaxAccepted)
        {
            accepted.Remove(_quest.GetId());
            Debug.Log($"最多只能同时接 {MaxAccepted} 个任务");
            return false;
        }

        if (saveImmediately && SaveManager.instance != null)
            SaveManager.instance.SaveGame();

        return true;
    }

    public int GetProgress(BountyQuest _quest)
    {
        if (_quest == null)
            return 0;

        if (_quest.objectiveType == BountyObjectiveType.Collect)
            return GetCollectedAmount(_quest);

        return progress.TryGetValue(_quest.GetId(), out int value) ? value : 0;
    }

    private static int GetCollectedAmount(BountyQuest _quest)
    {
        if (_quest == null || _quest.targetItem == null || Inventory.instance == null)
            return 0;

        return Inventory.instance.GetItemAmount(_quest.targetItem);
    }

    /// <summary>进度够了就能提交。</summary>
    public bool CanSubmit(BountyQuest _quest) => IsAccepted(_quest) && !IsSubmitted(_quest) && GetProgress(_quest) >= Required(_quest);

    private static int Required(BountyQuest _quest) => Mathf.Max(1, _quest.requiredAmount);

    /// <summary>提交任务：收集型扣物品，然后发奖励。</summary>
    public bool Submit(BountyQuest _quest)
    {
        if (!CanSubmit(_quest))
            return false;

        // 收集型要扣掉对应数量
        if (_quest.objectiveType == BountyObjectiveType.Collect && _quest.targetItem != null)
        {
            for (int i = 0; i < Required(_quest); i++)
                Inventory.instance.RemoveItem(_quest.targetItem);
        }

        submitted.Add(_quest.GetId());
        accepted.Remove(_quest.GetId());

        GrantRewards(_quest);

        // 全部提交完 → 自动免费刷新
        if (AllOffersCompleted())
            RollNewOffers(true);

        if (saveImmediately && SaveManager.instance != null)
            SaveManager.instance.SaveGame();

        return true;
    }

    private void GrantRewards(BountyQuest _quest)
    {
        if (_quest.rewards == null)
            return;

        foreach (BountyReward reward in _quest.rewards)
        {
            if (reward == null)
                continue;

            if (reward.currency > 0 && PlayerManager.instance != null)
                PlayerManager.instance.currency += reward.currency;

            if (reward.item != null && reward.amount > 0 && Inventory.instance != null)
            {
                for (int i = 0; i < reward.amount; i++)
                    Inventory.instance.AddItem(reward.item);
            }
        }
    }

    /// <summary>挂出来的任务是不是都提交完了。</summary>
    public bool AllOffersCompleted()
    {
        if (offers.Count == 0)
            return false;

        foreach (BountyQuest quest in offers)
        {
            if (quest != null && !submitted.Contains(quest.GetId()))
                return false;
        }

        return true;
    }

    #endregion

    #region 刷新任务列表

    /// <summary>花金币刷新一批新任务。</summary>
    public bool RefreshWithCurrency()
    {
        if (CanRefreshFree())
            return RollNewOffers(true);

        if (PlayerManager.instance == null)
            return false;

        if (PlayerManager.instance.GetCurrency() < RefreshCost)
            return false;

        if (!PlayerManager.instance.HaveEnoughCurrency(RefreshCost))
            return false;

        return RollNewOffers(false);
    }

    /// <summary>所有任务都完成了就免费刷新。</summary>
    public bool CanRefreshFree() => AllOffersCompleted();

    private bool RollNewOffers(bool _free)
    {
        offers.Clear();

        if (pool != null && pool.Count > 0)
        {
            List<BountyQuest> candidates = new List<BountyQuest>();

            foreach (BountyQuest quest in pool)
            {
                if (quest != null && !submitted.Contains(quest.GetId()))
                    candidates.Add(quest);
            }

            int count = Mathf.Clamp(offerCount, 1, Mathf.Max(1, candidates.Count));

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                offers.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        if (saveImmediately && SaveManager.instance != null)
            SaveManager.instance.SaveGame();

        return true;
    }

    #endregion

    #region 存档

    public void LoadData(GameData _data)
    {
        offers.Clear();
        accepted.Clear();
        submitted.Clear();
        progress.Clear();

        if (_data == null)
            return;

        if (_data.bountyOffers != null)
        {
            foreach (string id in _data.bountyOffers)
            {
                BountyQuest quest = FindInPool(id);

                if (quest != null)
                    offers.Add(quest);
            }
        }

        if (_data.bountyAccepted != null)
        {
            foreach (KeyValuePair<string, bool> pair in _data.bountyAccepted)
                if (pair.Value) accepted.Add(pair.Key);
        }

        if (_data.bountySubmitted != null)
        {
            foreach (KeyValuePair<string, bool> pair in _data.bountySubmitted)
                if (pair.Value) submitted.Add(pair.Key);
        }

        if (_data.bountyProgress != null)
        {
            foreach (KeyValuePair<string, int> pair in _data.bountyProgress)
                progress[pair.Key] = pair.Value;
        }
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        if (_data.bountyOffers == null) _data.bountyOffers = new List<string>();
        if (_data.bountyAccepted == null) _data.bountyAccepted = new SerializableDictionary<string, bool>();
        if (_data.bountySubmitted == null) _data.bountySubmitted = new SerializableDictionary<string, bool>();
        if (_data.bountyProgress == null) _data.bountyProgress = new SerializableDictionary<string, int>();

        _data.bountyOffers.Clear();
        foreach (BountyQuest quest in offers)
            if (quest != null) _data.bountyOffers.Add(quest.GetId());

        _data.bountyAccepted.Clear();
        foreach (string id in accepted) _data.bountyAccepted[id] = true;

        _data.bountySubmitted.Clear();
        foreach (string id in submitted) _data.bountySubmitted[id] = true;

        _data.bountyProgress.Clear();
        foreach (KeyValuePair<string, int> pair in progress) _data.bountyProgress[pair.Key] = pair.Value;
    }

    private BountyQuest FindInPool(string _id)
    {
        if (pool == null || string.IsNullOrEmpty(_id))
            return null;

        foreach (BountyQuest quest in pool)
        {
            if (quest != null && string.Equals(quest.GetId(), _id, System.StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    #endregion
}
