using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathBringerBattleState : EnemyState
{
    private Transform player;
    private Enemy_DeathBringer enemy;
    private int moveDir;
    public DeathBringerBattleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_DeathBringer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        player = PlayerManager.instance.player.transform;
        //if (player.GetComponent<Player>().stats.isDead)
        //    stateMachine.ChangState(enemy.moveState);
    }
    public override void Update()
    {
        base.Update();

        if (enemy.IsPlayerDetected())
        {
            stateTimer = enemy.battleTime;

            if (enemy.IsPlayerDetected().distance < enemy.attackDistance)
            {
                if (CanAttack())
                {
                    stateMachine.ChangState(enemy.attackState);
                }else
                    stateMachine.ChangState(enemy.idleState);
            }
        }
        //else if (stateTimer < 0)
        //    stateMachine.ChangState(enemy.idleState);

        if (player.position.x > enemy.transform.position.x)
            moveDir = 1;
        else if (player.position.x < enemy.transform.position.x)
            moveDir = -1;

        if (enemy.IsPlayerDetected() && enemy.IsPlayerDetected().distance < enemy.attackDistance - .5f)
        {
            stateMachine.ChangState(enemy.attackState);
            return;
        }

        enemy.SetVelocity(enemy.moveSpeed * moveDir, rb.velocity.y);
    }

    public override void Exit()
    {
        base.Exit();
    }

    private bool CanAttack()
    {
        if (Time.time >= enemy.lastAttackTime + enemy.attackCoolDown)
        {
            enemy.attackCoolDown = Random.Range(enemy.minAttackCoolDown, enemy.maxAttackCoolDown);
            enemy.lastAttackTime = Time.time;
            return true;
        }
        return false;
    }
}
