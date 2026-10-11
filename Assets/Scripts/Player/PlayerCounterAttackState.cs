using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCounterAttackState : PlayerState
{
    private bool canCreateClone;
    // 同一次弹反里可能同时框到多个碰撞体，声音只放一次
    private bool parrySoundPlayed;
    public PlayerCounterAttackState(Player _player, PlayerStateMachine _stateMachine, string _animBoolName) : base(_player, _stateMachine, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        canCreateClone = true;
        parrySoundPlayed = false;
        stateTimer = player.counterAttackDuration;
        player.anim.SetBool("SuccessfulCounterAttack", false);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        base.Update();

        player.SetZeroVelocity();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(player.attackCheck.position, player.attackCheckRadius);

        foreach (var hit in colliders)
        {
            if(hit.GetComponent<Arrow_Controller>() != null)
            {
                SuccessfulCounter();
                hit.GetComponent<Arrow_Controller>().FlipArrow();
            }

            if (hit.GetComponent<Enemy>() != null)
            {
                if (hit.GetComponent<Enemy>().CanBeStunned())
                {
                    SuccessfulCounter();

                    player.skill.parry.UseSkill();

                    if (canCreateClone)
                    {
                        canCreateClone = false;
                        player.skill.parry.MakeMirageOnParry(hit.transform);
                    }
                }
            }
        }

        if (stateTimer < 0 || triggerCalled)
            stateMachine.ChangState(player.idleState);
    }

    private void SuccessfulCounter()
    {
        stateTimer = 10;
        player.anim.SetBool("SuccessfulCounterAttack", true);

        // 成功弹反音效（61）：一次弹反只响一下
        if (!parrySoundPlayed)
        {
            parrySoundPlayed = true;

            if (AudioManager.instance != null)
                AudioManager.instance.PlaySFX(61, player.transform);
        }
    }
}
