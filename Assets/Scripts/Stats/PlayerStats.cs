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
        base.TakeDamage(_damage);
        player.DamegedEffect();
    }
    protected override void Die()
    {
        base.Die();
        player.Die();

        GameManager.instance.lostCurrencyAmount = PlayerManager.instance.currency;
        PlayerManager.instance.currency = 0;

        GetComponent<PlayerItemDrop>()?.GenerateDrop();
    }

    public override void DecreaseHP_By(int _damage)
    {
        base.DecreaseHP_By(_damage);
        if(_damage > GetMaxHealthValue() * .3f)
        {
            player.SetupKncokbackPower(new Vector2(10, 6));
            player.fx.ScreenShake(player.fx.shake_getHighDamage);

            AudioManager.instance.PlaySFX(35, null);
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

        if (_multiplier > 0)
            totalDamage = Mathf.RoundToInt(totalDamage * _multiplier);

        if (CanCrit())
        {
            totalDamage = CalculateCriticalDamage(totalDamage);
        }

        totalDamage = CheckTargetArmor(_targetStats, totalDamage);
        _targetStats.TakeDamage(totalDamage);
    }

    private void CloneDoMagicDamage(CharacterStats _targetStats, float _attackMultiplier)
    {
        int _fireDamage = fireDamage.GetValue();
        int _iceDamage = iceDamage.GetValue();
        int _lightningDamage = lightningDamage.GetValue();

        int totalMagicDamage = _fireDamage + _iceDamage + _lightningDamage + intelligence.GetValue();
        totalMagicDamage = CheckTargetResistance(_targetStats, totalMagicDamage);

        if (_attackMultiplier > 0)
            totalMagicDamage = Mathf.RoundToInt(totalMagicDamage * _attackMultiplier);

        _targetStats.TakeDamage(totalMagicDamage);

        CheckCanApplyAilments(_targetStats, _fireDamage, _iceDamage, _lightningDamage);
    }
}
