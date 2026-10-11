using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Boss 战流程控制：
/// 1. 玩家靠近 Boss → 锁住玩家操作，镜头切到 Boss；
/// 2. Boss 演一遍攻击（播攻击动画）；
/// 3. 屏幕中央浮现 Boss 名字，停留后慢慢淡出；
/// 4. 镜头回到玩家，解锁操作，屏幕下方的 Boss 血条展开，正式开打；
/// 5. Boss 阵亡 → 全场慢放一段时间，再恢复正常。
///
/// 挂在场景里的 /BossFight 上，引用由 BossSceneBuilder 自动接好。
/// </summary>
public class BossFightController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Enemy_DeathBringer boss;
    [SerializeField] private Player player;

    [Tooltip("演出时用的摄像机（优先级比玩家那台高），平时关着")]
    [SerializeField] private CinemachineVirtualCamera bossCamera;

    [Tooltip("演出时镜头对准的点，会一直贴着 Boss 的视觉中心")]
    [SerializeField] private Transform bossFocusPoint;

    [SerializeField] private BossHealthBarUI healthBar;

    [Tooltip("屏幕中央浮现的 Boss 名字")]
    [SerializeField] private CanvasGroup titleGroup;

    [Header("触发")]
    [Tooltip("玩家和 Boss 的距离小于这个值就自动开始演出")]
    [SerializeField] private float triggerDistance = 8f;

    [Tooltip("镜头对准点的额外偏移，用来对准 Boss 的身体中心")]
    [SerializeField] private Vector2 focusPointOffset = new Vector2(-2.17f, 0.97f);

    [Header("演出音效")]
    [Tooltip("演出开始时 Boss 的吼声（AudioManager 的 sfx 下标，21 = sfx_mosnter_growl_1；-1 = 不播）")]
    [SerializeField] private int introRoarSfxIndex = 21;

    [Tooltip("Boss 战 BGM（AudioManager 的 bgm 下标。3 = BGM_nemesis）。演出一开始就起，不等演出结束")]
    [SerializeField] private int bossBgmIndex = 3;

    [Header("演出节奏（秒）")]
    [SerializeField] private float cameraBlendTime = 0.8f;
    [Tooltip("Boss 亮攻击的时长，攻击动画本身 0.92 秒")]
    [SerializeField] private float attackShowcaseTime = 1.8f;
    [SerializeField] private float titleFadeInTime = 0.6f;
    [SerializeField] private float titleHoldTime = 1.1f;
    [SerializeField] private float titleFadeOutTime = 1.4f;

    [Header("阵亡慢放")]
    [SerializeField] private float deathSlowTimeScale = 0.25f;
    [Tooltip("用真实时间算，不受慢放影响")]
    [SerializeField] private float deathSlowDuration = 2.5f;

    [Tooltip("慢放结束后 BGM 淡出到停止的时长（秒）。0 = 立刻停。只改这条 BGM 的音量，不动全局音量设置")]
    [SerializeField] private float deathBgmFadeDuration = 3f;

    private EnemyStats bossStats;
    private bool introPlayed;
    private bool deathHandled;

    /// <summary>演出结束、正式开打了没有。</summary>
    public bool FightStarted { get; private set; }

    /// <summary>Boss 的属性（血量 / 死亡状态），升降墙之类的脚本要用它判断时机。</summary>
    public EnemyStats BossStats => bossStats;

    private void Awake()
    {
        bossStats = boss != null ? boss.GetComponent<EnemyStats>() : null;

        if (healthBar != null)
            healthBar.HideImmediate();

        if (titleGroup != null)
            titleGroup.alpha = 0f;

        if (bossCamera != null)
            bossCamera.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (boss == null || player == null || bossStats == null)
            return;

        // 镜头对准点跟着 Boss 的视觉中心（Boss 会翻转，所以不用子物体）
        if (bossFocusPoint != null)
        {
            Vector3 bossPosition = boss.transform.position;
            bossFocusPoint.position = new Vector3(
                bossPosition.x + focusPointOffset.x,
                bossPosition.y + focusPointOffset.y,
                bossPosition.z);
        }

        if (!introPlayed && !bossStats.isDead && !player.stats.isDead &&
            Vector2.Distance(player.transform.position, boss.transform.position) <= triggerDistance)
        {
            StartCoroutine(IntroRoutine());
            return;
        }

        // 只看死没死：哪怕没触发演出（比如隔远了打死的）也要慢放
        if (!deathHandled && bossStats.isDead)
        {
            deathHandled = true;
            StartCoroutine(DeathRoutine());
        }
    }

    private void OnDisable()
    {
        // 演出中途场景被切走/重载时别把时间缩放留在慢放上
        if (Time.timeScale != 1f)
            Time.timeScale = 1f;

        if (player != null)
            player.SetControlLocked(false);
    }

    #region 入场演出

    private IEnumerator IntroRoutine()
    {
        introPlayed = true;

        // 演出一开始就把 Boss 战 BGM 起起来（以前整段演出是没音乐的），同时让 Boss 吼一声
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayBGM(bossBgmIndex);

            if (introRoarSfxIndex >= 0)
                AudioManager.instance.PlaySFX(introRoarSfxIndex, boss != null ? boss.transform : null);
        }

        player.SetControlLocked(true);

        // 先冻住 Boss，免得镜头还在往过推的时候它已经追着玩家跑了
        boss.enabled = false;
        boss.SetZeroVelocity();
        boss.stateMachine.ChangState(boss.idleState);

        if (bossCamera != null)
            bossCamera.gameObject.SetActive(true);

        yield return new WaitForSeconds(cameraBlendTime);

        // 亮一手攻击
        boss.stateMachine.ChangState(boss.attackState);

        yield return new WaitForSeconds(attackShowcaseTime);

        boss.stateMachine.ChangState(boss.idleState);

        yield return new WaitForSeconds(0.2f);

        yield return TitleRoutine();

        if (bossCamera != null)
            bossCamera.gameObject.SetActive(false);

        yield return new WaitForSeconds(cameraBlendTime);

        // 开打
        boss.bossFightBegun = true;
        boss.enabled = true;
        boss.stateMachine.ChangState(boss.battleState);

        if (healthBar != null)
            healthBar.Show();

        player.SetControlLocked(false);

        FightStarted = true;
    }

    private IEnumerator TitleRoutine()
    {
        if (titleGroup == null)
            yield break;

        yield return FadeGroup(titleGroup, 1f, titleFadeInTime);
        yield return new WaitForSeconds(titleHoldTime);
        yield return FadeGroup(titleGroup, 0f, titleFadeOutTime);
    }

    private static IEnumerator FadeGroup(CanvasGroup _group, float _target, float _duration)
    {
        float start = _group.alpha;
        float time = 0f;

        while (time < _duration)
        {
            time += Time.unscaledDeltaTime;

            _group.alpha = Mathf.Lerp(start, _target, Mathf.Clamp01(time / _duration));

            yield return null;
        }

        _group.alpha = _target;
    }

    #endregion

    #region 阵亡慢放

    private IEnumerator DeathRoutine()
    {
        float originalScale = Time.timeScale;

        Time.timeScale = deathSlowTimeScale;

        // 慢放本身要按真实时间计时，否则会越等越久
        yield return new WaitForSecondsRealtime(deathSlowDuration);

        Time.timeScale = originalScale;

        // 慢放结束：BGM 溢淡出到静音后停止。
        // 只降这一条 BGM 自己的音量，不动全局音量设置（Options 面板那套走 AudioMixer，不受影响）。
        if (AudioManager.instance != null)
            AudioManager.instance.FadeOutBGM(deathBgmFadeDuration);

        if (healthBar != null)
            healthBar.Hide();
    }

    #endregion
}
