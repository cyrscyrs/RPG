using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Clone_Skill_Controller : MonoBehaviour
{
    private Player player;
    private SpriteRenderer sr;
    private Animator anim;
    [SerializeField] private float colorLosingSpeed;

    private float cloneTimer;
    private float attackMultiplier;
    [SerializeField] Transform attackCheck;
    [SerializeField] float attackCheckRadius;
    private Transform closestEnemy;
    private int cloneFacingDir = 1;

    private bool canDuplicateClone;
    private float chanceToDuplicate;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        cloneTimer -= Time.deltaTime;

        if(cloneTimer < 0)
        {
            sr.color = new Color(1, 1, 1, sr.color.a - (Time.deltaTime * colorLosingSpeed));
        }

        if(sr.color.a <= 0)
            Destroy(gameObject);
    }
    public void SetupClone(Player _player,Transform _newTransform, float _cloneDuration, bool _canAttack, 
        Vector3 _offset, Transform _closestEnemy, bool _canDuplicateClone, float _chanceToDuplicate, float _attackMultiplier)
    {

        if (_canAttack)
            anim.SetInteger("AttackNumber", Random.Range(1, 4));

        player = _player;
        transform.position= _newTransform.position + _offset;
        cloneTimer = _cloneDuration;
        closestEnemy = _closestEnemy;
        canDuplicateClone = _canDuplicateClone;
        chanceToDuplicate = _chanceToDuplicate;
        attackMultiplier = _attackMultiplier;

        FaceClosestEnemy();
    }

    private void AnimationTrigger()
    {
        cloneTimer = -.1f;
    }

    private void AttackTrigger()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(attackCheck.position, attackCheckRadius);

        foreach (var hit in colliders)
        {
            if (hit.GetComponent<Enemy>() != null)
            {
                hit.GetComponent<Entity>().SetupKnockbackDir(transform);

                //player.stats.DoDamage(hit.GetComponent<CharacterStats>());
                PlayerStats playerStats = player.GetComponent<PlayerStats>();
                EnemyStats enemyStats = hit.GetComponent<EnemyStats>();

                playerStats.CloneDoDamage(enemyStats, attackMultiplier);

                if (player.skill.clone.canApplyOnHitEffect)
                {
                    Inventory.instance.GetEquipment(EquipmentType.Weapon)?.Effect(hit.transform);
                }

                if (canDuplicateClone)
                {
                    if(Random.Range(0,100) < chanceToDuplicate)
                    {
                        SkillManager.instance.clone.CreateClone(hit.transform, new Vector3(.5f * cloneFacingDir, 0));
                    }
                }
            }
        }
    }

    private void FaceClosestEnemy()
    {
        

        //克隆初始面向右，敌人在克隆的左侧的时候要翻转
        if(closestEnemy != null)
        {
            if (transform.position.x > closestEnemy.position.x)
            {
                cloneFacingDir = -1;
                transform.Rotate(0, 180, 0);
            }
            
        }
    }
}
