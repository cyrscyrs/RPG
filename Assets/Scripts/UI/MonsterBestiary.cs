using System.Collections.Generic;
using UnityEngine;

/// <summary>图鉴里的一个怪物条目。</summary>
[System.Serializable]
public class MonsterEntry
{
    [Tooltip("存档用的怪物 ID。留空 = 自动用敌人预制体的名字，一般留空就行")]
    public string monsterId;

    [Tooltip("图鉴里显示的名字。留空 = 用预制体名（会自动去掉 Enemy_ 前缀）")]
    public string displayName;

    [TextArea(2, 5)]
    [Tooltip("点开图片放大后显示的文字描述")]
    public string description;

    [Tooltip("敌人预制体：拖进来就会自动取它的图片和掉落物")]
    public GameObject enemyPrefab;

    [Tooltip("图鉴里的图片。留空 = 自动用敌人预制体上的 SpriteRenderer 图")]
    public Sprite icon;

    [Tooltip("掉落物。留空 = 自动用敌人预制体上 ItemDrop 的 possibleDrop")]
    public ItemData[] drops;

    [Tooltip("勾上后：没击杀过时显示为 ？？？（图鉴的迷雾效果）")]
    public bool hideUntilFirstKill;
}

/// <summary>
/// 怪物图鉴的数据部分：怪物列表 + 每种怪的累计击杀数。
///
/// 击杀数由 EnemyStats 死亡时调用 <see cref="NotifyKill"/> 累计，并通过 ISaveManager 存进存档，
/// 所以退出游戏再进来击杀数还在（开新游戏会清零）。
/// 界面的部分在 MonsterBestiaryUI 里。
/// </summary>
public class MonsterBestiary : MonoBehaviour, ISaveManager
{
    public static MonsterBestiary instance;

    [Tooltip("图鉴里的怪物列表。可以直接用菜单「Tools/怪物图鉴/从敌人预制体自动填充」生成")]
    [SerializeField] private List<MonsterEntry> monsters = new List<MonsterEntry>();

    // 用忽略大小写的字典，避免大小写不一致导致击杀数对不上
    private readonly Dictionary<string, int> kills = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

    public int MonsterCount => monsters != null ? monsters.Count : 0;

    private void Awake()
    {
        if (instance != null && instance != this)
            Destroy(instance.gameObject);
        else
            instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public MonsterEntry GetMonster(int _index)
    {
        if (monsters == null || _index < 0 || _index >= monsters.Count)
            return null;

        return monsters[_index];
    }

    #region 怪物信息（留空的字段会自动从敌人预制体上取）

    /// <summary>算出这个条目用的怪物 ID。</summary>
    public static string ResolveId(MonsterEntry _entry)
    {
        if (_entry == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(_entry.monsterId))
            return CleanName(_entry.monsterId);

        if (_entry.enemyPrefab != null)
            return CleanName(_entry.enemyPrefab.name);

        return CleanName(_entry.displayName);
    }

    public static string GetDisplayName(MonsterEntry _entry)
    {
        if (_entry == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(_entry.displayName))
            return _entry.displayName;

        if (_entry.enemyPrefab != null)
            return _entry.enemyPrefab.name.Replace("Enemy_", "");

        return _entry.monsterId;
    }

    /// <summary>图鉴用的图片：没手填就用敌人预制体上第一个有图的 SpriteRenderer。</summary>
    public static Sprite GetIcon(MonsterEntry _entry)
    {
        if (_entry == null)
            return null;

        if (_entry.icon != null)
            return _entry.icon;

        if (_entry.enemyPrefab == null)
            return null;

        // 先看根物体上的图（敌人本体的图一般在这里，避免拿到血条之类的子物体图）
        SpriteRenderer own = _entry.enemyPrefab.GetComponent<SpriteRenderer>();

        if (own != null && own.sprite != null)
            return own.sprite;

        foreach (SpriteRenderer sr in _entry.enemyPrefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr != null && sr.sprite != null)
                return sr.sprite;
        }

        return null;
    }

    /// <summary>掉落物：没手填就用敌人预制体上 ItemDrop 的 possibleDrop。</summary>
    public static ItemData[] GetDrops(MonsterEntry _entry)
    {
        if (_entry == null)
            return null;

        if (_entry.drops != null && _entry.drops.Length > 0)
            return _entry.drops;

        if (_entry.enemyPrefab == null)
            return null;

        ItemDrop drop = _entry.enemyPrefab.GetComponent<ItemDrop>();

        if (drop == null)
            drop = _entry.enemyPrefab.GetComponentInChildren<ItemDrop>(true);

        return drop != null ? drop.possibleDrop : null;
    }

    /// <summary>去掉运行时克隆出来的 "(Clone)" 后缀，让击杀统计能和图鉴条目对上。</summary>
    public static string CleanName(string _name) => string.IsNullOrEmpty(_name) ? string.Empty : _name.Replace("(Clone)", "").Trim();

    #endregion

    #region 击杀统计

    /// <summary>敌人死亡时由 EnemyStats 调用。</summary>
    public static void NotifyKill(string _monsterId)
    {
        if (instance != null)
            instance.RegisterKill(_monsterId);
    }

    public void RegisterKill(string _monsterId)
    {
        string id = CleanName(_monsterId);

        if (string.IsNullOrEmpty(id))
            return;

        kills[id] = GetKills(id) + 1;
    }

    public int GetKills(string _monsterId)
    {
        string id = CleanName(_monsterId);

        if (string.IsNullOrEmpty(id))
            return 0;

        return kills.TryGetValue(id, out int count) ? count : 0;
    }

    public int GetKills(MonsterEntry _entry) => GetKills(ResolveId(_entry));

    #endregion

    #region 存档

    public void LoadData(GameData _data)
    {
        kills.Clear();

        if (_data == null || _data.monsterKills == null)
            return;

        foreach (KeyValuePair<string, int> pair in _data.monsterKills)
            kills[pair.Key] = pair.Value;
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        if (_data.monsterKills == null)
            _data.monsterKills = new SerializableDictionary<string, int>();

        _data.monsterKills.Clear();

        foreach (KeyValuePair<string, int> pair in kills)
            _data.monsterKills[pair.Key] = pair.Value;
    }

    #endregion
}
