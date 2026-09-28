using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Entity
{
    [Header("Attack detil")]
    public Vector2[] attackMovement;
    public float counterAttackDuration = .2f;

    public bool isBusy { get; private set; }

    /// <summary>演出等场合被锁住操作时为 true。</summary>
    public bool controlLocked { get; private set; }

    [Header("Move info")]
    public float moveSpeed;
    public float jumpForce;
    public int jumpTimes;
    public int jumpLimit = 1;
    public float swordReturnImpact;
    private float defaultMoveSpeed;
    private float defaultJumpForce;

    [Header("Dash info")]
    public float dashSpeed;
    public float dashDuration;
    private float defaultDashSpeed;
    public float dashDir { get; private set; }

    [Header("移速加成（击杀连击）")]
    [Tooltip("当前生效的移速倍率，1 = 没有加成。由 KillStreakBuffManager 按连击阶段设置")]
    [SerializeField] private float speedBuffMultiplier = 1f;

    // 减速 debuff（寒冷之类）的倍率，独立于连击加成
    private float slowMultiplier = 1f;


    public SkillManager skill { get; private set; }
    //public GameObject sword;// { get; private set; }
    public PlayerFX fx { get; private set; }


    
    #region States

    public PlayerStateMachine stateMachine { get; private set; }
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }
    public PlayerWallSlideState wallSlide { get; private set; }
    public PlayerWallJumpState wallJump { get; private set; }
    public PlayerDashState dashState { get; private set; }

    public PLayerPrimaryAttackState primaryAttack { get; private set; }
    public PlayerCounterAttackState counterAttack { get; private set; }

    public PlayerAimSwordState aimSword { get; private set; }
    public PlayerCatchSwordState catchSword { get; private set; }
    public PlayerBlackholeState blackhole { get; private set; }
    public PlayerDeadState deadState { get; private set; }

    #endregion

    protected override void Awake()
    {
        base.Awake();

        stateMachine = new PlayerStateMachine();

        idleState = new PlayerIdleState(this, stateMachine,"Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Move");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");
        airState  = new PlayerAirState(this, stateMachine, "Jump");
        dashState = new PlayerDashState(this, stateMachine, "Dash");
        wallSlide = new PlayerWallSlideState(this, stateMachine, "WallSlide");
        wallJump  = new PlayerWallJumpState(this, stateMachine, "Jump");

        primaryAttack = new PLayerPrimaryAttackState(this, stateMachine, "Attack");
        counterAttack = new PlayerCounterAttackState(this, stateMachine, "CounterAttack");

        aimSword = new PlayerAimSwordState(this, stateMachine, "AimSword");
        catchSword = new PlayerCatchSwordState(this, stateMachine, "CatchSword");
        blackhole = new PlayerBlackholeState(this, stateMachine, "Jump");

        deadState = new PlayerDeadState(this, stateMachine, "Die");
    }

    protected override void Start()
    {
        base.Start();
        fx = GetComponent<PlayerFX>();

        skill = SkillManager.instance;
        stateMachine.Initialize(idleState);

        defaultMoveSpeed = moveSpeed;
        defaultJumpForce = jumpForce;
        defaultDashSpeed = dashSpeed;

        SetupJumpLimit(PlayerManager.instance.jumpTwiceUnlocked);
    }

    protected override void Update()
    {
        if (Time.timeScale == 0)
            return;

        base.Update();
        stateMachine.currentState.Update();
        CheckForDashInput();

        // 移动动画的速度要跟着当前的移速倍率走
        UpdateAnimSpeed();

        if (Input.GetKeyDown(KeyCode.F) && skill.crystal.crystalUnlocked)
            skill.crystal.CanUseSkill();

        if(Input.GetKeyDown(KeyCode.Alpha1))
            Inventory.instance.UseFlask();
    }

    public override void DamegedEffect()
    {
        fx.StartCoroutine("FlashFX");
    }

    public override void SlowEntityBy(float _slowPercentage, float _slowDuration)
    {
        slowMultiplier = Mathf.Clamp01(1f - _slowPercentage);
        ApplySpeedModifiers();

        CancelInvoke(nameof(RemoveSlow));
        Invoke(nameof(RemoveSlow), _slowDuration);
    }

    public override void ReturnDefaultSpeed()
    {
        base.ReturnDefaultSpeed();
        RemoveSlow();
    }

    private void RemoveSlow()
    {
        slowMultiplier = 1f;
        ApplySpeedModifiers();
    }

    /// <summary>当前生效的移速倍率（1 = 没加成，2 = 两倍速）。</summary>
    public float SpeedBuffMultiplier => speedBuffMultiplier;

    /// <summary>
    /// 锁/解锁玩家操作。Boss 入场演出用：锁住时直接停掉 Player 的 Update，
    /// 状态机就不再读输入，人也站在原地；解锁时从待机状态继续。
    /// </summary>
    public void SetControlLocked(bool _locked)
    {
        if (controlLocked == _locked)
            return;

        controlLocked = _locked;

        if (_locked)
        {
            SetZeroVelocity();

            if (stateMachine != null && stateMachine.currentState != null)
                stateMachine.ChangState(idleState);
        }

        enabled = !_locked;
    }

    /// <summary>
    /// 设置击杀连击给的移速倍率（1.33 / 1.66 / 2），
    /// 同时把移动动画的播放速度一起调快。
    /// </summary>
    public void SetSpeedBuffMultiplier(float _multiplier)
    {
        speedBuffMultiplier = Mathf.Max(1f, _multiplier);
        ApplySpeedModifiers();
    }

    /// <summary>把移速 / 跳跃 / 冲刺和动画速度按当前倍率重新算一遍。</summary>
    private void ApplySpeedModifiers()
    {
        if (defaultMoveSpeed <= 0f)
            return;   // Start 之前默认值还没存下来，先不动

        moveSpeed = defaultMoveSpeed * slowMultiplier * speedBuffMultiplier;

        // 跳跃和冲刺只受减速影响；连击加成只加移速
        jumpForce = defaultJumpForce * slowMultiplier;
        dashSpeed = defaultDashSpeed * slowMultiplier;

        UpdateAnimSpeed();
    }

    /// <summary>
    /// 移动动画的播放速度：走动时跟着移速倍率一起变快，其余动作保持原速。
    /// </summary>
    private void UpdateAnimSpeed()
    {
        if (anim == null || stateMachine == null || moveState == null)
            return;

        anim.speed = stateMachine.currentState == moveState
            ? slowMultiplier * speedBuffMultiplier
            : 1f;
    }

    /*
    public void AssignNewSword(GameObject _newSword)
    {
        sword = _newSword;
    }

    public void ClearSword()
    {
        Destroy(sword);
    }
    */

    public IEnumerator BusyFor(float _seconds)
    {
        isBusy = true;

        yield return new WaitForSeconds(_seconds);

        isBusy= false;
    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationFinishTrigger();

    private void CheckForDashInput()
    {
        //dashTimer -= Time.deltaTime;

        if (isWallDectected())
            return;

        if (!skill.dash.dashUnlocked)
            return;

        if (Input.GetKeyDown(KeyCode.LeftShift) && SkillManager.instance.dash.CanUseSkill())
        {
            //dashTimer = dashCoolDown;
            dashDir = Input.GetAxisRaw("Horizontal");

            if (dashDir == 0)
                dashDir = facingDir;

            stateMachine.ChangState(dashState);

        }
    }

    public override void Die()
    {
        base.Die();
        stateMachine.ChangState(deadState);
    }

    protected override void SetupZeroKnockbackPower()
    {
        knockbackPower = new Vector2(0, 0);
    }

    public void SetupJumpLimit(bool _jumpTwiceUnlocked) => jumpLimit = _jumpTwiceUnlocked ? 2 : 1;
}
