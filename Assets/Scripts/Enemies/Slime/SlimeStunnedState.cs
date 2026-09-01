using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimeStunnedState : EnemyState
{
    private Enemy_Slime enemy;
    public SlimeStunnedState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Slime _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
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

        enemy.stats.MakeInvincible(false);
        enemy.fx.Invoke("CancelColorChange", 0);
    }

    public override void Update()
    {
        base.Update();

        if(rb.velocity.y < .1f && enemy.isGroundDetected())
        {
            enemy.fx.Invoke("CancelColorChange", 0);
            enemy.anim.SetTrigger("StunFold");
            enemy.stats.MakeInvincible(true);
        }

        if (stateTimer < 0)
            stateMachine.ChangState(enemy.idleState);
    }
}
