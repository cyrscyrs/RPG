using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_DeathBringerTrigger : EnemyAnimationTriggers
{
    private Enemy_DeathBringer enemy => GetComponentInParent<Enemy_DeathBringer>();
    private void Relocate() => enemy.FindPosition();

    private void MakeInvisible() => enemy.fx.MakeTransparent(true);
    private void MakeVisible() => enemy.fx.MakeTransparent(false);
}
