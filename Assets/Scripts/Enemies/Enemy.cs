using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EntityFX))]
[RequireComponent(typeof(ItemDrop))]

public class Enemy : Entity
{
    [SerializeField] protected LayerMask whatIsPlayer;

    [Header("Detect info")]
    [Tooltip("检测玩家用的圆投射半径。原来是零厚度的射线，玩家比检测线高/矮几厘米就打不到，怪物就会在战斗和待机之间来回横跳、永远打不到人")]
    [SerializeField] protected float playerDetectRadius = 0.35f;

    [Tooltip("检测距离")]
    [SerializeField] protected float playerDetectDistance = 10f;

    [Tooltip("上面那条打空时的兜底：玩家站在正面、且高度差不超过这个值，就算看到了")]
    [SerializeField] protected float playerDetectHeightTolerance = 1.2f;

    [Header("Stun info")]
    public float stunDuration = 1;
    public Vector2 stunDirection = new Vector2(10, 12);
    protected bool canBeStunned;
    [SerializeField] protected GameObject counterImage;



    [Header("Move info")]
    public float moveSpeed = 1.5f;
    public float idleTime = 2;
    public float battleTime = 7;
    private float defaultMoveSpeed;

    [Header("Attack info")]
    public float agroDistance = 2;
    public float attackDistance = 2;
    public float attackCoolDown;
    public float maxAttackCoolDown = 2;
    public float minAttackCoolDown = 1;
    [HideInInspector]public float lastAttackTime;


    public EnemyStateMachine stateMachine { get; private set; }
    public EntityFX fx { get; private set; }
    public string lastAnimBoolName { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new EnemyStateMachine();

        defaultMoveSpeed = moveSpeed;
    }

    protected override void Start()
    {
        base.Start();
        fx = GetComponent<EntityFX>();
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();
    }

    public override void DamegedEffect()
    {
        fx.StartCoroutine("FlashFX");
    }

    public virtual void AssignLastAnimName(string _animBoolName)
    {
        lastAnimBoolName = _animBoolName;
    }

    public override void SlowEntityBy(float _slowPercentage, float _slowDuration)
    {
        moveSpeed *= (1 - _slowPercentage);
        anim.speed = (1 - _slowPercentage);

        Invoke("ReturnDefaultSpeed", _slowDuration);
    }

    public override void ReturnDefaultSpeed()
    {
        base.ReturnDefaultSpeed();
        moveSpeed = defaultMoveSpeed;
    }

    public virtual void FreezeTime(bool _timeFreeze)
    {
        if (_timeFreeze)
        {
            moveSpeed = 0;
            anim.speed = 0;
        }
        else
        {
            moveSpeed = defaultMoveSpeed;
            anim.speed = 1;
        }
    }

    public virtual void FreezeTimeFor(float _duration) => StartCoroutine(FreezeTimerCoroutine(_duration));

    protected virtual IEnumerator FreezeTimerCoroutine(float _seconds)
    {
        FreezeTime(true);

        yield return new WaitForSeconds(_seconds);

        FreezeTime(false);
    }

    #region Counter Attack Window

    public virtual void OpenCounterAttackWindow()
    {
        canBeStunned = true;
        counterImage.SetActive(true);
    }

    public virtual void CloseCounterAttackWindow()
    {
        canBeStunned = false;
        counterImage.SetActive(false);
    }
    #endregion

    public virtual bool CanBeStunned()
    {
        if (canBeStunned)
        {
            CloseCounterAttackWindow();
            return true;
        }

        return false;
    }

    public virtual void AnimationFinishTrigger() => stateMachine.currentState.AnimationFinishTrigger();

    public virtual void AnimationSpecialAttackTrigger()
    {

    }

    /// <summary>
    /// 有没有看到玩家。
    /// 先从前方的 wallCheckUp 打一条有粗细的圆投射；打空的话再看玩家是不是就站在正面、高度也差不多。
    /// 加这层兜底是因为射线原来是一条零厚度的横线：玩家比它高/矮几厘米就会整个擦过去，
    /// 战斗状态又会立刻因为「没看到」退回待机，怪物就永远走不到攻击分支。
    /// </summary>
    public virtual RaycastHit2D IsPlayerDetected()
    {
        Vector2 origin = wallCheckUp.position;
        Vector2 direction = Vector2.right * facingDir;

        RaycastHit2D hit = Physics2D.CircleCast(origin, Mathf.Max(0.01f, playerDetectRadius), direction,
            playerDetectDistance, whatIsPlayer);

        if (hit.collider != null)
            return hit;

        Player player = PlayerManager.instance != null ? PlayerManager.instance.player : null;

        if (player == null || player.stats.isDead)
            return hit;

        Vector2 delta = (Vector2)player.transform.position - (Vector2)transform.position;

        // 必须在正面、水平距离够近、高度差不大（否则会隔着地板去打楼上的人）
        bool inFront = delta.x * facingDir >= -0.2f && Mathf.Abs(delta.x) <= agroDistance;
        bool sameLevel = Mathf.Abs(delta.y) <= playerDetectHeightTolerance;

        if (!inFront || !sameLevel)
            return hit;

        // 兜底：从检测点直接朝玩家身上再打一条（同样带粗细），打到就算看到。
        // 只有「在正面 + 够近 + 高度差不多」才会走到这里，所以不会隔着地板去打楼上的人。
        Vector2 toPlayer = (Vector2)player.transform.position - origin;

        return Physics2D.CircleCast(origin, Mathf.Max(0.01f, playerDetectRadius), toPlayer.normalized,
            toPlayer.magnitude, whatIsPlayer);
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, new Vector3(transform.position.x + attackDistance * facingDir, 
            transform.position.y));
    }
}
