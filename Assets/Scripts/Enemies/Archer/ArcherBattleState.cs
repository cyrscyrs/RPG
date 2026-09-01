using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherBattleState : EnemyState
{
    protected int moveDir;
    protected Transform player;
    protected Enemy_Archer enemy;
    public ArcherBattleState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Archer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }


    public override void Enter()
    {
        base.Enter();

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

            if(enemy.IsPlayerDetected().distance < enemy.safeDistance) 
            {
                if(CanJump())
                    stateMachine.ChangState(enemy.jumpState);
            }

            if (enemy.IsPlayerDetected().distance < enemy.attackDistance)
            {
                if (CanAttack())
                    stateMachine.ChangState(enemy.attackState);
            }
        }
        else if (stateTimer < 0)
            stateMachine.ChangState(enemy.idleState);

        if (player.position.x > enemy.transform.position.x && enemy.facingDir != 1)
            enemy.Flip();
        else if (player.position.x < enemy.transform.position.x && enemy.facingDir != -1)
            //moveDir = -1;
            enemy.Flip();

        //enemy.SetVelocity(rb.velocity.x * moveDir, rb.velocity.y);

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

    private bool CanJump()
    {
        if(enemy.GroundBehindCheck() == false || enemy.WallBehindCheck() == true)
            return false;

        if(Time.time >= enemy.lastTimeJumped + enemy.jumpCooldown)
        {
            enemy.lastTimeJumped = Time.time;
            return true;
        }
        return false;
    }
}
