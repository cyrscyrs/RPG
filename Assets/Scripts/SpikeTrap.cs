using System.Collections;
using UnityEngine;

/// <summary>
/// 地刺陷阱：玩家踩到或碰到就会受到伤害；站在上面不动时会每隔 damageInterval 秒再结算一次。
///
/// 实现要点：
/// - OnTriggerEnter2D 负责「踩到」的第一下伤害；
/// - 持续伤害在 Update 里按自己的计时器结算，而不是用 OnTriggerStay2D：
///   玩家站着不动时刚体会进入休眠，休眠期间物理回调不一定还会派发，
///   用自己的接触状态 + 计时器就不受这个问题影响。
/// - 冲刺等无敌状态（PlayerStats.isInvincible）不会掉血，也不占用间隔，
///   无敌一结束还站在刺上就会立刻受伤。
/// - 伤害走 PlayerStats.TakeDamage()，所以护甲、无敌帧、死亡、掉魂、重生这些原有流程都会自动生效。
///
/// 摆放方式：
/// 1. 在刺的位置新建一个空物体，挂上本脚本 + BoxCollider2D（勾上 Is Trigger）
///    + SpriteRenderer 摆上 spikes 的图，然后把 Collider 调到刚好盖住刺尖。
///    ⚠ 不要直接挂在 Tilemap 上：Tilemap 的 Collider 是所有格子共用的一个组件，
///      没法只给某一块格子单独加陷阱逻辑。
/// 2. 挂在墙边的刺要把 BoxCollider2D 贴住墙面，别伸出到玩家正常走动的位置，不然会凭空掉血。
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    [Header("伤害")]
    [Tooltip("每次造成的固定伤害")]
    [SerializeField] private int damage = 10;
    [Tooltip("勾上后改用「玩家最大生命值的百分比」计算伤害，适合让陷阱在游戏后期依然有威胁")]
    [SerializeField] private bool useMaxHealthPercent = false;
    [Tooltip("最大生命值百分比（0.1 = 10%）")]
    [Range(0f, 1f)]
    [SerializeField] private float maxHealthPercent = 0.1f;

    [Header("重复受伤")]
    [Tooltip("站在刺上不动时，是否每隔一段时间再受伤一次")]
    [SerializeField] private bool damageWhileStanding = true;
    [Tooltip("两次受伤之间的最小间隔（秒）。踩上去后 0.8 秒内又踩回来也不会重复扣血")]
    [SerializeField] private float damageInterval = 0.8f;

    [Header("表现（可选，不填也能正常用）")]
    [Tooltip("受伤时在陷阱位置生成的特效预制体")]
    [SerializeField] private GameObject hitFXPrefab;
    [Tooltip("受伤音效在 AudioManager 里的下标，-1 表示不播放")]
    [SerializeField] private int hitSFXIndex = -1;
    [Tooltip("特效物体的自动销毁时间（秒）")]
    [SerializeField] private float fxLifetime = 2f;

    /// <summary>每次真正扣到血时触发，参数是玩家和实际伤害，可以用来做镜头抖动、UI 提示之类。</summary>
    public System.Action<PlayerStats, int> onPlayerDamaged;

    // 记「玩家还在刺上」的接触状态：用计数是因为玩家可能有多个 Collider 先后进入触发器
    private PlayerStats playerInRange;
    private int overlapCount;
    private float nextDamageTime;

    /// <summary>在编辑器里第一次挂上这个脚本时，自动把 Collider 设成 Trigger。</summary>
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
            Debug.LogWarning($"SpikeTrap「{name}」所在物体上没有 Collider2D，陷阱不会触发。" +
                             "请加一个 BoxCollider2D 并勾上 Is Trigger。", this);
        else if (!col.isTrigger)
            Debug.LogWarning($"SpikeTrap「{name}」的 Collider2D 没有勾 Is Trigger，玩家会被挡住而不是踩上去受伤。", this);
    }

    private void OnTriggerEnter2D(Collider2D _collision)
    {
        PlayerStats playerStats = _collision.GetComponent<PlayerStats>();

        if (playerStats == null)
            return;

        overlapCount++;
        playerInRange = playerStats;

        TryDamagePlayer(playerStats);   // 踩到就立刻结算一次
    }

    private void OnTriggerExit2D(Collider2D _collision)
    {
        if (_collision.GetComponent<PlayerStats>() == null)
            return;

        overlapCount = Mathf.Max(0, overlapCount - 1);

        if (overlapCount == 0)
            playerInRange = null;
    }

    private void Update()
    {
        if (!damageWhileStanding || playerInRange == null)
            return;

        // 玩家已死（或对象已销毁）就不再继续结算
        if (playerInRange.isDead)
        {
            playerInRange = null;
            return;
        }

        TryDamagePlayer(playerInRange);
    }

    private void TryDamagePlayer(PlayerStats _playerStats)
    {
        if (Time.time < nextDamageTime)
            return;

        if (_playerStats == null || _playerStats.isDead)
            return;

        // 冲刺无敌帧：这次不算，也不占用间隔，无敌结束还站在上面会正常受伤
        if (_playerStats.isInvincible)
            return;

        int finalDamage = GetDamage(_playerStats);

        if (finalDamage <= 0)
            return;

        nextDamageTime = Time.time + damageInterval;

        // 扣血、受击闪光、死亡、掉魂都在 PlayerStats / Player 里处理好了
        _playerStats.TakeDamage(finalDamage);

        PlayHitFeedback();

        onPlayerDamaged?.Invoke(_playerStats, finalDamage);
    }

    private int GetDamage(PlayerStats _playerStats)
    {
        if (useMaxHealthPercent)
            return Mathf.RoundToInt(_playerStats.GetMaxHealthValue() * maxHealthPercent);

        return damage;
    }

    private void PlayHitFeedback()
    {
        if (hitSFXIndex >= 0 && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(hitSFXIndex, transform);

        if (hitFXPrefab == null)
            return;

        GameObject fx = Instantiate(hitFXPrefab, transform.position, Quaternion.identity);

        // 编辑器里（非播放模式）没有延迟销毁，直接删掉，避免报错
        if (Application.isPlaying)
            Destroy(fx, fxLifetime);
        else
            DestroyImmediate(fx);
    }
}
