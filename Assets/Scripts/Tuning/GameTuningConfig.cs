using UnityEngine;

/// <summary>
/// 战斗 / 属性相关的可调系数（装备成长那部分在 EquipmentGrowthConfig 里，不重复）。
///
/// 这些数字以前散在 CharacterStats / PlayerStats / EnemyStats 的代码里，
/// 现在集中到这个资产上：改数值不用改代码，配成 Addressables 之后还能热更。
///
/// 字段的初始值就是「没有资产时」的默认值——所以资产丢了 / 没加载到也不会崩，只是用回默认数值。
/// </summary>
[CreateAssetMenu(fileName = "Game Tuning", menuName = "Data/Game Tuning")]
public class GameTuningConfig : ScriptableObject
{
    [Header("伤害与减伤")]
    [Tooltip("护甲减伤：减伤比例 = 护甲 / (护甲 + 这个常数)。越大，护甲越不值钱")]
    public float armorConstant = 50f;

    [Tooltip("魔抗减伤：减伤比例 = 魔抗 / (魔抗 + 这个常数)")]
    public float magicResistanceConstant = 66f;

    [Tooltip("魔抗换算：1 点智力额外给多少魔抗")]
    public int intelligenceMagicResistanceScale = 3;

    [Tooltip("减伤算完至少保底给这么多伤害")]
    public int minimumDamage = 1;

    [Header("暴击")]
    [Tooltip("爆伤换算：倍率 = (爆伤 + 力量) × 这个系数")]
    public float critPowerScale = 0.01f;

    [Header("易伤")]
    [Tooltip("处于「易伤」时受到的伤害倍率")]
    public float vulnerableDamageMultiplier = 1.1f;

    [Header("点燃")]
    public float igniteDuration = 2f;

    [Tooltip("点燃每隔多久跳一次伤害")]
    public float igniteTickInterval = 0.3f;

    [Tooltip("每跳伤害 = 目标最大生命 × 这个比例 + 火伤 × 下面的系数")]
    public float igniteMaxHealthPercent = 0.001f;

    public float igniteFireDamageScale = 0.2f;

    [Header("寒冷")]
    public float chillDuration = 2f;

    [Tooltip("寒冷减速比例（0.2 = 移速 -20%）")]
    [Range(0f, 1f)] public float chillSlowPercentage = 0.2f;

    [Tooltip("寒冷期间护甲打折：护甲 × 这个系数")]
    [Range(0f, 1f)] public float chillArmorMultiplier = 0.8f;

    [Header("麻痹")]
    public float shockDuration = 4f;

    [Tooltip("麻痹额外跳的雷伤 = 雷伤 × 这个系数")]
    public float shockLightningScale = 0.1f;

    [Tooltip("麻痹时目标的闪避 +这个值")]
    public int shockEvasionBonus = 20;

    [Header("雷击连锁")]
    [Tooltip("找下一个连锁目标的搜索半径")]
    public float shockStrikeSearchRadius = 25f;

    [Tooltip("离自己这么近的敌人不算连锁目标")]
    public float shockStrikeMinDistance = 1f;

    [Header("异常状态表现")]
    [Tooltip("点燃 / 麻痹时血条闪色的间隔（秒）")]
    public float ailmentBlinkInterval = 0.3f;

    [Header("玩家")]
    [Tooltip("单次伤害超过最大生命的这个比例算「重击」：额外击退 + 屏幕震动")]
    [Range(0f, 1f)] public float playerHeavyHitHealthPercent = 0.3f;

    [Header("敌人等级")]
    [Tooltip("每升一级，各项属性按当前值的这个比例往上叠")]
    [Range(0f, 1f)] public float enemyLevelStatPercent = 0.1f;

    [Header("闪避")]
    [Tooltip("闪避率 = 闪避值 / (这个常数 + 闪避值)。常数越大，闪避越难堆（30 → 30 点闪避刚好 50%）")]
    public float dodgeConstant = 30f;

    [Header("Boss 攻击附带的负面效果")]
    [Tooltip("负面效果持续时间（秒）。同一套效果重复命中只刷新这个时间，不会叠加")]
    public float bossAttackDebuffDuration = 3f;

    [Tooltip("护甲降低比例（0.3 = 防御 -30%）")]
    [Range(0f, 1f)] public float bossAttackArmorReduction = 0.3f;

    [Tooltip("闪避降低比例（0.5 = 闪避 -50%）")]
    [Range(0f, 1f)] public float bossAttackEvasionReduction = 0.5f;

    [Tooltip("受到的回复降低比例（0.5 = 回复 -50%）")]
    [Range(0f, 1f)] public float bossAttackHealReduction = 0.5f;

    [Header("Boss 命中回血")]
    [Tooltip("攻击命中玩家时给自己回的基础血量")]
    public float bossHitHealBase = 100f;

    [Tooltip("再按已损失生命的这个比例额外回血（0.05 = 5%）")]
    [Range(0f, 1f)] public float bossHitHealLostHealthPercent = 0.05f;

    [Header("Boss 法术召唤")]
    [Tooltip("Boss 放法术时召唤出来的怪物等级")]
    public int bossSummonLevel = 5;
}
