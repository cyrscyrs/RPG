using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathBringerIdleState : EnemyState
{
    private Enemy_DeathBringer enemy;
    private Transform player;
    public DeathBringerIdleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_DeathBringer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }
    public override void Enter()
    {
        base.Enter();

        player = PlayerManager.instance.player.transform;
        stateTimer = enemy.idleTime;
    }

    public override void Exit()
    {
        base.Exit();

    }

    public override void Update()
    {
        base.Update();

        if(Vector2.Distance(player.position,enemy.transform.position) < 7)
        {
            enemy.bossFightBegun = true;
            //stateMachine.ChangState(enemy.battleState);
        }

        //if (stateTimer < 0)
            //stateMachine.ChangState(enemy.moveState);
        if(Input.GetKeyUp(KeyCode.V)) {
            stateMachine.ChangState(enemy.teleportState);
        }

        if (stateTimer < 0 && enemy.bossFightBegun)
            stateMachine.ChangState(enemy.battleState);
    }
}
