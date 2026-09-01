using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAirState : PlayerState
{
    public PlayerAirState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        if (player.isGroundDetected())
            stateMachine.ChangState(player.idleState);

        if (player.isWallDectected())
            stateMachine.ChangState(player.wallSlide);

        if (xInput != 0)
            player.SetVelocity(player.moveSpeed * 0.8f * xInput, rb.velocity.y);
        

        if (player.jumpTimes < player.jumpLimit && Input.GetKeyDown(KeyCode.Space))
        {
            player.jumpTimes += 2;
            stateMachine.ChangState(player.jumpState);
        }
    }
}
