using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArcherDeadState : EnemyState
{
    protected Enemy_Archer enemy;
    public ArcherDeadState(Enemy _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, Enemy_Archer _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        enemy.anim.SetBool(enemy.lastAnimBoolName, true);
        enemy.anim.speed = 0;
        enemy.cd.enabled = false;

        stateTimer = .1f;
    }


    public override void Update()
    {
        base.Update();
        if (stateTimer > 0)
        {
            rb.velocity = new Vector2(0, 10);
        }

        if (stateTimer < -5)
            Object.Destroy(enemy.gameObject);
    }

}
