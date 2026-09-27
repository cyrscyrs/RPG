using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器 10 级的「剑气」：玩家攻击时向前飞出一道判定，命中的敌人受到一定比例的攻击力伤害。
/// 由 PLayerAnimationTriggers 在攻击动画事件里生成，同一道剑气对同一个敌人只结算一次。
/// </summary>
public class SwordAura : MonoBehaviour
{
    private readonly List<EnemyStats> hitTargets = new List<EnemyStats>();

    private int damage;
    private float direction;
    private float speed;
    private float radius;
    private float dieTime;

    /// <summary>生成一道剑气。伤害 = 玩家攻击力 × 配置里的比例。</summary>
    public static SwordAura Spawn(Player _player, EquipmentGrowthConfig _config)
    {
        if (_player == null || _config == null)
            return null;

        int damage = Mathf.RoundToInt(_player.stats.GetDamageValue() * _config.swordAuraDamagePercent);

        if (damage <= 0)
            return null;

        GameObject go = new GameObject("SwordAura");
        go.transform.position = _player.transform.position;

        // 剑气整体朝向跟着玩家面朝方向（这样挂上去的特效也会翻转）
        int facing = _player.facingDir;
        go.transform.rotation = facing < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        // 有配特效就挂一个跟着剑气走
        if (_config.swordAuraPrefab != null)
        {
            GameObject visual = Instantiate(_config.swordAuraPrefab, go.transform.position, go.transform.rotation);
            visual.transform.SetParent(go.transform, true);
        }

        SwordAura aura = go.AddComponent<SwordAura>();
        aura.Setup(damage, facing, _config);

        return aura;
    }

    private void Setup(int _damage, int _facingDir, EquipmentGrowthConfig _config)
    {
        damage = _damage;
        direction = _facingDir < 0 ? -1f : 1f;
        speed = Mathf.Max(0.1f, _config.swordAuraSpeed);
        radius = Mathf.Max(0.1f, _config.swordAuraRadius);
        dieTime = Time.time + Mathf.Max(0.05f, _config.swordAuraLifeTime);
    }

    private void Update()
    {
        transform.position += Vector3.right * (direction * speed * Time.deltaTime);

        HitNearbyEnemies();

        if (Time.time >= dieTime)
            Destroy(gameObject);
    }

    private void HitNearbyEnemies()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, radius);

        foreach (Collider2D hit in colliders)
        {
            EnemyStats target = hit.GetComponent<EnemyStats>();

            if (target == null || target.isDead || hitTargets.Contains(target))
                continue;

            hitTargets.Add(target);
            target.TakeDamage(damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
