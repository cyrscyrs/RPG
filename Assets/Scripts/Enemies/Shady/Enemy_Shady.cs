using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class Enemy_Shady : Enemy
{
    [Header("Shady specifics")]
    public float battleStateMoveSpeed;

    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float growSpeed;
    [SerializeField] private float maxSize;

    #region  States

    public ShadyIdleState idleState { get; private set; }
    public ShadyMoveState moveState { get; private set; }
    public ShadyBattleState battleState { get; private set; }
    public ShadyStunnedState stunnedState { get; private set; }
    public ShadyDeadState deadState { get; private set; }

    #endregion

    protected override void Awake()
    {
        base.Awake();

        idleState = new ShadyIdleState(this, stateMachine, "Idle", this);
        moveState = new ShadyMoveState(this, stateMachine, "Move", this);
        battleState = new ShadyBattleState(this, stateMachine, "MoveFast", this);
        stunnedState = new ShadyStunnedState(this, stateMachine, "Stunned", this);
        deadState = new ShadyDeadState(this, stateMachine, "Dead", this);
    }

    protected override void Start()
    {
        base.Start();

        stateMachine.Initialize(idleState);
    }

    public override bool CanBeStunned()
    {
        if (base.CanBeStunned())
        {
            stateMachine.ChangState(stunnedState);
            return true;
        }

        return false;
    }

    public override void Die()
    {
        base.Die();
        stateMachine.ChangState(deadState);
    }

    public override void AnimationSpecialAttackTrigger()
    {
        GameObject newExplsion = Instantiate(explosionPrefab,transform.position, Quaternion.identity);

        newExplsion.GetComponent<Explosion_Controller>().SetupExplosion(stats, growSpeed, maxSize, attackCheckRadius);
    }

    public void SelfDestroy() => Destroy(gameObject);
}
