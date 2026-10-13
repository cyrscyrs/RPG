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

    /// <summary>等级加成算过没有（算过之后再改 level 不会补算属性）。</summary>
    private bool levelApplied;

    /// <summary>
    /// 关掉这只怪的掉落（Boss 召唤出来的怪用：杀了不掉东西）。
    /// 只在这一次运行里有效，不动预制体上的配置。
    /// </summary>
    [System.NonSerialized] public bool suppressDrops;

    /// <summary>
    /// 运行时改等级（Boss 召唤的怪用它设成 5 级）。
    ///
    /// ⚠ 等级加成是在 Start 里一次性乘进属性里的，所以要在 Instantiate 之后、Start 之前调用
    /// （Instantiate 会立刻跑 Awake，Start 要等到这一帧的 Update 之后，紧接着设是安全的）。
    /// </summary>
    public void SetLevel(int _level)
    {
        level = Mathf.Max(1, _level);

        if (levelApplied)
            Debug.LogWarning($"{name}：等级是在 Start 之后才改的，属性加成不会再补算（召唤物要在 Instantiate 后立刻 SetLevel）。");
    }

    /// <summary>每级属性成长比例：这个敌人自己配了就用它，没配（<= 0）就用调参表里的全局值。</summary>
    private float LevelStatPercent
        => percentageModifier > 0f ? percentageModifier : Tuning.Config.enemyLevelStatPercent;
    protected override void Start()
    {
        //soulsDropAmont.SetDefaultValue(1000);
        ApplyLevelModifier();
        levelApplied = true;

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
            float modifier = _stat.GetValue() * LevelStatPercent;

            _stat.AddModifier(Mathf.RoundToInt(modifier));
        }
    }

    public override void TakeDamage(int _damage)
    {
        TakeDamage(_damage, DamageType.Physical);
    }

    public override void TakeDamage(int _damage, DamageType _type)
    {
        // 怪物受击音效（58）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(58, transform);

        base.TakeDamage(_damage, _type);
        enemy.DamegedEffect();
    }

    protected override void Die()
    {
        base.Die();

        // 怪物死亡喊声（20）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(20, transform);

        MonsterBestiary.NotifyKill(MonsterId);   // 图鉴：累计击杀 +1
        BountyBoard.NotifyKill(MonsterId);       // 赏金任务：猎杀型任务进度
        KillStreakBuffManager.NotifyKill(MonsterId);   // 连续杀敌加速：连击进度
        enemy.Die();
        PlayerManager.instance.currency += soulsDropAmont.GetValue();

        // Boss 召唤出来的怪不掉东西（击杀奖励 / 图鉴计数照旧，只把掉落物去掉）
        if (!suppressDrops)
            myDrop.GenerateDrop();
    }
}
