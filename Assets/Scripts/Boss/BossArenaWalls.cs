using System.Collections;
using UnityEngine;

/// <summary>
/// 竞技场两侧的升降墙调度。
///
/// 两面墙本身是 MovingWall（周期往返），这里只负责掐时机：
/// - Boss 战开始前：让它们停住不动（SetMoving(false)），一直保持在起始位置；
/// - 战斗开始时：快速移动到终点（也就是 Inspector 里配的 moveOffset 那一头）并停住，封住场地；
/// - 击败 Boss 后：缓慢移动回起点，也就是下降同样的距离，然后停住。
///
/// 移动距离完全用 MovingWall 上配好的 moveOffset，不在这里重复配置；
/// 这里只给两个阶段各配一个速度，所以同一面墙可以「升得快、降得慢」。
/// 挂在 /BossFight 上，引用由 BossSceneBuilder 接好。
/// </summary>
public class BossArenaWalls : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("用来判断战斗开始和 Boss 阵亡")]
    [SerializeField] private BossFightController fight;

    [Tooltip("两侧的升降墙，顺序无所谓")]
    [SerializeField] private MovingWall[] walls = new MovingWall[0];

    [Header("开战：快速升起")]
    [Tooltip("战斗开始后等多少秒再动（普通时间）")]
    [SerializeField] private float riseDelay = 0.2f;

    [Tooltip("升起速度（单位/秒）。距离取墙自己的 moveOffset")]
    [SerializeField] private float riseSpeed = 10f;

    [Header("击败 Boss：缓慢下降")]
    [Tooltip("用真实时间计时，这样慢放期间等待时间也符合预期")]
    [SerializeField] private float descendDelay = 2.5f;

    [Tooltip("下降速度（单位/秒），慢一点才有「落闸」的感觉")]
    [SerializeField] private float descendSpeed = 1.5f;

    private bool risen;
    private bool descended;

    private void Awake()
    {
        HoldWalls();
    }

    private void Start()
    {
        // MovingWall 在 Awake 里才缓存好路径，这里再压一次确保战斗前绝对不会动
        HoldWalls();
    }

    private void HoldWalls()
    {
        if (walls == null)
            return;

        foreach (MovingWall wall in walls)
        {
            if (wall != null)
                wall.SetMoving(false);
        }
    }

    private void Update()
    {
        if (fight == null)
            return;

        if (!risen && fight.FightStarted)
        {
            risen = true;
            StartCoroutine(RiseRoutine());
        }

        if (risen && !descended && fight.BossStats != null && fight.BossStats.isDead)
        {
            descended = true;
            StartCoroutine(DescendRoutine());
        }
    }

    private IEnumerator RiseRoutine()
    {
        if (riseDelay > 0f)
            yield return new WaitForSeconds(riseDelay);

        MoveAll(true, riseSpeed);
    }

    private IEnumerator DescendRoutine()
    {
        if (descendDelay > 0f)
            yield return new WaitForSecondsRealtime(descendDelay);

        MoveAll(false, descendSpeed);
    }

    private void MoveAll(bool _toEnd, float _speed)
    {
        if (walls == null)
            return;

        foreach (MovingWall wall in walls)
        {
            if (wall != null)
                wall.MoveOnce(_toEnd, _speed);
        }
    }
}
