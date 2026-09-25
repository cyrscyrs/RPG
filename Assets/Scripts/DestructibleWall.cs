using System.Collections;
using UnityEngine;

/// <summary>
/// 可被摧毁的墙体：被玩家攻击命中 hitsToDestroy 次（默认 3 次）后永久摧毁。
///
/// 「永久」是靠存档做出来的：摧毁状态通过 ISaveManager 写进 GameData.destroyedWalls，
/// 重新进入地图时（GameManager.RestartScene 重载场景、回主菜单后再继续游戏）墙体在
/// LoadData 里读到「已摧毁」就直接保持消失，不会复原；只有开新游戏才会重新出现。
///
/// 摆放方式：
/// 1. 在墙体位置新建一个空物体，挂上本脚本 + BoxCollider2D，里面再放 SpriteRenderer 摆出墙的样子。
///    ⚠ 不要直接挂在 Tilemap 上：Tilemap 的 Collider 是所有格子共用的一个组件，
///      而攻击判定是按「被命中的 Collider 所在物体」去找组件的，挂在 Tilemap 上无法定位到单面墙。
///    BoxCollider2D 必须和本脚本在同一个物体上。
/// 2. 右键组件标题 → Generate Wall Id 生成唯一 ID（存档认的就是它，生成后不要再改）。
/// 3. 墙体必须一开始就在场景里（和 Checkpoint 一样）。运行时才 Instantiate 出来的墙体不会自动读档。
/// </summary>
public class DestructibleWall : MonoBehaviour, ISaveManager, IHittable
{
    [Header("墙体标识")]
    [Tooltip("存档用的唯一 ID。留空会按「场景名 + 层级路径」生成一个兜底 ID，但改动层级后 ID 会变、墙体就会复活，建议用右键菜单生成 GUID。")]
    [SerializeField] private string wallId;

    [Header("耐久")]
    [Tooltip("被玩家攻击命中多少次之后摧毁")]
    [Min(1)]
    [SerializeField] private int hitsToDestroy = 3;

    [Header("表现（可选，不填也能正常用）")]
    [Tooltip("受击阶段贴图：第 N 次受击后换成 hitStageSprites[N-1]，用来做墙体逐渐开裂的效果")]
    [SerializeField] private Sprite[] hitStageSprites;
    [Tooltip("受击特效预制体")]
    [SerializeField] private GameObject hitFXPrefab;
    [Tooltip("摧毁特效预制体")]
    [SerializeField] private GameObject destroyFXPrefab;
    [Tooltip("受击音效在 AudioManager 里的下标，-1 表示不播放")]
    [SerializeField] private int hitSFXIndex = -1;
    [Tooltip("摧毁音效在 AudioManager 里的下标，-1 表示不播放")]
    [SerializeField] private int destroySFXIndex = -1;
    [Tooltip("特效物体的自动销毁时间（秒）")]
    [SerializeField] private float fxLifetime = 2f;

    [Header("存档")]
    [Tooltip("摧毁的瞬间立刻写一次存档，避免玩家在下一个存盘点之前崩溃/强退导致这面墙复活")]
    [SerializeField] private bool saveImmediatelyWhenDestroyed = true;

    /// <summary>墙体被摧毁时触发；读档还原成「已摧毁」时也会触发一次，方便联动开门、铺桥之类的机关。</summary>
    public System.Action onWallDestroyed;

    public bool IsDestroyed => isDestroyed;
    public int HitsTaken => currentHits;
    public int HitsToDestroyCount => hitsToDestroy;

    // 缓存起来：摧毁 = 关掉碰撞（能走过去）+ 关掉显示
    private Collider2D[] wallColliders;
    private SpriteRenderer[] wallRenderers;
    private SpriteRenderer mainRenderer;

    private int currentHits;
    private bool isDestroyed;

    private void Awake()
    {
        CacheWallParts();

        if (string.IsNullOrEmpty(wallId))
        {
            wallId = BuildFallbackId();

            Debug.LogWarning($"DestructibleWall「{name}」没有设置 wallId，已按层级路径生成兜底 ID：{wallId}。" +
                             "一旦这个物体的层级/名字变了，ID 就会变、墙体就会复活，请用右键菜单 Generate Wall Id 固定它。", this);
        }
    }

