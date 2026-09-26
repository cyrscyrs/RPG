using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 易碎地面：玩家（或敌人）踩上去后，过一小段时间就会碎裂消失，踩在上面的人会直接掉下去。
///
/// 实现要点：
/// - 用 OnCollisionEnter2D 判断「站上来了」（地板是实心 Collider，玩家才踩得住），
///   只认从上往下落到这块地板上的情况，从下面顶到、侧面擦到都不算；
/// - 倒计时用 Update 里的自己的计时器，不用协程也不用 OnCollisionStay2D：
///   玩家站着不动时刚体会休眠，持续类物理回调不一定还会派发；
/// - 碎裂 = 关掉 Collider（人就掉下去了）+ 关掉显示，物体本身保持激活，方便后面复原；
/// - 倒计时结束前离开，默认会恢复原状（踩一下就退回去不会碎）。
///
/// 摆放方式：
/// 1. 在要塌的位置新建一个空物体，挂上本脚本 + BoxCollider2D（**不要**勾 Is Trigger，
///    必须是实心碰撞，玩家才站得住）+ SpriteRenderer 摆上地板的图；
/// 2. ⚠ 不要直接把这块地面做成 Tilemap 的一部分：Tilemap 的 Collider 是所有格子共用的，
///    没法只让某一块塌掉。要在 Tilemap 上面单独叠一个物体当易碎地板。
/// 3. 需要连续的易碎地面就复制这个物体，每块各自独立计时。
/// </summary>
public class FragileFloor : MonoBehaviour
{
    [Header("碎裂")]
    [Tooltip("踩上去之后多久碎裂（秒）。这段时间够玩家跑过去")]
    [SerializeField] private float crumbleDelay = 0.6f;
    [Tooltip("碎裂前离开是否恢复原状（推荐勾上：踩一下就退回去不会碎）")]
    [SerializeField] private bool resetWhenLeft = true;

    [Header("复原")]
    [Tooltip("碎裂后多少秒复原；填 0 表示永久消失，掉下去之后不会再出现")]
    [SerializeField] private float respawnDelay = 0f;

    [Header("谁能踩碎")]
    [SerializeField] private bool breakOnPlayer = true;
    [Tooltip("敌人踩上去也会碎，可以用来把敌人坑下去")]
    [SerializeField] private bool breakOnEnemy = false;

    [Header("表现（可选，不填也能正常用）")]
    [Tooltip("倒计时期间依次替换的贴图，用来做地面开裂的效果")]
    [SerializeField] private Sprite[] crumbleStageSprites;
    [Tooltip("倒计时期间闪烁的颜色，越接近碎裂闪得越快")]
    [SerializeField] private Color crumbleTint = new Color(1f, 0.75f, 0.7f, 1f);
    [Tooltip("开始碎裂（刚踩上去）时的特效预制体")]
    [SerializeField] private GameObject crumbleFXPrefab;
    [Tooltip("真正碎掉时的特效预制体")]
    [SerializeField] private GameObject breakFXPrefab;
    [Tooltip("开始碎裂的音效在 AudioManager 里的下标，-1 表示不播放")]
    [SerializeField] private int crumbleSFXIndex = -1;
    [Tooltip("碎掉的音效在 AudioManager 里的下标，-1 表示不播放")]
    [SerializeField] private int breakSFXIndex = -1;
    [Tooltip("特效物体的自动销毁时间（秒）")]
    [SerializeField] private float fxLifetime = 2f;

    /// <summary>地板真正碎掉时触发，可以用来做镜头抖动、触发机关之类。</summary>
    public System.Action onFloorBroken;

    public bool IsCrumbling => isCrumbling;
    public bool IsBroken => isBroken;

    // 站在上面的东西（玩家/敌人）。用列表是因为可能同时有多个，
    // 而且敌人会死会销毁，Update 里顺手清理掉失效的引用。
    private readonly List<Collider2D> occupants = new List<Collider2D>();

    private Collider2D[] floorColliders;
    private SpriteRenderer[] floorRenderers;
    private SpriteRenderer mainRenderer;
    private Color[] originalColors;

    private bool isCrumbling;
    private bool isBroken;
    private float crumbleTimer;
    private float respawnTimer;

    private void Awake()
    {
        CacheFloorParts();
    }

    private void CacheFloorParts()
    {
        floorColliders = GetComponentsInChildren<Collider2D>(true);
        floorRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        mainRenderer = GetComponent<SpriteRenderer>();

        if (mainRenderer == null)
            mainRenderer = GetComponentInChildren<SpriteRenderer>(true);

        originalColors = new Color[floorRenderers.Length];

        for (int i = 0; i < floorRenderers.Length; i++)
            originalColors[i] = floorRenderers[i] != null ? floorRenderers[i].color : Color.white;

        if (GetComponent<Collider2D>() == null)
            Debug.LogWarning($"FragileFloor「{name}」所在物体上没有 Collider2D，玩家踩不上来。请加一个 BoxCollider2D（不要勾 Is Trigger）。", this);
    }

