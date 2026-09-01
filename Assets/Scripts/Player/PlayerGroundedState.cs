using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGroundedState : PlayerState
{
    
    public PlayerGroundedState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
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

        PlayerManager.instance.lastGrounedPos = player.transform.position;
    }

    public override void Update()
    {
        base.Update();
        if (Input.GetKeyDown(KeyCode.R) && player.skill.blackhole.blackholeUnlocked)
        {
            if(player.skill.blackhole.cooldownTimer > 0)
            {
                player.fx.CreatePopUpText("ººƒ‹¿‰»¥÷–");
                return;
            }

            stateMachine.ChangState(player.blackhole);
        }

        if (Input.GetKeyDown(KeyCode.Mouse1) && HasNoSword() && player.skill.sword.swordUnlocked && player.skill.sword.CanUseSkill())
            stateMachine.ChangState(player.aimSword);

        if (Input.GetKeyDown(KeyCode.Q) && player.skill.parry.parryUnlocked)
            stateMachine.ChangState(player.counterAttack);

        if (Input.GetKeyDown(KeyCode.Mouse0))
            stateMachine.ChangState(player.primaryAttack);

        if (!player.isGroundDetected())
            stateMachine.ChangState(player.airState);

        if (Input.GetKeyDown(KeyCode.Space) && player.jumpTimes < player.jumpLimit)
        {
            player.jumpTimes++;
            stateMachine.ChangState(player.jumpState);
        }
    }

    private bool HasNoSword()
    {
        if(!player.skill.sword.sword)
            return true;
        player.skill.sword.sword.GetComponent<Sword_Skill_Controller>().ReturnSword();
        return false;

    }
}
