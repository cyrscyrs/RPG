using System.Collections;
using UnityEngine;

/// <summary>
/// 会周期性移动的墙体：在「初始位置」和「初始位置 + 位移」两点之间来回移动，可以设置速度和两端停留时间。
///
/// 编辑时会在 Scene 视图里画出移动轨迹（线 + 两端轮廓 + 方向箭头），方便对着地图摆位置；
/// 游玩时轨迹不会出现——Gizmos 只在 Scene 视图里画，Game 视图和打包后的游戏里都看不到，
/// 脚本也不会生成任何实际的线条/网格对象。
///
/// 摆放方式：
/// 1. 在墙的位置新建物体，挂上本脚本 + BoxCollider2D（实心，不要勾 Is Trigger）
///    + SpriteRenderer 摆墙的图；
/// 2. 本脚本会自动要求并配置 Rigidbody2D 为 Kinematic（无重力、带动插值），
///    这是让移动中的墙能正常挡住 / 推动玩家、不会穿模的关键；
/// 3. Inspector 里填 moveOffset（相对墙体自身朝向的位移，例如 (3,0) = 向墙的右边移动 3 格），
///    然后看 Scene 视图里的轨迹是否是你想要的范围；
/// 4. ⚠ 不要把这面墙做成 Tilemap 的一部分：Tilemap 的 Collider 是所有格子共用的一个组件，
///    没法只让某一块动起来，要在 Tilemap 上单独做一个物体。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingWall : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("从初始位置出发的位移，方向相对墙体自身（(3,0) = 向墙的右边移动 3 个单位）")]
    [SerializeField] private Vector2 moveOffset = new Vector2(3f, 0f);
    [Tooltip("移动速度（单位/秒）")]
    [Min(0.01f)]
    [SerializeField] private float moveSpeed = 1.5f;
    [Tooltip("到达一端后停留多久再往回走，0 = 不停顿")]
    [SerializeField] private float waitTime = 0.5f;
    [Tooltip("游戏开始后先等多久再动，可以用来错开多面墙的节奏")]
    [SerializeField] private float startDelay = 0f;

    [Header("编辑时显示（游戏里永远看不到）")]
    [Tooltip("在 Scene 视图里始终显示移动轨迹；不勾就是只在选中这面墙时显示")]
    [SerializeField] private bool alwaysShowPath = true;
    [Tooltip("播放模式下是否也显示轨迹。只影响 Scene 视图，Game 视图和打包后的游戏永远看不到")]
    [SerializeField] private bool showPathWhilePlaying = false;
    [Tooltip("轨迹颜色")]
    [SerializeField] private Color pathColor = new Color(0.25f, 0.9f, 1f, 1f);
    [Tooltip("轨迹上画多少条刻度线（方向感），0 = 只画一条线和两端的方块轮廓")]
    [Range(0, 20)]
    [SerializeField] private int pathMarks = 6;

    /// <summary>每到达一端触发一次，参数 true 表示到的是「终点」那一头。</summary>
    public System.Action<bool> onReachedEndpoint;

    private Rigidbody2D rb;

    private Vector2 startPoint;
    private Vector2 endPoint;
    private bool pathCached;

    private bool headingToEnd = true;
    private bool canMove = true;
    private float waitTimer;

    #region 生命周期

    /// <summary>在编辑器里第一次挂上这个脚本时，把刚自动加上的 Rigidbody2D 配成 Kinematic。</summary>
    private void Reset()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = false;   // 墙体是实心的，要能挡住玩家
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        EnsureKinematicBody();
        CachePath();
    }

    private void EnsureKinematicBody()
    {
        if (rb == null)
        {
            Debug.LogWarning($"MovingWall「{name}」没有 Rigidbody2D，这面墙不会移动也不会推动玩家。" +
                             "请加一个 Rigidbody2D 并把 Body Type 设为 Kinematic。", this);
            canMove = false;
            return;
        }

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"MovingWall「{name}」的 Rigidbody2D 不是 Kinematic，已自动改成 Kinematic，" +
                             "否则这面墙会受重力掉下去。", this);

            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        rb.gravityScale = 0f;
    }

    private void CachePath()
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;

        startPoint = origin;
        endPoint = origin + (Vector2)transform.TransformDirection(moveOffset);
        pathCached = true;

        if (moveOffset.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"MovingWall「{name}」的 moveOffset 是 0，这面墙不会移动。", this);
            canMove = false;
        }

        waitTimer = startDelay;
    }

    private void FixedUpdate()
    {
        if (rb == null || !canMove)
            return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 target = headingToEnd ? endPoint : startPoint;
        Vector2 next = Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime);

        // 用 MovePosition 而不是直接改 transform：这样墙才会正确地挡住 / 推动玩家
        rb.MovePosition(next);

        if ((next - target).sqrMagnitude > 0.000001f)
            return;

        bool arrivedAtEnd = headingToEnd;

        headingToEnd = !headingToEnd;
        waitTimer = waitTime;

        onReachedEndpoint?.Invoke(arrivedAtEnd);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        waitTime = Mathf.Max(0f, waitTime);
        startDelay = Mathf.Max(0f, startDelay);
    }

    #endregion

    #region 给别的脚本用

    /// <summary>起点（编辑模式下就是当前位置）。</summary>
    public Vector2 GetStartPoint() => pathCached ? startPoint : (Vector2)transform.position;

    /// <summary>终点。</summary>
    public Vector2 GetEndPoint() => pathCached
        ? endPoint
        : (Vector2)transform.position + (Vector2)transform.TransformDirection(moveOffset);

    /// <summary>0 = 在起点，1 = 在终点。</summary>
    public float Progress
    {
        get
        {
            Vector2 a = GetStartPoint();
            Vector2 ab = GetEndPoint() - a;

            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr < 0.000001f)
                return 0f;

            return Mathf.Clamp01(Vector2.Dot((Vector2)transform.position - a, ab) / lengthSqr);
        }
    }

    /// <summary>暂停 / 继续移动，可以接拉杆、机关。</summary>
    public void SetMoving(bool _moving) => canMove = _moving;

    /// <summary>让它停一会儿再继续（比 SetMoving 更常用）。</summary>
    public void PauseFor(float _seconds) => waitTimer = Mathf.Max(waitTimer, _seconds);

    #endregion

    #region 编辑时的轨迹显示

    private void OnDrawGizmos()
    {
        if (alwaysShowPath)
            DrawPath();
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysShowPath)
            DrawPath();
    }

    /// <summary>只画在 Scene 视图里，Game 视图和打包后的游戏都不会有。</summary>
    private void DrawPath()
    {
        // 编辑时用当前摆放位置实时算轨迹，拖动墙体轨迹会跟着动
        if (Application.isPlaying && !showPathWhilePlaying)
            return;

        Vector2 start = GetStartPoint();
        Vector2 end = GetEndPoint();
        Vector2 direction = end - start;
        float length = direction.magnitude;

        Gizmos.color = pathColor;

        // 主线 + 两端端点
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.12f);
        Gizmos.DrawWireSphere(end, 0.12f);

        if (length > 0.001f)
        {
            Vector2 dir = direction / length;
            Vector2 normal = new Vector2(-dir.y, dir.x);

            // 沿路径画刻度，一眼能看出移动方向和覆盖范围
            for (int i = 1; i <= pathMarks; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)(pathMarks + 1));
                Gizmos.DrawLine(point - normal * 0.12f, point + normal * 0.12f);
            }

            // 两端的箭头：因为是往返移动，所以两头都画
            DrawArrowHead(end, -dir, normal);
            DrawArrowHead(start, dir, normal);
        }

        // 两端各画一个墙体轮廓，方便对齐地图（用 Collider 的大小）
        Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.35f);

        Vector3 size = GetFootprintSize();
        Vector3 centerOffset = GetFootprintCenterOffset();

        Gizmos.DrawWireCube((Vector3)start + centerOffset, size);
        Gizmos.DrawWireCube((Vector3)end + centerOffset, size);

        // 播放时再标出墙现在的位置
        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position, 0.08f);
        }
    }

    private void DrawArrowHead(Vector2 _tip, Vector2 _direction, Vector2 _normal)
    {
        const float arrowLength = 0.35f;
        const float arrowWidth = 0.18f;

        Gizmos.DrawLine(_tip, _tip + _direction * arrowLength + _normal * arrowWidth);
        Gizmos.DrawLine(_tip, _tip + _direction * arrowLength - _normal * arrowWidth);
    }

    private Vector3 GetFootprintSize()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();

        if (box == null)
            return new Vector3(0.5f, 0.5f, 0.1f);

        Vector3 scale = transform.lossyScale;

        return new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), 0.1f);
    }

    private Vector3 GetFootprintCenterOffset()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();

        if (box == null)
            return Vector3.zero;

        Vector3 scale = transform.lossyScale;

        return new Vector3(box.offset.x * scale.x, box.offset.y * scale.y, 0f);
    }

    #endregion
}
