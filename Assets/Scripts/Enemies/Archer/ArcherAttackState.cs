using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherAttackState : EnemyState
{
    protected Enemy_Archer enemy;
    public ArcherAttackState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Archer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();
        enemy.SetZeroVelocity();
    }

    public override void Exit()
    {
        base.Exit();

        enemy.lastAttackTime = Time.time;
    }

    public override void Update()
    {
        base.Update();

        enemy.SetZeroVelocity();

        if (triggerCalled)
            stateMachine.ChangState(enemy.battleState);

    }
}
