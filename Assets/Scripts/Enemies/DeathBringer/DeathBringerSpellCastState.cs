using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathBringerSpellCastState : EnemyState
{
    private Enemy_DeathBringer enemy;
    private int amountOfCast;
    private float spellTimer;
    public DeathBringerSpellCastState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_DeathBringer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();
        amountOfCast = enemy.amountOfSpells;
        spellTimer =  .5f;
    }

    public override void Update()
    {
        base.Update();

        spellTimer -= Time.deltaTime;

        if(CanCast())
        {
            enemy.CastSpell();
        }

        if (amountOfCast <= 0)
            stateMachine.ChangState(enemy.teleportState);
    }

    public override void Exit()
    {
        base.Exit();
        enemy.lastTimeCast = Time.time;
    }

    private bool CanCast()
    {
        if(amountOfCast > 0 && spellTimer < 0)
        {
            spellTimer = enemy.spellCooldown;
            amountOfCast--;
            return true;
        }

        return false;
    }
}
