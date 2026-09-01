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
        player.fx.CreatePopUpText("¼¼ÄÜÀäÈ´ÖÐ");
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
