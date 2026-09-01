using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PLayerPrimaryAttackState : PlayerState
{
    
    public int comboCounter { get; private set; }

    private float lastTimeAttacked;
    private float comboWindow = 2;
    public PLayerPrimaryAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        xInput = 0;

        //AudioManager.instance.PlaySFX(2);

        if (comboCounter > 2 || Time.time >= lastTimeAttacked + comboWindow)
            comboCounter = 0;

        player.anim.SetInteger("ComboCounter", comboCounter);

        stateTimer = 0.1f;

        float attackDir = player.facingDir;

        if (xInput != 0)
            attackDir = xInput;

        player.SetVelocity(player.attackMovement[comboCounter].x * attackDir,
            player.attackMovement[comboCounter].y);
    }

    public override void Exit()
    {
        base.Exit();

        player.StartCoroutine("BusyFor", 0.15f);

        comboCounter++;
        lastTimeAttacked= Time.time;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            player.SetZeroVelocity();

        if (triggerCalled)
            stateMachine.ChangState(player.idleState);
    }
}
