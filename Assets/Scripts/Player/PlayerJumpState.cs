using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        /*
        if (rb.velocity.x * xInput < 0)
        {
            Debug.Log("x= "+ rb.velocity.x + "xInput= "+xInput);
            player.Flip();
        }
        //rb.velocity = new Vector2(rb.velocity.x, player.jumpForce);
        int turn = 1;
        if ((rb.velocity.x > 0 && xInput < 0) || (rb.velocity.x < 0 && xInput > 0))
            turn = -1;*/
        player.SetVelocity(rb.velocity.x, player.jumpForce);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {

        base.Update();

        if (xInput != 0)
            player.SetVelocity(player.moveSpeed * 0.8f * xInput, rb.velocity.y);

        if (player.jumpTimes < player.jumpLimit && Input.GetKeyDown(KeyCode.Space))
        {
            player.jumpTimes++;
            stateMachine.ChangState(player.jumpState);
        }

        if (rb.velocity.y < 0)
            stateMachine.ChangState(player.airState);
    }
}
