using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStats : CharacterStats
{
    private Enemy enemy;
    private ItemDrop myDrop;
    public Stat soulsDropAmont;

    [Tooltip("图鉴用的怪物 ID。留空 = 用这个物体的名字（会自动去掉 (Clone)），一般留空就行")]
    [SerializeField] private string monsterId;

    /// <summary>怪物图鉴统计击杀数用的 ID。</summary>
    public string MonsterId => string.IsNullOrEmpty(monsterId)
        ? MonsterBestiary.CleanName(gameObject.name)
        : MonsterBestiary.CleanName(monsterId);

    [Header("Level details")]
    [SerializeField] private int level = 1;

    [Range(0f, 1f)]
    [SerializeField] private float percentageModifier = .1f;
    protected override void Start()
    {
        //soulsDropAmont.SetDefaultValue(1000);
        ApplyLevelModifier();

        base.Start();

        enemy = GetComponent<Enemy>();
        myDrop = GetComponent<ItemDrop>();
    }

    private void ApplyLevelModifier()
    {
        Modify(strength);
        Modify(agility);
        Modify(intelligence);
        Modify(vitality);

        Modify(damage);
        //Modify(critChance);
        //Modify(critPower);

        Modify(maxHealth);
        Modify(armor);
        //Modify(evasion);
        Modify(magicResistance);

        Modify(fireDamage);
        Modify(iceDamage);
        Modify(lightningDamage);

        Modify(soulsDropAmont);
    }

    private void Modify(Stat _stat)
    {
        for (int i = 1; i < level; i++)
        {
            float modifier = _stat.GetValue() * percentageModifier;

            _stat.AddModifier(Mathf.RoundToInt(modifier));
        }
    }

    public override void TakeDamage(int _damage)
    {
        base.TakeDamage(_damage);
        enemy.DamegedEffect();
    }

    protected override void Die()
    {
        base.Die();
        MonsterBestiary.NotifyKill(MonsterId);   // 图鉴：累计击杀 +1
        enemy.Die();
        PlayerManager.instance.currency += soulsDropAmont.GetValue();
        myDrop.GenerateDrop();
    }
}
