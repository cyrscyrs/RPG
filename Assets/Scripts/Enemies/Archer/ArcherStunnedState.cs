using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherStunnedState : EnemyState
{
    protected Enemy_Archer enemy;
    public ArcherStunnedState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Archer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }
    public override void Enter()
    {
        base.Enter();

        stateTimer = enemy.stunDuration;

        enemy.fx.InvokeRepeating("RedColorBlink", 0, .1f);

        rb.velocity = new Vector2(-enemy.facingDir * enemy.stunDirection.x, enemy.stunDirection.y);
    }

    public override void Exit()
    {
        base.Exit();

        enemy.fx.Invoke("CancelColorChange", 0);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangState(enemy.idleState);
    }
}
