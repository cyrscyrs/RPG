using UnityEngine;

/// <summary>
/// 一次伤害的属性类型。只用来决定伤害数字的颜色和格式，不影响伤害数值本身。
/// None = 只扣血、不弹数字（魔法伤害拆成多段数字时，总扣血那一发用这个）。
/// </summary>
public enum DamageType
{
    Physical,
    Critical,
    Fire,
    Ice,
    Lightning,
    Magic,
    None,
}

/// <summary>各种属性的伤害数字长什么样（颜色 / 后缀），要调色改这里。</summary>
public static class DamageStyle
{
    /// <summary>物理：保持白色。</summary>
    public static readonly Color PhysicalColor = Color.white;

    /// <summary>暴击：红色。</summary>
    public static readonly Color CriticalColor = new Color(1f, 0.13f, 0.13f, 1f);

    /// <summary>火焰：橙色。</summary>
    public static readonly Color FireColor = new Color(1f, 0.5f, 0f, 1f);

    /// <summary>寒冰：#B3F7FC。</summary>
    public static readonly Color IceColor = new Color(0.7019608f, 0.9686275f, 0.9882353f, 1f);

    /// <summary>雷电：黄色（和游戏里雷击特效的颜色一致）。</summary>
    public static readonly Color LightningColor = new Color(1f, 0.9882353f, 0f, 1f);

    /// <summary>智力带来的无属性魔法伤害：#D3B6F6。</summary>
    public static readonly Color MagicColor = new Color(0.8274510f, 0.7137255f, 0.9647059f, 1f);

    /// <summary>回血：绿色。</summary>
    public static readonly Color HealColor = new Color(0.35f, 1f, 0.35f, 1f);

    /// <summary>闪避：淡灰蓝色的「Miss」。</summary>
    public static readonly Color MissColor = new Color(0.72f, 0.78f, 0.88f, 1f);

    /// <summary>攻击被闪避时弹的字。</summary>
    public const string MissText = "Miss";

    public static Color ColorOf(DamageType _type)
    {
        switch (_type)
        {
            case DamageType.Critical:
                return CriticalColor;
            case DamageType.Fire:
                return FireColor;
            case DamageType.Ice:
                return IceColor;
            case DamageType.Lightning:
                return LightningColor;
            case DamageType.Magic:
                return MagicColor;
            default:
                // 物理：保持白色
                return PhysicalColor;
        }
    }

    /// <summary>暴击数字末尾带一个感叹号，其它属性就是纯数字。</summary>
    public static string Format(int _damage, DamageType _type)
    {
        return _type == DamageType.Critical
            ? _damage + "！"
            : _damage.ToString();
    }

    /// <summary>回血数字：就是回复量本身（前面加个 + 更好认，想改就改这里）。</summary>
    public static string FormatHeal(int _amount) => "+" + _amount;
}
