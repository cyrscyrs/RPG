using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连续杀敌加速（击杀连击）。
///
/// 规则（数值都能在 Inspector 里改）：
/// - 10 秒内击杀 3 只怪物 → 第一阶段，移速 +33%；
/// - 同一个窗口里累计到 6 只 → 第二阶段，移速 +66%；
/// - 同一个窗口里累计到 9 只 → 第三阶段，移速 +100%；
/// - 三个阶段不叠加，任何时候只保留最高的那一档（取最大值）；
/// - 加成持续 10 秒，10 秒内只要再击杀怪物就把持续时间刷新回满；
/// - 整整 10 秒没有击杀，加成结束、连击清零，要重新从 3 只开始攒。
///
/// 敌人死亡时由 <see cref="EnemyStats"/> 调用 <see cref="NotifyKill"/> 上报，
/// 玩家头顶的图标由 <see cref="KillStreakBuffUI"/> 显示。
/// 场景里没有手动摆这个组件时，会在场景加载后自动建一个，所以不加也不影响。
/// </summary>
public class KillStreakBuffManager : MonoBehaviour
{
    public static KillStreakBuffManager instance;

    /// <summary>预制体在 Resources 下的路径（不带扩展名）。</summary>
    public const string DefaultManagerPath = "KillStreak/KillStreakBuffManager";
    public const string DefaultBuffUIPath = "KillStreak/KillStreakBuffUI";

    [Header("阶段条件")]
    [Tooltip("每一阶段需要的击杀数（统计窗口内累计）。默认 3 / 6 / 9")]
    [SerializeField] private int[] killRequirements = { 3, 6, 9 };

    [Tooltip("每一阶段对应的移速加成，0.33 就是 +33%。默认 33% / 66% / 100%")]
    [SerializeField] private float[] speedBonuses = { 0.33f, 0.66f, 1f };

    [Header("时间")]
    [Tooltip("击杀窗口：这段时间里杀掉的怪物才算连击")]
    [SerializeField] private float killWindow = 10f;

    [Tooltip("加成持续时间；生效期间再击杀会把这个时间刷新回满")]
    [SerializeField] private float buffDuration = 10f;

    [Header("表现")]
    [Tooltip("玩家头顶的 Buff 图标。留空会在运行时自动创建")]
    [SerializeField] private KillStreakBuffUI buffUI;

    [Tooltip("头顶图标的预制体。场景里没摆实例时用这个实例化，留空就去 Resources/KillStreak 下找")]
    [SerializeField] private KillStreakBuffUI buffUIPrefab;

    [Tooltip("阶段提升 / 到期时在玩家头顶弹一句提示")]
    [SerializeField] private bool showPopUpText = true;

    /// <summary>阶段变化时触发，参数是新的阶段（0 = 加成结束）。</summary>
    public System.Action<int> OnStageChanged;

    private readonly List<float> killTimes = new List<float>();

    // 不受暂停影响的自有时间：菜单打开（Time.timeScale = 0）时连击计时也停住
    private float clock;
    private int stage;
    private float buffTimeLeft;

    private Player player;

    #region 对外只读状态

    /// <summary>当前阶段：0 = 没有加成，1 / 2 / 3 = 对应档位。</summary>
    public int Stage => stage;

    /// <summary>现在是否正带着移速加成。</summary>
    public bool IsBuffActive => stage > 0 && buffTimeLeft > 0f;

    /// <summary>加成还剩多少秒。</summary>
    public float BuffTimeLeft => Mathf.Max(0f, buffTimeLeft);

    /// <summary>加成的总持续时间。</summary>
    public float BuffDuration => Mathf.Max(0.01f, buffDuration);

    /// <summary>剩余时间比例，给时间条用。</summary>
    public float BuffNormalized => IsBuffActive ? Mathf.Clamp01(buffTimeLeft / BuffDuration) : 0f;

    /// <summary>当前阶段的移速加成（0.33 / 0.66 / 1）。</summary>
    public float CurrentSpeedBonus
    {
        get
        {
            int index = stage - 1;

            if (speedBonuses == null || index < 0 || index >= speedBonuses.Length)
                return 0f;

            return speedBonuses[index];
        }
    }

    /// <summary>窗口内（最近 killWindow 秒）已经攒了多少只。</summary>
    public int KillsInWindow => killTimes.Count;

    #endregion

    #region 生命周期

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>场景里没摆这个组件时自动补一个，保证机制一定跑得起来。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        if (FindObjectOfType<KillStreakBuffManager>(true) != null)
            return;

