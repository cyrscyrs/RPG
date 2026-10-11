using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWallJumpState : PlayerState
{
    public PlayerWallJumpState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = 0.3f;
        player.SetVelocity(player.moveSpeed * -player.facingDir, player.jumpForce);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        /*if (stateTimer < 0)
            stateMachine.ChangState(player.idleState);*/
        if (player.isWallDectected())
            stateMachine.ChangState(player.wallSlide);

        if (player.isGroundDetected())
        {
            player.PlayLandingSFX();
            stateMachine.ChangState(player.idleState);
        }
        if (stateTimer <= 0 && xInput != 0)
            player.SetVelocity(player.moveSpeed * xInput, rb.velocity.y);

        if(player.jumpTimes < player.jumpLimit && Input.GetKeyDown(KeyCode.Space))
        {
            player.jumpTimes++;
            stateMachine.ChangState(player.jumpState);
        }
    }
}
