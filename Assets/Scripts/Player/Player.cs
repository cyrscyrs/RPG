using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Entity
{
    [Header("Attack detil")]
    public Vector2[] attackMovement;
    public float counterAttackDuration = .2f;

    public bool isBusy { get; private set; }

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
        moveSpeed *= (1 - _slowPercentage);
        jumpForce *= (1 - _slowPercentage);
        dashSpeed *= (1 - _slowPercentage);
        anim.speed = (1 - _slowPercentage);

        Invoke("ReturnDefaultSpeed", _slowDuration);
    }

    public override void ReturnDefaultSpeed()
    {
        base.ReturnDefaultSpeed();

        moveSpeed = defaultMoveSpeed;
        jumpForce = defaultJumpForce;
        dashSpeed = defaultDashSpeed;
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
