using UnityEngine;

/// <summary>
/// 跨场景传送时携带的一点临时信息。
///
/// <see cref="UnityEngine.SceneManagement.SceneManager.LoadScene(string)"/> 没法带参数，
/// 而门需要在「新场景加载完之后」知道自己是从哪儿来的，才能把角色放到目标场景里
/// 那扇「通回出发场景的门」那里。所以用一个静态类在切场景前后带一次，
/// 谁读谁清空，读完就归零，不会影响之后的死亡重开 / 普通读档。
/// </summary>
public static class SceneTransition
{
    /// <summary>这次场景加载是不是「走门」过来的。</summary>
    public static bool IsDoorTravel { get; private set; }

    /// <summary>出发的场景名，用来在目标场景里找「通回去」的那扇门。</summary>
    public static string FromSceneName { get; private set; }

    /// <summary>出发的门指定的目标出生点 id；留空 = 自动找通回去的那扇门。</summary>
    public static string TargetSpawnId { get; private set; }

    /// <summary>开始一次门传送。要在 LoadScene 之前调用。</summary>
    public static void BeginDoorTravel(string _fromSceneName, string _targetSpawnId)
    {
        IsDoorTravel = true;
        FromSceneName = _fromSceneName;
        TargetSpawnId = _targetSpawnId ?? string.Empty;
    }

    /// <summary>
    /// 取出并清空这次传送信息（只对紧接着的那一次场景加载有效）。
    /// 返回值：这次是不是走门过来的。
    /// </summary>
    public static bool Consume(out string _fromSceneName, out string _targetSpawnId)
    {
        _fromSceneName = FromSceneName;
        _targetSpawnId = TargetSpawnId;

        bool wasDoorTravel = IsDoorTravel;

        IsDoorTravel = false;
        FromSceneName = null;
        TargetSpawnId = null;

        return wasDoorTravel;
    }
}
