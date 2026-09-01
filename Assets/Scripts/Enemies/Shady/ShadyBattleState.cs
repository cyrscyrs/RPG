using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadyBattleState : EnemyState
{
    protected Enemy_Shady enemy;
    protected Transform player;
    protected int moveDir;
    protected float defaultSpeed;
    public ShadyBattleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Shady _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        defaultSpeed = enemy.moveSpeed;
        enemy.moveSpeed = enemy.battleStateMoveSpeed;

        player = PlayerManager.instance.player.transform;
        if (player.GetComponent<Player>().stats.isDead)
            stateMachine.ChangState(enemy.moveState);
    }
    public override void Update()
    {
        base.Update();

        if (enemy.IsPlayerDetected())
        {
            stateTimer = enemy.battleTime;

            if (enemy.IsPlayerDetected().distance < enemy.attackDistance)
            {
                //enemy.stateMachine.ChangState(enemy.deadState);
                enemy.stats.KillEntity();
            }
        }
        else if (stateTimer < 0)
            stateMachine.ChangState(enemy.idleState);

        if (player.position.x > enemy.transform.position.x)
            moveDir = 1;
        else if (player.position.x < enemy.transform.position.x)
            moveDir = -1;

        enemy.SetVelocity(enemy.moveSpeed * moveDir, rb.velocity.y);
    }

    public override void Exit()
    {
        base.Exit();

        enemy.moveSpeed = defaultSpeed;
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
