using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDeadState : PlayerState
{
    public PlayerDeadState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void AnimationFinishTrigger()
    {
        base.AnimationFinishTrigger();
    }

    public override void Enter()
    {
        base.Enter();
        GameObject.Find("Canvas").GetComponent<UI>().SwitchOnEndScreen();
        //DeadFadeOut(1.5f);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();
        player.SetZeroVelocity();
    }

    IEnumerator DeadFadeOut(float _delay)
    {
        GameObject.Find("Canvas").GetComponent<UI>().SwitchOnEndScreen();
        yield return new WaitForSeconds(_delay);
    }
}
