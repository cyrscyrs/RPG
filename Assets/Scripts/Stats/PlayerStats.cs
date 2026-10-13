using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : CharacterStats
{
    private Player player;
    protected override void Start()
    {
        base.Start();

        player = GetComponent<Player>();
    }

    public override void TakeDamage(int _damage)
    {
        TakeDamage(_damage, DamageType.Physical);
    }

    public override void TakeDamage(int _damage, DamageType _type)
    {
        if (isInvincible)
        {
            base.TakeDamage(_damage, _type);   // 冲刺无敌帧：不消耗免死
            return;
        }

        // 衣服 20 级：免疫一次致命伤害（CD 60 秒）
        if (_damage >= currentHealth && TryUseLethalImmunity())
            return;

        // 受伤喊声（32）+ 受击音效（52）：挨打时的两声反馈
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(32, null);
            AudioManager.instance.PlaySFX(52, null);
        }

        // 先取出攻击者，用来做衣服 10 级的反弹
        CharacterStats attacker = lastAttacker;
        lastAttacker = null;

        base.TakeDamage(_damage, _type);
        player.DamegedEffect();

        TryReflectDamage(attacker, _damage);
    }

    /// <summary>武器 20 级：攻击后如果敌人生命值低于门槛，直接斩杀。</summary>
    public override void DoDamage(CharacterStats _targetStats)
    {
        base.DoDamage(_targetStats);

        if (_targetStats == null || _targetStats.isDead || EquipmentLevelManager.Instance == null)
            return;

        ItemData_Equipment weapon = Inventory.instance != null ? Inventory.instance.GetEquipment(EquipmentType.Weapon) : null;

        if (weapon == null || !EquipmentLevelManager.Instance.HasMilestone(weapon, EquipmentLevelManager.Instance.Config.secondMilestone))
            return;

        float threshold = EquipmentLevelManager.Instance.ExecuteHealthPercent;

        if (_targetStats.currentHealth > 0 && _targetStats.currentHealth <= _targetStats.GetMaxHealthValue() * threshold)
        {
            Debug.Log($"武器 20 级效果：斩杀「{_targetStats.name}」（生命值低于 {threshold * 100f:0.#}%）");

            // 斩杀时在敌人头上弹一个红色的「处决！」
            if (_targetStats.fx != null)
                _targetStats.fx.CreatePopUpText("处决！", Color.red);

            _targetStats.KillEntity();
        }
    }

    /// <summary>衣服 20 级：致命伤害挡下来，血量留 1 点。</summary>
    private bool TryUseLethalImmunity()
    {
        ItemData_Equipment armor = Inventory.instance != null ? Inventory.instance.GetEquipment(EquipmentType.Armor) : null;

        if (armor == null || EquipmentLevelManager.Instance == null)
            return false;

        if (!EquipmentLevelManager.Instance.HasMilestone(armor, EquipmentLevelManager.Instance.Config.secondMilestone))
            return false;

        if (!EquipmentLevelManager.Instance.TryUseLethalImmunity())
            return false;

        currentHealth = 1;
        OnHP_Changed?.Invoke();
        player.DamegedEffect();

        Debug.Log($"衣服 20 级效果：免疫了一次致命伤害（冷却 {EquipmentLevelManager.Instance.LethalImmunityCooldown:0} 秒）");

        return true;
    }

    /// <summary>衣服 10 级：把受到的伤害按比例反弹给攻击者。</summary>
    private void TryReflectDamage(CharacterStats _attacker, int _damage)
    {
        if (_attacker == null || _attacker.isDead || _damage <= 0)
            return;

        ItemData_Equipment armor = Inventory.instance != null ? Inventory.instance.GetEquipment(EquipmentType.Armor) : null;

        if (armor == null || EquipmentLevelManager.Instance == null)
            return;

        if (!EquipmentLevelManager.Instance.HasMilestone(armor, EquipmentLevelManager.Instance.Config.firstMilestone))
            return;

        int reflectDamage = Mathf.RoundToInt(_damage * EquipmentLevelManager.Instance.ReflectPercent);

        if (reflectDamage <= 0)
            return;

        Debug.Log($"衣服 10 级效果：反弹 {reflectDamage} 点伤害给「{_attacker.name}」");
        _attacker.TakeDamage(reflectDamage);
    }
    protected override void Die()
    {
        base.Die();

        // 死亡音效（35）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(35, null);

        player.Die();

        GameManager.instance.lostCurrencyAmount = PlayerManager.instance.currency;
        PlayerManager.instance.currency = 0;

        GetComponent<PlayerItemDrop>()?.GenerateDrop();
    }

    public override void DecreaseHP_By(int _damage)
    {
        DecreaseHP_By(_damage, DamageType.Physical);
    }

    public override void DecreaseHP_By(int _damage, DamageType _type)
    {
        base.DecreaseHP_By(_damage, _type);
        if (_damage > GetMaxHealthValue() * Tuning.Config.playerHeavyHitHealthPercent)
        {
            player.SetupKncokbackPower(new Vector2(10, 6));
            player.fx.ScreenShake(player.fx.shake_getHighDamage);

        }

        ItemData_Equipment currentArmor = Inventory.instance.GetEquipment(EquipmentType.Armor);

        if (currentArmor != null)
            currentArmor.Effect(player.transform);
    }

    public override void OnEvasion(Transform _responTransform)
    {
        player.skill.dodge.MakeMirageOnDodge(_responTransform);
    }

    public void CloneDoDamage(CharacterStats _targetStats, float _multiplier)
    {
        CloneDoPhysicsDamage(_targetStats, _multiplier);
        CloneDoMagicDamage(_targetStats, _multiplier);

    }

    private void CloneDoPhysicsDamage(CharacterStats _targetStats, float _multiplier)
    {
        if (TargetCanAvoidAttack(_targetStats))
            return;

        int totalDamage = damage.GetValue() + strength.GetValue();

        bool criticalHit = false;

        if (_multiplier > 0)
            totalDamage = Mathf.RoundToInt(totalDamage * _multiplier);

        if (CanCrit())
        {
            totalDamage = CalculateCriticalDamage(totalDamage);
            criticalHit = true;
        }

        totalDamage = CheckTargetArmor(_targetStats, totalDamage);
        _targetStats.TakeDamage(totalDamage, criticalHit ? DamageType.Critical : DamageType.Physical);
    }

    private void CloneDoMagicDamage(CharacterStats _targetStats, float _attackMultiplier)
    {
        int _fireDamage = fireDamage.GetValue();
        int _iceDamage = iceDamage.GetValue();
        int _lightningDamage = lightningDamage.GetValue();

        // 分身的魔法伤害同样按属性拆开弹数字
        ApplyMagicDamage(_targetStats, _fireDamage, _iceDamage, _lightningDamage, intelligence.GetValue(), _attackMultiplier);

        CheckCanApplyAilments(_targetStats, _fireDamage, _iceDamage, _lightningDamage);
    }
}
