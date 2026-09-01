using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWallSlideState : PlayerState
{
    public PlayerWallSlideState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.jumpTimes = 0;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (PlayerManager.instance.wallJumpUnlocked && player.jumpTimes < player.jumpLimit && Input.GetKeyDown(KeyCode.Space))
        {
            player.jumpTimes++;
            stateMachine.ChangState(player.wallJump);
            return;
        }

        if (xInput != 0 && xInput != player.facingDir)
            stateMachine.ChangState(player.idleState);

        if (yInput < 0)
            rb.velocity = new Vector2(0, rb.velocity.y);
        else
            rb.velocity = new Vector2(0, rb.velocity.y * 0.7f);

        if(!player.isWallDectected())
            stateMachine.ChangState(player.idleState);
        if(player.isGroundDetected())
            stateMachine.ChangState(player.idleState);
    }
}