    /// <summary>缓存要关闭的碰撞与渲染。Awake 时调一次；TakeHit 里补一次是为了在编辑器里直接跑测试时也不报错。</summary>
    private void CacheWallParts()
    {
        wallColliders = GetComponentsInChildren<Collider2D>(true);
        wallRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        mainRenderer = GetComponent<SpriteRenderer>();
        if (mainRenderer == null)
            mainRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    #region 受击与摧毁

    /// <summary>玩家的攻击判定命中时由 PLayerAnimationTriggers.AttackTrigger() 调用。</summary>
    public void TakeHit()
    {
        if (isDestroyed)
            return;

        if (wallColliders == null)
            CacheWallParts();

        currentHits++;

        if (currentHits >= hitsToDestroy)
        {
            DestroyWall(true);
            SaveDestroyedState();
            return;
        }

        PlayHitFeedback();
    }

    /// <param name="_playFX">读档还原时传 false：状态要恢复，但不该再放一次特效和音效。</param>
    private void DestroyWall(bool _playFX)
    {
        if (isDestroyed)
            return;

        isDestroyed = true;
        currentHits = Mathf.Max(currentHits, hitsToDestroy);

        if (_playFX)
        {
            PlaySFX(destroySFXIndex);
            SpawnFX(destroyFXPrefab);
        }

        // 只关闭碰撞和显示，物体本身保持激活：
        // SaveManager 是在场景开始时扫描 ISaveManager 的，物体一旦 SetActive(false)，
        // 下次就扫描不到它、再也写不进/读不出状态了。
        foreach (Collider2D col in wallColliders)
        {
            if (col != null)
                col.enabled = false;
        }

        foreach (SpriteRenderer sr in wallRenderers)
        {
            if (sr != null)
                sr.enabled = false;
        }

        onWallDestroyed?.Invoke();
    }

    /// <summary>只有「被玩家打碎」才立刻写一次档。读档还原成已摧毁时绝不能写：那一刻 SaveManager.LoadGame
    /// 正在逐个 LoadData，回写会把还没读完的半成品状态存回存档。</summary>
    private void SaveDestroyedState()
    {
        if (saveImmediatelyWhenDestroyed && SaveManager.instance != null)
            SaveManager.instance.SaveGame();
    }

    private void PlayHitFeedback()
    {
        PlaySFX(hitSFXIndex);
        SpawnFX(hitFXPrefab);

        if (mainRenderer == null || hitStageSprites == null)
            return;

        int spriteIndex = currentHits - 1;

        if (spriteIndex < 0 || spriteIndex >= hitStageSprites.Length)
            return;

        if (hitStageSprites[spriteIndex] != null)
            mainRenderer.sprite = hitStageSprites[spriteIndex];
    }

    private void PlaySFX(int _index)
    {
        if (_index < 0 || AudioManager.instance == null)
            return;

        AudioManager.instance.PlaySFX(_index, transform);
    }

    private void SpawnFX(GameObject _fxPrefab)
    {
        if (_fxPrefab == null)
            return;

        GameObject fx = Instantiate(_fxPrefab, transform.position, Quaternion.identity);

        // 编辑器里（非播放模式）没有延迟销毁，直接删掉，避免报错
        if (Application.isPlaying)
            Destroy(fx, fxLifetime);
        else
            DestroyImmediate(fx);
    }

    #endregion

    #region 存档

    public void LoadData(GameData _data)
    {
        if (_data == null)
            return;

        EnsureWallDictionary(_data);

        // 存档里记着「这面墙已经碎了」→ 直接进入已摧毁状态，不放特效、不再回写存档
        if (_data.destroyedWalls.TryGetValue(wallId, out bool destroyed) && destroyed)
            DestroyWall(false);
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        EnsureWallDictionary(_data);

        _data.destroyedWalls[wallId] = isDestroyed;
    }

    /// <summary>destroyedWalls 是后加的字段，旧存档里可能压根没有它，这里兜底补一个空字典，避免读写时报空引用。</summary>
    private static void EnsureWallDictionary(GameData _data)
    {
        if (_data.destroyedWalls == null)
            _data.destroyedWalls = new SerializableDictionary<string, bool>();
    }

    #endregion

    #region 编辑期工具

    [ContextMenu("Generate Wall Id")]
    private void GenerateWallId() => wallId = System.Guid.NewGuid().ToString();

    /// <summary>没填 ID 时的兜底方案：场景名 + 层级路径，跨次运行稳定，至少不会每次进游戏都复活。</summary>
    private string BuildFallbackId()
    {
        string path = name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return $"{gameObject.scene.name}/{path}";
    }

    #endregion
}