        // 优先用 Resources 下的预制体，这样在 Inspector 里调好的数值和接好的 UI 才会生效
        GameObject prefab = Resources.Load<GameObject>(DefaultManagerPath);

        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        new GameObject("KillStreakBuffManager").AddComponent<KillStreakBuffManager>();
    }

    private void Start()
    {
        EnsureBuffUI();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (dt > 0f)
            clock += dt;

        if (player == null && PlayerManager.instance != null)
            player = PlayerManager.instance.player;

        PruneKills();

        if (stage <= 0)
            return;

        // 玩家死了就别再挂着加速
        if (player != null && player.stats != null && player.stats.isDead)
        {
            EndStreak(false);
            return;
        }

        if (dt > 0f)
            buffTimeLeft -= dt;

        // 10 秒内没有再击杀 → 连击断掉
        if (buffTimeLeft <= 0f)
            EndStreak();
    }

    #endregion

    #region 击杀上报

    /// <summary>敌人死亡时由 EnemyStats 调用。</summary>
    public static void NotifyKill(string _monsterId = null)
    {
        if (instance == null)
            instance = FindObjectOfType<KillStreakBuffManager>(true);

        if (instance != null)
            instance.RegisterKill();
    }

    /// <summary>记一次击杀，重新算阶段并刷新持续时间。</summary>
    public void RegisterKill()
    {
        killTimes.Add(clock);
        PruneKills();

        int reached = StageForKillCount(killTimes.Count);

        // 只保留最高的那一档，不叠加
        int newStage = Mathf.Max(stage, reached);

        // 只要在窗口里击杀，持续时间就刷新回满
        buffTimeLeft = BuffDuration;

        SetStage(newStage);
    }

    #endregion

    #region 阶段切换

    private void SetStage(int _stage)
    {
        _stage = Mathf.Max(0, _stage);

        if (_stage == stage)
            return;

        stage = _stage;

        ApplyToPlayer();
        EnsureBuffUI();

        OnStageChanged?.Invoke(stage);

        if (showPopUpText)
            ShowStageText(stage);
    }

    /// <summary>连击断掉：清空积攒的击杀，加成归零。</summary>
    public void EndStreak() => EndStreak(true);

    private void EndStreak(bool _showText)
    {
        killTimes.Clear();
        buffTimeLeft = 0f;

        bool hadBuff = stage > 0;

        if (hadBuff)
        {
            stage = 0;
            ApplyToPlayer();
            OnStageChanged?.Invoke(0);
        }

        if (_showText && hadBuff)
            ShowStageText(0);
    }

    /// <summary>清空连击状态（读档、复活之类的场合可以调）。</summary>
    public void ResetStreak() => EndStreak(false);

    private int StageForKillCount(int _count)
    {
        int reached = 0;

        if (killRequirements == null)
            return reached;

        for (int i = 0; i < killRequirements.Length; i++)
        {
            if (killRequirements[i] > 0 && _count >= killRequirements[i])
                reached = i + 1;
        }

        return reached;
    }

    private void PruneKills()
    {
        while (killTimes.Count > 0 && clock - killTimes[0] > killWindow)
            killTimes.RemoveAt(0);
    }

    private void ApplyToPlayer()
    {
        if (player == null && PlayerManager.instance != null)
            player = PlayerManager.instance.player;

        if (player == null)
            return;

        // 1 + 加成 = 移速倍率（1 / 1.33 / 1.66 / 2）
        player.SetSpeedBuffMultiplier(1f + CurrentSpeedBonus);
    }

    private void ShowStageText(int _current)
    {
        if (player == null || player.fx == null)
            return;

        if (_current == 0)
        {
            player.fx.CreatePopUpText("连击中断");
            return;
        }

        int percent = Mathf.RoundToInt(CurrentSpeedBonus * 100f);
        int nextIndex = _current;   // speedBonuses 是 0 基的，下一档正好是这个下标

        bool hasNext = speedBonuses != null && nextIndex < speedBonuses.Length;

        string text = $"连击 {_current} 阶！移速 +{percent}%";

        if (hasNext && killRequirements != null && nextIndex < killRequirements.Length)
            text += $"（再杀 {Mathf.Max(1, killRequirements[nextIndex] - KillsInWindow)} 只进下一阶）";

        player.fx.CreatePopUpText(text);
    }

    private void EnsureBuffUI()
    {
        if (buffUI == null)
            buffUI = FindObjectOfType<KillStreakBuffUI>(true);

        if (buffUI == null && buffUIPrefab != null)
            buffUI = Instantiate(buffUIPrefab);

        if (buffUI == null)
            buffUI = InstantiateDefaultBuffUI();

        if (buffUI == null)
            return;

        buffUI.SetManager(this);
    }

    /// <summary>
    /// 场景里没摆实例时的兜底：先用 Resources/KillStreak 下的预制体，这样直接改预制体就能看到效果；
    /// 连预制体都没有才现搭一个，保证机制不会因为缺资源而失效。
    /// </summary>
    private KillStreakBuffUI InstantiateDefaultBuffUI()
    {
        GameObject prefab = Resources.Load<GameObject>(DefaultBuffUIPath);

        if (prefab != null)
        {
            KillStreakBuffUI instance = Instantiate(prefab).GetComponent<KillStreakBuffUI>();

            if (instance != null)
                return instance;
        }

        GameObject go = new GameObject("KillStreakBuffUI", typeof(RectTransform), typeof(KillStreakBuffUI));

        return go.GetComponent<KillStreakBuffUI>();
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>编辑器里改完数值方便立刻看效果。</summary>
    private void OnValidate()
    {
        if (killWindow <= 0f) killWindow = 10f;
        if (buffDuration <= 0f) buffDuration = 10f;
    }
#endif
}
