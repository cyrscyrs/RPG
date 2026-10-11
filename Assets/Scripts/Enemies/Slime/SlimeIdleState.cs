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

        enemy.SetZeroVelocity();   // 待机就是站在原地，不然会带着上一状态的速度继续滑

        stateTimer = enemy.idleTime;
    }

    public override void Exit()
    {
        base.Exit();

        // 史莱姆待机声（38）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(38, enemy.transform);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangState(enemy.moveState);
    }
}
