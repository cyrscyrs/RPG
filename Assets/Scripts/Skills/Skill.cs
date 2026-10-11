using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill : MonoBehaviour
{
    public float cooldown;
    [SerializeField]public float cooldownTimer;
    protected Player player;

    protected virtual void Start()
    {
        player = PlayerManager.instance.player;

        // CheckUnlock 里有的技能会碰到别的对象的数据：
        // 「闪避」要给 player.stats 加闪避值，「闪避」还会刷新 Inventory 的属性面板——
        // 而 stats 是 Entity.Start、面板数组是 Inventory.Start 里才赋值的，
        // 这些 Start 谁先跑 Unity 不保证，早跑就会空引用（Dodge_SKILL.cs:35 就是这么挂的）。
        // 所以等这一帧所有 Start 都跑完再检查解锁状态。
        StartCoroutine(CheckUnlockAfterStart());
    }

    private IEnumerator CheckUnlockAfterStart()
    {
        yield return null;
        CheckUnlock();
    }

    protected virtual void Update()
    {
        cooldownTimer -= Time.deltaTime;
    }

    public virtual bool CanUseSkill()
    {
        if(cooldownTimer < 0)
        {
            UseSkill();
            cooldownTimer = cooldown;
            return true;
        }
        player.fx.CreatePopUpText("技能冷却中");
        return false;
    }

    public virtual void UseSkill()
    {

    }

    protected virtual void CheckUnlock()
    {

    }

    protected virtual Transform FindClosestEnemy(Transform _checkTransform)
    {
        float cloestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(_checkTransform.position, 25);

        foreach (var hit in colliders)
        {
            if (hit.GetComponent<Enemy>() != null)
            {
                float distanceToEnemy = Vector2.Distance(_checkTransform.position, hit.transform.position);

                if (distanceToEnemy < cloestDistance)
                {
                    closestEnemy = hit.transform;
                    cloestDistance = distanceToEnemy;
                }
            }
        }

        return closestEnemy;
    }
}
