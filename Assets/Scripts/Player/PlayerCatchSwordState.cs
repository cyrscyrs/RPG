using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCatchSwordState : PlayerState
{
    public System.Action catchTheSword;

    private Transform sword;
    public PlayerCatchSwordState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.fx.PlayDustFx();
        player.fx.ScreenShake(player.fx.shake_catchSword);
        catchTheSword?.Invoke();

        if (player.skill.sword.sword == null) return;

        sword = player.skill.sword.sword.transform;

        if (sword.position.x > player.transform.position.x && player.facingDir == -1)
            player.Flip();
        else if (sword.position.x < player.transform.position.x && player.facingDir == 1)
            player.Flip();

        player.skill.sword.cooldownTimer = player.skill.sword.cooldown;
        rb.velocity = new Vector2(player.swordReturnImpact * -player.facingDir, rb.velocity.y);

    }

    public override void Exit()
    {
        base.Exit();

        player.StartCoroutine("BusyFor", .2f);
    }

    public override void Update()
    {
        base.Update();

        if (triggerCalled)
            stateMachine.ChangState(player.idleState);
    }
}
