using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadyIdleState : ShadyGrounedState
{
    public ShadyIdleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Shady _enemy) : base(_enemyBase, _stateMachine, _animBoolName, _enemy)
    {
    }

    // Start is called before the first frame update
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
