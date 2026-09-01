using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimeIdleState : EnemyState
{
    protected Enemy_Slime enemy;
    public SlimeIdleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Slime _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemy.idleTime;
    }

    public override void Exit()
    {
        base.Exit();

        //AudioManager.instance.PlaySFX(24, enemy.transform);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangState(enemy.moveState);
    }
}
