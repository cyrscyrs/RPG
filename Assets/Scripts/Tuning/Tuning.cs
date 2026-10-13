using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 调参表的统一入口。所有战斗数值都从这里取，别再往代码里写死数字。
///
/// 取值顺序：
///   1. Addressables 热更资产（key = "GameTuning"，可以放远端热更）；
///   2. Resources/Tuning/GameTuning（随客户端发的兜底资产）；
///   3. 代码里的默认值（ScriptableObject 的字段初始值）。
///
/// 后两步是保底：资产没生成、Addressables 没初始化、网络挂了 —— 都只是用回上一档的数值，不会崩。
/// </summary>
public static class Tuning
{
    /// <summary>Addressables 里的 key，和生成器里设置的一致。</summary>
    public const string AddressableKey = "GameTuning";

    /// <summary>随包发的兜底资产路径（相对 Resources）。</summary>
    public const string ResourcePath = "Tuning/GameTuning";

    /// <summary>装备成长配置（EquipmentGrowthConfig）在 Addressables 里的 key。</summary>
    public const string EquipmentGrowthKey = "EquipmentGrowth";

    private static EquipmentGrowthConfig hotfixEquipmentGrowth;
    private static AsyncOperationHandle<EquipmentGrowthConfig> hotfixEquipmentHandle;

    /// <summary>热更下来的装备成长配置（没热更到就是 null，调用方自己兜底）。</summary>
    public static EquipmentGrowthConfig EquipmentGrowthHotfix => hotfixEquipmentGrowth;

    private static GameTuningConfig cachedDefault;
    private static GameTuningConfig hotfixConfig;
    private static AsyncOperationHandle<GameTuningConfig> hotfixHandle;
    private static bool hotfixLoadStarted;

    /// <summary>
    /// 等 Addressables 多久没结果就先放弃、改用本地兜底。
    /// 编辑器里没打过 bundle、或者远端不通时，Addressables 的加载会一直挂着。
    /// </summary>
    private const float HotfixLoadTimeout = 3f;

    /// <summary>当前生效的调参表，永远不为 null。</summary>
    public static GameTuningConfig Config
    {
        get
        {
            if (hotfixConfig != null)
                return hotfixConfig;

            if (cachedDefault == null)
                cachedDefault = LoadDefault();

            return cachedDefault;
        }
    }

    /// <summary>现在用的是不是热更下来的那份。</summary>
    public static bool UsingHotfixConfig => hotfixConfig != null;

    private static GameTuningConfig LoadDefault()
    {
        GameTuningConfig fromResources = Resources.Load<GameTuningConfig>(ResourcePath);

        if (fromResources != null)
            return fromResources;

        Debug.LogWarning($"调参表没找到（Resources/{ResourcePath}），先用代码里的默认数值。" +
                         "可以用菜单「Tools/热更/初始化热更配置」生成它。");

        return ScriptableObject.CreateInstance<GameTuningConfig>();
    }

    /// <summary>
    /// 启动时调一次：把 Addressables 上的调参表（如果有）加载进来，覆盖兜底资产。
    /// 取不到就什么都不做，继续用 Resources / 默认值。
    /// </summary>
    public static IEnumerator LoadHotfixAsync()
    {
        if (hotfixLoadStarted)
            yield break;

        hotfixLoadStarted = true;

        hotfixHandle = Addressables.LoadAssetAsync<GameTuningConfig>(AddressableKey);

        float waited = 0f;

        // 注意：不能写成 yield return 一个 IEnumerator（Unity 不会把它当成子协程驱动，会直接卡住）
        while (!hotfixHandle.IsDone && waited < HotfixLoadTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!hotfixHandle.IsDone)
        {
            Debug.LogWarning($"[热更] 调参表：Addressables 在 {HotfixLoadTimeout:0.#} 秒内没返回" +
                             "（编辑器里没打 bundle、或者远端不通都会这样），先用本地数值。");
            ReleaseTimedOutLoads();
            yield break;
        }

        if (hotfixHandle.Status == AsyncOperationStatus.Succeeded && hotfixHandle.Result != null)
        {
            hotfixConfig = hotfixHandle.Result;
            Debug.Log("[热更] 调参表：已换成 Addressables 里的版本。");
        }
        else
        {
            Debug.LogWarning("[热更] 调参表：Addressables 里没取到，继续用 Resources / 默认数值。");
        }

        // 装备成长配置也一起换上（成长曲线、里程碑效果都在它里面）
        hotfixEquipmentHandle = Addressables.LoadAssetAsync<EquipmentGrowthConfig>(EquipmentGrowthKey);

        waited = 0f;

        while (!hotfixEquipmentHandle.IsDone && waited < HotfixLoadTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!hotfixEquipmentHandle.IsDone)
        {
            Debug.LogWarning("[热更] 装备成长配置：Addressables 没及时返回，继续用 Inspector 上配的那份。");
            ReleaseTimedOutLoads();
            yield break;
        }

        if (hotfixEquipmentHandle.Status == AsyncOperationStatus.Succeeded && hotfixEquipmentHandle.Result != null)
        {
            hotfixEquipmentGrowth = hotfixEquipmentHandle.Result;
            Debug.Log("[热更] 装备成长配置：已换成 Addressables 里的版本。");
        }
        else
        {
            Debug.LogWarning("[热更] 装备成长配置：Addressables 里没取到，继续用 Inspector 上配的那份。");
        }
    }

    /// <summary>超时后的收尾：把挂着的句柄放掉，并清掉「已加载过」的标记，之后还能重试。</summary>
    private static void ReleaseTimedOutLoads()
    {
        if (hotfixHandle.IsValid())
            Addressables.Release(hotfixHandle);

        if (hotfixEquipmentHandle.IsValid())
            Addressables.Release(hotfixEquipmentHandle);

        hotfixLoadStarted = false;
    }

    /// <summary>刚下载完新内容后调用：先把旧的放掉，再重新加载一遍。</summary>
    public static IEnumerator ReloadHotfixAsync()
    {
        ReleaseHotfixConfig();

        yield return LoadHotfixAsync();
    }

    /// <summary>释放热更下来的资产（之后取值会回到 Resources / Inspector 上配的那份）。</summary>
    public static void ReleaseHotfixConfig()
    {
        if (hotfixHandle.IsValid())
            Addressables.Release(hotfixHandle);

        if (hotfixEquipmentHandle.IsValid())
            Addressables.Release(hotfixEquipmentHandle);

        hotfixConfig = null;
        hotfixEquipmentGrowth = null;
        hotfixLoadStarted = false;
    }
}
