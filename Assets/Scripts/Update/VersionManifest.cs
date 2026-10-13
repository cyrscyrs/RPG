using UnityEngine;

/// <summary>
/// 远端 version.json 的内容。放在更新源（GitHub Pages / Releases / 自建服务器）上，
/// 客户端启动时拉一次，决定要不要更新内容、要不要提示更新 App。
///
/// 一个最小例子：
/// {
///     "dataVersion": 1,
///     "minClientVersion": "1.0.0",
///     "note": "调低了 boss 的攻击力"
/// }
/// </summary>
[System.Serializable]
public class VersionManifest
{
    [Tooltip("热更内容版本号：每次推热更包 +1，比本地大的时候才会去更新")]
    public int dataVersion;

    [Tooltip("这份热更内容要求的最低客户端版本。比 Application.version 高的话就提示去更新游戏本体")]
    public string minClientVersion = "";

    [Tooltip("备注，随便写，方便自己看")]
    public string note = "";
}
