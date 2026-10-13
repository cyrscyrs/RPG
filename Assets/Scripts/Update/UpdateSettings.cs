using UnityEngine;
using TMPro;

/// <summary>
/// 热更的「本地」配置，放在 Resources/Update/UpdateSettings（菜单「Tools/热更/初始化热更配置」会生成）。
///
/// 这些东西必须留在客户端本地：它是「去哪儿找更新」的起点，不能指望从远端拿（鸡生蛋问题）。
/// 没有这个资产也能跑，用的是下面的字段默认值（默认不检查更新）。
/// </summary>
[CreateAssetMenu(fileName = "Update Settings", menuName = "Data/Update Settings")]
public class UpdateSettings : ScriptableObject
{
    [Tooltip("version.json 的地址，例如 https://raw.githubusercontent.com/<用户>/<仓库>/main/release/version.json。" +
             "留空 = 跳过更新检查，直接用本地内容")]
    public string manifestUrl = "";

    [Tooltip("启动时自动检查更新")]
    public bool checkInMainMenu = true;

    [Tooltip("哪个场景算「主菜单」：只有进入这个场景时才会检查更新，游戏里不查")]
    public string menuSceneName = "MainMenu";

    [Tooltip("「有可更新内容」弹窗用的字体。留空会用 TMP 默认字体（LiberationSans，没有中文字形）")]
    public TMP_FontAsset popupFont;

    [Tooltip("要预下载的 Addressables 标签（建议给热更组打这个标签）。留空 = 不预下载，" +
             "等真正用到那个资源时按需下载")]
    public string downloadLabel = "hotfix";

    [Tooltip("单次网络请求的超时（秒）")]
    public float timeoutSeconds = 20f;

    [Tooltip("检查 / 下载失败时只报警告，继续用本地内容")]
    public bool continueOnFailure = true;

    [Tooltip("开发期显示调试浮层（状态 + 手动检查 / 清缓存）。只在编辑器或 Development Build 里显示")]
    public bool showDebugOverlay = false;

    /// <summary>Resources 下的路径（不带扩展名）。</summary>
    public const string ResourcePath = "Update/UpdateSettings";

    private static UpdateSettings cached;

    public static UpdateSettings Current
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<UpdateSettings>(ResourcePath);

            if (cached == null)
                cached = CreateInstance<UpdateSettings>();   // 没有资产就用默认值，保证不崩

            return cached;
        }
    }
}
