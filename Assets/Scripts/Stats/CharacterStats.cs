using System.Collections;
using UnityEngine;


public enum StatType
{
    strength,
    agility,
    intelegence,
    vitality,
    damage,
    critChance,
    critPower,
    health,
    armor,
    evasion,
    magicRes,
    fireDamage,
    iceDamage,
    lightningDamage
}
public class CharacterStats : MonoBehaviour
{
    public EntityFX fx;

    [Header("基础属性")]
    [Tooltip("力量，1力量提高1点伤害(攻击力)和1%暴伤")]
    public Stat strength;
    [Tooltip("敏捷，1敏捷增加1%闪避率和1%暴击率")]
    public Stat agility;
    [Tooltip("智力，1智力增加1魔法伤害和3点魔抗")]
    public Stat intelligence;
    [Tooltip("体质，1体质增加4点最大生命值")]
    public Stat vitality;

    [Header("攻击属性")]
    [Tooltip("伤害")]
    public Stat damage;
    [Tooltip("暴击")]
    public Stat critChance;
    [Tooltip("爆伤")]
    public Stat critPower;

    [Header("防御属性")]
    [Tooltip("生命值")]
    public Stat maxHealth;
    [Tooltip("护甲")]
    public Stat armor;
    [Tooltip("闪避")]
    public Stat evasion;
    [Tooltip("魔抗")]
    public Stat magicResistance;

    [Header("魔法属性")]
    [Tooltip("火伤")]
    public Stat fireDamage;
    [Tooltip("冰伤")]
    public Stat iceDamage;
    [Tooltip("雷伤")]
    public Stat lightningDamage;

    public bool isIgnited; //点燃
    public bool isChilled; //寒冷，减少20%护甲
    public bool isShocked; //麻痹，降低命中率

    private float igniteDuration = 2;
    private float ignitedTimer;
    private float ignitedDamageCoolDown = .3f;
    private float ignitedDamageTimer;
    private int ignitedDamage;

    private float chillDuration = 2;
    private float chilledTimer;
    private float slowPercentage = .2f;

    private float shockDuration = 4;
    private float shockedTimer;
    private int shockedDamage;
    [SerializeField] private GameObject shockStrikePrefab;


    public int currentHealth;

    public System.Action OnHP_Changed;
    public bool isDead { get; private set; }

    /// <summary>最近一次攻击自己的对象（衣服的反弹伤害等效果会用到）。</summary>
    [System.NonSerialized] public CharacterStats lastAttacker;
    private bool isVulnerable;
    public bool isInvincible { get; private set; }

    protected virtual void Start()
    {
        critPower.SetDefaultValue(150);
        currentHealth = GetMaxHealthValue();
        fx = GetComponent<EntityFX>();
    }

    protected virtual void Update()
    {
        ignitedTimer -= Time.deltaTime;
        chilledTimer -= Time.deltaTime;
        shockedTimer -= Time.deltaTime;

        ignitedDamageTimer -= Time.deltaTime;

        if (ignitedTimer < 0)
            isIgnited = false;

        if (chilledTimer < 0)
            isChilled = false;

        if (shockedTimer < 0)
            isShocked = false;

        if(isIgnited)
            ApplyIgnite();
    }

    public void MakeVulnerableFor(float _duration) => StartCoroutine(VulnerableCorutine(_duration));

    private IEnumerator VulnerableCorutine(float _duartion)
    {
        isVulnerable = true;

        yield return new WaitForSeconds(_duartion);

        isVulnerable = false;
    }

    public virtual void IncreaseStatBy(int _modifier, float _duration, Stat _statToModifiy)
    {
        StartCoroutine(StatModCoroutine(_modifier,_duration, _statToModifiy));
    }

    private IEnumerator StatModCoroutine(int _modifier, float _duration, Stat _statToModify)
    {
        _statToModify.AddModifier(_modifier);

        yield return new WaitForSeconds(_duration);

        _statToModify.RemoveModifier(_modifier);
    }

