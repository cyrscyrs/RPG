using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crystal_Skill_Controller : MonoBehaviour
{
    private Animator anim => GetComponent<Animator>();
    private CircleCollider2D cd => GetComponent<CircleCollider2D>();
    private Player player;
    private float crystalExistTimer;

    private bool canExplode;
    private bool canMoveToEnemy;
    private float moveSpeed;

    private bool canGrow;
    private float growSpeed = 5;

    private Transform closestEnemy;
    [SerializeField] private LayerMask whatIsEnemy;

    public void SetupCrystal(Player _player, float _crystalDuration, bool _canExplode, bool _canMoveToEnemy, float _moveSpeed, Transform _closestEnemy)
    {
        player = _player;
        crystalExistTimer = _crystalDuration;
        canExplode = _canExplode;
        canMoveToEnemy = _canMoveToEnemy;
        moveSpeed = _moveSpeed;
        closestEnemy = _closestEnemy;
    }

    public void ChooseRandomEnemy()
    {
        float radius = SkillManager.instance.blackhole.GetBlackholeRadius();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, radius, whatIsEnemy);

        if (colliders.Length > 0)
            closestEnemy = colliders[Random.Range(0, colliders.Length)].transform;
    }

    private void Update()
    {
        crystalExistTimer -= Time.deltaTime;
        if (crystalExistTimer < 0)
        {
            FinishCrystal();
        }

        if (canMoveToEnemy)
        {
            if (closestEnemy != null)
            {
                transform.position = Vector2.MoveTowards(transform.position, closestEnemy.position, moveSpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, closestEnemy.position) < 1)
                {
                    FinishCrystal();
                    canMoveToEnemy = false;
                }
            }
            else
                return;

        }

        if (canGrow)
        {
            transform.localScale = Vector2.Lerp(transform.localScale, new Vector2(3, 3), growSpeed * Time.deltaTime);
        }
    }

    public void AnimationExplodeEvent()
    {

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, cd.radius * growSpeed* 0.5f);

        foreach (var hit in colliders)
        {
            if (hit.GetComponent<Enemy>() != null)
            {
                hit.GetComponent<Entity>().SetupKnockbackDir(transform);

                EnemyStats target = hit.GetComponent<EnemyStats>();
                if(target != null)
                    player.stats.DoMagicDamage(target);

                Inventory.instance.GetEquipment(EquipmentType.Amulet)?.Effect(target.transform);
            }
        }
    }

    public void FinishCrystal()
    {
        if (canExplode)
        {
            canGrow = true;
            anim.SetTrigger("Explode");
        }
        else
            SelfDestroy();
    }

    public void SelfDestroy() => Destroy(gameObject);
}
