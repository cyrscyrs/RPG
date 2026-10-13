using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_DeathBringer : Enemy
{
    #region States

    public DeathBringerAttackState attackState { get; private set; }
    public DeathBringerBattleState battleState { get; private set; }
    public DeathBringerDeadState deadState { get; private set; }
    public DeathBringerIdleState idleState { get; private set; }
    public DeathBringerSpellCastState spellCastState { get; private set; }
    public DeathBringerTeleportState teleportState { get; private set; }

    #endregion

    public bool bossFightBegun;

    [Header("Spell cast details")]
    [SerializeField] private GameObject spellPrefab;
    public int amountOfSpells;
    public float spellCooldown;
    public float lastTimeCast;
    [SerializeField] private float spellStateCooldown;
    [SerializeField] private Vector2 spellOffset;

    [Header("Spell summons")]
    [Tooltip("放法术时在法术的位置随机召唤一只怪：从这里随机挑一个预制体（空着就不召唤）")]
    [SerializeField] private GameObject[] summonPrefabs;

    [Tooltip("同时存在的召唤物上限，0 = 不限。法术是循环放的，不限的话场地很快就满了")]
    [SerializeField] private int maxSummonsAlive = 6;

    private readonly List<EnemyStats> summons = new List<EnemyStats>();

    [Header("Teleport details")]
    [SerializeField] private BoxCollider2D arena;
    [SerializeField] private Vector2 surroundingCheckSize;
    public float chanceToTeleport;
    public float defaultChanceToTeleport = 25;

    protected override void Awake()
    {
        base.Awake();

        SetupDefaultFacingDir(-1);

        attackState = new DeathBringerAttackState(this, stateMachine, "Attack", this);
        battleState = new DeathBringerBattleState(this, stateMachine, "Move", this);
        deadState = new DeathBringerDeadState(this, stateMachine, "Idle", this);
        idleState = new DeathBringerIdleState(this, stateMachine, "Idle", this);
        spellCastState = new DeathBringerSpellCastState(this, stateMachine, "SpellCast", this);
        teleportState = new DeathBringerTeleportState(this, stateMachine, "Teleport", this);
    }

    protected override void Start()
    {
        base.Start();

        stateMachine.Initialize(idleState);

        // 攻击命中后自己还有后续动作（附负面效果 + 回血），挂在属性表的回调上：
        // 近战命中、法术命中最后都走 DoDamage，所以这一处就能盖住 Boss 全部攻击
        if (stats != null)
            stats.onAttackResolved += OnAttackResolved;
    }

    public override void Die()
    {
        base.Die();
        stateMachine.ChangState(deadState);
    }

    public void CastSpell()
    {
        Player player = PlayerManager.instance.player;

        float xOffset = 0;

        if (player.rb.velocity.x != 0)
            xOffset = spellOffset.x * player.facingDir;

        Vector3 spellPosition = new Vector3(player.transform.position.x + xOffset, player.transform.position.y + spellOffset.y);

        GameObject newSpell = Instantiate(spellPrefab, spellPosition, Quaternion.identity);
        newSpell.GetComponent<DeathBringerSpell_Controller>().SetupSpell(stats);

        // 法术生成（41）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(41, newSpell.transform);

        // 在法术落点的位置随机召唤一只怪
        SummonRandomMonsterAt(spellPosition);
    }

    /// <summary>在指定位置随机召唤一只怪，等级由调参表（GameTuningConfig.bossSummonLevel）决定。</summary>
    private void SummonRandomMonsterAt(Vector3 _position)
    {
        if (summonPrefabs == null || summonPrefabs.Length == 0)
            return;

        // 先把已经死掉 / 被销毁的召唤物从名单里剔掉
        summons.RemoveAll(summon => summon == null || summon.isDead);

        if (maxSummonsAlive > 0 && summons.Count >= maxSummonsAlive)
            return;

        GameObject prefab = summonPrefabs[Random.Range(0, summonPrefabs.Length)];

        if (prefab == null)
            return;

        GameObject summon = Instantiate(prefab, _position, Quaternion.identity);
        EnemyStats summonStats = summon.GetComponentInChildren<EnemyStats>(true);

        if (summonStats == null)
            return;

        // 等级加成是在 EnemyStats.Start 里算的：Instantiate 只跑了 Awake，
        // 所以这里设完等级，本帧稍后 Start 就会按新等级算属性
        summonStats.SetLevel(Tuning.Config.bossSummonLevel);
        summonStats.suppressDrops = true;   // 召唤出来的怪死了不掉东西
        summons.Add(summonStats);
    }

    /// <summary>
    /// 自己的攻击结算完时回调（_hit = 有没有真的打中，被闪避就是 false）。
    /// 只针对打中玩家的那一击：给玩家挂负面效果，同时给自己回血。
    /// </summary>
    private void OnAttackResolved(CharacterStats _target, bool _hit)
    {
        if (!_hit || _target == null || _target.isDead)
            return;

        if (!(_target is PlayerStats))
            return;

        GameTuningConfig tuning = Tuning.Config;

        // 1) 负面效果：防御 -30% / 闪避 -50% / 回复 -50%，持续 3 秒（同一个效果只刷新不叠加）
        _target.ApplyAttackDebuff(tuning.bossAttackDebuffDuration,
            tuning.bossAttackArmorReduction,
            tuning.bossAttackEvasionReduction,
            tuning.bossAttackHealReduction);

        // 2) 自己回血：100 + 已损失生命的 5%
        if (stats == null || stats.isDead)
            return;

        int lostHealth = Mathf.Max(0, stats.GetMaxHealthValue() - stats.currentHealth);
        int healAmount = Mathf.RoundToInt(tuning.bossHitHealBase
            + lostHealth * tuning.bossHitHealLostHealthPercent);

        if (healAmount > 0)
            stats.IncreaseHP_By(healAmount);
    }

    // Unity 的消息方法（不是虚函数，只能同签名声明）
    private void OnDestroy()
    {
        if (stats != null)
            stats.onAttackResolved -= OnAttackResolved;
    }

    public void FindPosition()
    {
        float x = Random.Range(arena.bounds.min.x + 3, arena.bounds.max.x - 3);
        float y = Random.Range(arena.bounds.min.y + 3, arena.bounds.max.y - 3);

        transform.position = new Vector3(x, y);
        //为了传送后能站在地面上
        transform.position = new Vector3(transform.position.x, transform.position.y - GroundBelow().distance + (cd.size.y / 2));

        if(!GroundBelow() || SomethingIsAround())
        {
            FindPosition();
        }
    }

    //防止传送到非地面
    private RaycastHit2D GroundBelow() => Physics2D.Raycast(transform.position, Vector2.down, 100, whatIsGround);
    //防止传送到墙里
    private bool SomethingIsAround() => Physics2D.BoxCast(transform.position, surroundingCheckSize, 0, Vector2.zero, 0, whatIsGround);

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.DrawLine(transform.position, new Vector2(transform.position.x,transform.position.y - GroundBelow().distance));
        Gizmos.DrawWireCube(transform.position, surroundingCheckSize);
    }

    public bool CanTeleport()
    {
        if (Random.Range(0, 100) < chanceToTeleport)
        {
            chanceToTeleport = defaultChanceToTeleport;
            return true;
        }

        return false;
    }

    public bool CanSpellCast()
    {
        if (Time.time >= lastTimeCast + spellStateCooldown)
        {
            return true;
        }
        return false;
    }
}