    public virtual void DoDamage(CharacterStats _targetStats)
    {
        if (_targetStats.isInvincible) return;
        _targetStats.lastAttacker = this;   // 记下攻击者，方便被攻击方做反弹之类
        DoPhysicsDamage(_targetStats);
        DoMagicDamage(_targetStats);
        _targetStats.GetComponent<Entity>().SetupKnockbackDir(transform);
    }

    public virtual void DoPhysicsDamage(CharacterStats _targetStats)
    {
        if (TargetCanAvoidAttack(_targetStats))
            return;

        int totalDamage = damage.GetValue() + strength.GetValue();
        bool criticalHit = false;

        if (CanCrit())
        {
            totalDamage = CalculateCriticalDamage(totalDamage);
            criticalHit = true;
        }

        fx.CreateHitFX(_targetStats.transform, criticalHit);

        // 玩家打出暴击时的专属音效（57）
        if (criticalHit && this is PlayerStats && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(57, transform);

        totalDamage = CheckTargetArmor(_targetStats, totalDamage);

        // 暴击用红色并带「！」，普通物理保持白色
        _targetStats.TakeDamage(totalDamage, criticalHit ? DamageType.Critical : DamageType.Physical);
    }


    #region 魔法伤害和异常
    public virtual void DoMagicDamage(CharacterStats _targetStats)
    {
        int _fireDamage = fireDamage.GetValue();
        int _iceDamage = iceDamage.GetValue();
        int _lightningDamage = lightningDamage.GetValue();

        ApplyMagicDamage(_targetStats, _fireDamage, _iceDamage, _lightningDamage, intelligence.GetValue());

        CheckCanApplyAilments(_targetStats, _fireDamage, _iceDamage, _lightningDamage);
    }

    /// <summary>
    /// 魔法伤害的结算：血量还是按老规矩只扣一次（数值不变），
    /// 但伤害数字按火 / 冰 / 雷 / 智力拆开弹，这样各自的颜色才对得上。
    /// _multiplier &lt;= 0 表示不额外乘倍率（分身技能会用到）。
    /// </summary>
    protected void ApplyMagicDamage(CharacterStats _targetStats, int _fireDamage, int _iceDamage,
        int _lightningDamage, int _intelligenceDamage, float _multiplier = 0f)
    {
        int rawTotal = _fireDamage + _iceDamage + _lightningDamage + _intelligenceDamage;

        int totalMagicDamage = CheckTargetResistance(_targetStats, rawTotal);

        if (_multiplier > 0f)
            totalMagicDamage = Mathf.RoundToInt(totalMagicDamage * _multiplier);

        // 扣血这一发不弹数字，数字在下面按属性分开弹
        _targetStats.TakeDamage(totalMagicDamage, DamageType.None);

        if (rawTotal <= 0)
            return;

        // 各属性按同一个减伤/倍率系数折算，拆出来的数字加起来≈实际扣的血
        float factor = (float)totalMagicDamage / rawTotal;

        ShowDamagePopUp(_targetStats, _fireDamage, factor, DamageType.Fire);
        ShowDamagePopUp(_targetStats, _iceDamage, factor, DamageType.Ice);
        ShowDamagePopUp(_targetStats, _lightningDamage, factor, DamageType.Lightning);
        ShowDamagePopUp(_targetStats, _intelligenceDamage, factor, DamageType.Magic);
    }

    /// <summary>按属性弹一个伤害数字（数值 = 原始值 × 减伤/倍率系数）。</summary>
    private void ShowDamagePopUp(CharacterStats _targetStats, int _rawDamage, float _factor, DamageType _type)
    {
        if (_rawDamage <= 0 || _targetStats == null || _targetStats.fx == null)
            return;

        int value = Mathf.RoundToInt(_rawDamage * _factor);

        if (value <= 0)
            return;

        _targetStats.fx.CreatePopUpText(value.ToString(), DamageStyle.ColorOf(_type));
    }

    protected void CheckCanApplyAilments(CharacterStats _targetStats, int _fireDamage, int _iceDamage, int _lightningDamage)
    {
        int maxElementDamage = Mathf.Max(_fireDamage, _iceDamage, _lightningDamage);

        if (maxElementDamage <= 0)
            //if (maxElementDamage <= 0 ||_targetStats.isIgnited || _targetStats.isChilled || _targetStats.isShocked)
            return;

        bool canIgnite = false, canChille = false, canShocke = false;

        //同时只能套用一种异常
        //CheckOnlyOneAilment(_fireDamage, _iceDamage, _lightningDamage, maxElementDamage, ref canIgnite, ref canChille, ref canShocke);

        //同时可套用多种异常
        if(_fireDamage > 0 && !_targetStats.isIgnited) 
            canIgnite = true;
        if(_iceDamage > 0 && !_targetStats.isChilled)
            canChille = true;
        if(_lightningDamage > 0 && !_targetStats.isShocked)
            canShocke = true;

        ApplyAilments(_targetStats, canIgnite, canChille, canShocke);
    }

    private void CheckOnlyOneAilment(int _fireDamage, int _iceDamage, int _lightningDamage, int maxElementDamage, ref bool canIgnite, ref bool canChille, ref bool canShocke)
    {
        if (_fireDamage == _iceDamage && _fireDamage == _lightningDamage && _fireDamage == _lightningDamage)
        {
            float chance = Random.value;
            Debug.Log(chance);
            if (chance < .33f)
                canIgnite = true;
            else if (chance < .66f)
                canChille = true;
            else
                canShocke = true;
        }
        else if (_fireDamage == maxElementDamage)
        {
            if (_iceDamage != _fireDamage && _lightningDamage != _fireDamage)
                canIgnite = true;
            else if (_iceDamage == _fireDamage)
            {
                canIgnite = Random.value < .5f;
                canChille = !canIgnite;
            }
            else
            {
                canIgnite = Random.value < .5f;
                canShocke = !canIgnite;
            }
        }
        else if (_iceDamage == maxElementDamage)
        {
            if (_iceDamage == _lightningDamage)
            {
                canChille = Random.value < .5f;
                canShocke = !canChille;
            }
            else
            {
                canChille = true;
            }
        }
        else
            canShocke = true;
    }

    

    public void ApplyAilments(CharacterStats _targetStats, bool _ignite, bool _chill, bool _shock)
    {
        if (_ignite)
        {
            _targetStats.isIgnited = _ignite;
            _targetStats.ignitedTimer = igniteDuration;

            //给予敌人点燃伤害
            _targetStats.SetupIgnitedDamage(Mathf.RoundToInt(_targetStats.maxHealth.GetValue() * .001f + fireDamage.GetValue() * .2f));
            _targetStats.fx.IgnitedFxFor(igniteDuration);
        }
        if (_chill)
        {
            _targetStats.isChilled = _chill;
            _targetStats.chilledTimer = chillDuration;

            _targetStats.GetComponent<Entity>().SlowEntityBy(slowPercentage, chillDuration);
            _targetStats.fx.ChilledFxFor(chillDuration);
        }
        if (_shock)
        {
            _targetStats.SetupShockDamage(Mathf.RoundToInt(lightningDamage.GetValue() * .1f));
            if (!_targetStats.isShocked)
            {
                ApplyShock(_targetStats, _shock);
            }
            else
            {
                if (_targetStats.GetComponent<Player>() != null)
                    return;

                GenerateShcokStrikeTo(_targetStats);

            }
        }
    }

    private void ApplyIgnite()
    {
        if (ignitedDamageTimer < 0)
        {
            DoIgniteDamage();
            if (currentHealth <= 0)
            {
                isIgnited = false;
                Die();
            }
            ignitedDamageTimer = ignitedDamageCoolDown;
        }
    }
    public void ApplyShock(CharacterStats _targetStats, bool _shock)
    {
        if (_targetStats.isShocked)
            return;

        _targetStats.isShocked = _shock;
        _targetStats.shockedTimer = shockDuration;

        _targetStats.fx.ShockedFxFor(shockDuration);
    }

    private void GenerateShcokStrikeTo(CharacterStats _targetStats)
    {
        float cloestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(_targetStats.transform.position, 25);

        foreach (var hit in colliders)
        {
            if (hit.GetComponent<Enemy>() != null && Vector2.Distance(_targetStats.transform.position, hit.transform.position) > 1)
            {
                float distanceToEnemy = Vector2.Distance(_targetStats.transform.position, hit.transform.position);

                if (distanceToEnemy < cloestDistance)
                {
                    closestEnemy = hit.transform;
                    cloestDistance = distanceToEnemy;
                }
            }

            if (closestEnemy == null)
                closestEnemy = _targetStats.transform;
        }

        if (closestEnemy != null)
        {
            GameObject newShockStrike = Instantiate(shockStrikePrefab, _targetStats.transform.position, Quaternion.identity);
            newShockStrike.GetComponent<ShockStrike_Controller>().Setup(shockedDamage, closestEnemy.GetComponent<CharacterStats>());
        }
    }

    private void DoIgniteDamage()
    {
        //Debug.Log("burn damage:" + ignitedDamage);
        // 持续燃烧属于火焰伤害，数字用橙色
        DecreaseHP_By(ignitedDamage, DamageType.Fire);
    }

    #endregion

    public virtual void TakeDamage(int _damage)
    {
        TakeDamage(_damage, DamageType.Physical);
    }

    /// <summary>_type 只影响伤害数字的颜色/格式，不影响数值。</summary>
    public virtual void TakeDamage(int _damage, DamageType _type)
    {
        if (isInvincible)
            return;

        if (isVulnerable)
            _damage = Mathf.RoundToInt(_damage * 1.1f);
        DecreaseHP_By(_damage, _type);

        GetComponent<Entity>().DamegedEffect();

        if (currentHealth <= 0 && !isDead)
        {
            Die();
        }
    }

    /// <summary>回血：弹出实际回上去的量（满血时不会弹）。</summary>
    public virtual void IncreaseHP_By(int _amount)
    {
        int before = currentHealth;

        currentHealth += _amount;

        if (currentHealth > GetMaxHealthValue())
            currentHealth = GetMaxHealthValue();

        // 真正加上去的那部分：满血时 healed = 0，就不会弹数字
        int healed = currentHealth - before;

        if (healed > 0 && !isDead && fx != null)
            fx.CreatePopUpText(DamageStyle.FormatHeal(healed), DamageStyle.HealColor);

        OnHP_Changed?.Invoke();
    }

    public virtual void DecreaseHP_By(int _damage)
    {
        DecreaseHP_By(_damage, DamageType.Physical);
    }

    public virtual void DecreaseHP_By(int _damage, DamageType _type)
    {
        //if (isDead) return;
        if (_damage > 0)
        {
            // None = 只扣血不弹数字（魔法伤害拆成多段数字时，总扣血那一发用这个）
            if (_type != DamageType.None)
                fx.CreatePopUpText(DamageStyle.Format(_damage, _type), DamageStyle.ColorOf(_type));
        }

        currentHealth -= _damage;

        OnHP_Changed?.Invoke();
    }

    protected virtual void Die()
    {
        isDead = true;
    }

    #region 闪避、护甲、抗性、暴击、引燃、雷击、HP上限计算

    public virtual void OnEvasion(Transform _responTransform)
    {

    }
    protected bool TargetCanAvoidAttack(CharacterStats _targetStats)
    {
        int totalEvasion = _targetStats.evasion.GetValue() + _targetStats.agility.GetValue();

        if (isShocked)
            totalEvasion += 20;

        if (Random.Range(0, 100) < totalEvasion)
        {
            _targetStats.OnEvasion(transform);
            return true;
        }

        return false;
    }
    //护甲减伤比例为：[护甲值] / ([护甲值] + 50)
    protected int CheckTargetArmor(CharacterStats _targetStats, int totalDamage)
    {
        int targetArmor = _targetStats.armor.GetValue();
        if (_targetStats.isChilled)
        {
            targetArmor = Mathf.RoundToInt(targetArmor * .8f);
        }

        totalDamage = Mathf.RoundToInt(totalDamage * (1 - (targetArmor / (targetArmor + 50))));
        //保底伤害
        if (totalDamage <= 0) totalDamage = 1;
        return totalDamage;
    }

    //魔抗减伤比例：[总魔抗] / ([总魔抗] + 66)
    protected int CheckTargetResistance(CharacterStats _targetStats, int totalMagicDamage)
    {
        int targetResistance = _targetStats.magicResistance.GetValue() + 3 * _targetStats.intelligence.GetValue();
        totalMagicDamage = Mathf.RoundToInt(totalMagicDamage * (1 - (targetResistance / (targetResistance + 66))));
        if (totalMagicDamage <= 0) totalMagicDamage = 1;
        return totalMagicDamage;
    }

    protected bool CanCrit()
    {
        int totalCritChance = critChance.GetValue() + agility.GetValue();

        if (Random.Range(0, 100) <= totalCritChance)
            return true;

        return false;
    }

    protected int CalculateCriticalDamage(int _damage)
    {
        float totalCritPower = (critPower.GetValue() + strength.GetValue()) * .01f;

        float critDamage = _damage * totalCritPower;

        return Mathf.RoundToInt(critDamage);
    }
    private int SetupIgnitedDamage(int _damage) => ignitedDamage = _damage;
    private int SetupShockDamage(int _damage) => shockedDamage = _damage;

    public int GetMaxHealthValue() => maxHealth.GetValue() + vitality.GetValue() * 4;
    public int GetDamageValue() => damage.GetValue() + strength.GetValue();
    public int GetCritPowerValue() => critPower.GetValue() + strength.GetValue();
    public int GetCrtiChanceValue() => critChance.GetValue() + agility.GetValue();
    public int GetEvasionValue() => evasion.GetValue() + agility.GetValue();
    public int GetMagicResistance() => magicResistance.GetValue() + intelligence.GetValue() * 3;
    #endregion

    public Stat GetStat(StatType _statType)
    {
        return _statType switch
        {
            StatType.strength => strength,
            StatType.agility => agility,
            StatType.intelegence => intelligence,
            StatType.vitality => vitality,
            StatType.damage => damage,
            StatType.critChance => critChance,
            StatType.critPower => critPower,
            StatType.health => maxHealth,
            StatType.armor => armor,
            StatType.evasion => evasion,
            StatType.magicRes => magicResistance,
            StatType.fireDamage => fireDamage,
            StatType.iceDamage => iceDamage,
            StatType.lightningDamage => lightningDamage,
            _ => null,
        };
    }
    public string GetStatName(StatType _statType)
    {
        return _statType switch
        {
            StatType.strength => "力量",
            StatType.agility => "敏捷",
            StatType.intelegence => "智力",
            StatType.vitality => "体质",
            StatType.damage => "攻击力",
            StatType.critChance => "暴击",
            StatType.critPower => "爆伤",
            StatType.health => "生命",
            StatType.armor => "护甲",
            StatType.evasion => "闪避",
            StatType.magicRes => "魔抗",
            StatType.fireDamage => "火伤",
            StatType.iceDamage => "冰伤",
            StatType.lightningDamage => "雷伤",
            _ => "",
        };
    }

    public void KillEntity()
    {
        if(!isDead)
            Die();
    }

    public void MakeInvincible(bool _invincible) => isInvincible = _invincible;

}