    #region 检测谁踩上来了

    private void OnCollisionEnter2D(Collision2D _collision)
    {
        if (isBroken || !CanBeBrokenBy(_collision.collider))
            return;

        if (!IsLandingOnTop(_collision.collider))
            return;

        if (!occupants.Contains(_collision.collider))
            occupants.Add(_collision.collider);

        if (!isCrumbling)
            StartCrumble();
    }

    private void OnCollisionExit2D(Collision2D _collision)
    {
        occupants.Remove(_collision.collider);

        if (isCrumbling && resetWhenLeft && occupants.Count == 0)
            ResetCrumble();
    }

    /// <summary>只有脚底落在这块地板顶面附近才算「踩上来」。</summary>
    private bool IsLandingOnTop(Collider2D _other)
    {
        const float tolerance = 0.4f;

        return _other.bounds.min.y >= GetTopY() - tolerance;
    }

    private float GetTopY()
    {
        float top = float.NegativeInfinity;

        foreach (Collider2D col in floorColliders)
        {
            if (col != null && col.enabled)
                top = Mathf.Max(top, col.bounds.max.y);
        }

        return top;
    }

    private bool CanBeBrokenBy(Collider2D _other)
    {
        if (breakOnPlayer && _other.GetComponent<Player>() != null)
            return true;

        if (breakOnEnemy && _other.GetComponent<Enemy>() != null)
            return true;

        return false;
    }

    #endregion

    #region 碎裂与复原

    private void Update()
    {
        // 把已经销毁的引用清掉（比如站在上面的敌人死了）
        for (int i = occupants.Count - 1; i >= 0; i--)
        {
            if (occupants[i] == null)
                occupants.RemoveAt(i);
        }

        if (isCrumbling)
        {
            crumbleTimer -= Time.deltaTime;
            UpdateCrumbleVisual();

            if (crumbleTimer <= 0f)
                BreakFloor();

            return;
        }

        if (isBroken && respawnDelay > 0f)
        {
            respawnTimer -= Time.deltaTime;

            if (respawnTimer <= 0f)
                RespawnFloor();
        }
    }

    private void StartCrumble()
    {
        isCrumbling = true;
        crumbleTimer = crumbleDelay;

        PlaySFX(crumbleSFXIndex);
        SpawnFX(crumbleFXPrefab);
    }

    private void ResetCrumble()
    {
        isCrumbling = false;
        crumbleTimer = 0f;

        RestoreLook();
    }

    private void BreakFloor()
    {
        isCrumbling = false;
        isBroken = true;

        PlaySFX(breakSFXIndex);
        SpawnFX(breakFXPrefab);

        // 关掉碰撞 → 站在上面的人立刻掉下去；关掉显示 → 这块地面看不见了
        SetCollidersEnabled(false);
        SetRenderersEnabled(false);

        onFloorBroken?.Invoke();

        respawnTimer = respawnDelay;
    }

    private void RespawnFloor()
    {
        isBroken = false;
        respawnTimer = 0f;
        occupants.Clear();

        SetCollidersEnabled(true);
        SetRenderersEnabled(true);
        RestoreLook();
    }

    private void SetCollidersEnabled(bool _enabled)
    {
        foreach (Collider2D col in floorColliders)
        {
            if (col != null)
                col.enabled = _enabled;
        }
    }

    private void SetRenderersEnabled(bool _enabled)
    {
        foreach (SpriteRenderer sr in floorRenderers)
        {
            if (sr != null)
                sr.enabled = _enabled;
        }
    }

    private void RestoreLook()
    {
        if (floorRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < floorRenderers.Length; i++)
        {
            if (floorRenderers[i] != null)
                floorRenderers[i].color = originalColors[i];
        }
    }

    private void UpdateCrumbleVisual()
    {
        float progress = crumbleDelay > 0f ? 1f - Mathf.Clamp01(crumbleTimer / crumbleDelay) : 1f;

        // 开裂贴图：把倒计时按进度分段换图，最后一段就是最碎的那张
        if (mainRenderer != null && crumbleStageSprites != null && crumbleStageSprites.Length > 0)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(progress * crumbleStageSprites.Length), 0, crumbleStageSprites.Length - 1);

            if (crumbleStageSprites[index] != null)
                mainRenderer.sprite = crumbleStageSprites[index];
        }

        // 颜色闪烁：越接近碎裂闪得越快，给玩家「快跑」的信号
        float blink = Mathf.PingPong(Time.time * Mathf.Lerp(3f, 14f, progress), 1f);

        for (int i = 0; i < floorRenderers.Length; i++)
        {
            if (floorRenderers[i] == null)
                continue;

            floorRenderers[i].color = Color.Lerp(originalColors[i], crumbleTint, blink);
        }
    }

    #endregion

    #region 表现

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
}
