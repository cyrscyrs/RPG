using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.SceneManagement;

/// <summary>
/// 热更入口：**只在进入主菜单时**问一次远端的 version.json。
/// 发现有新内容（dataVersion 比本地大）就先弹个窗：“有可更新内容” + 一个「更新」按钮，
/// 玩家按了才去更新 Addressables 目录并下载新的内容包 —— 不会在游戏里自己闷声下载。
///
/// 设计原则是「失败不影响进游戏」：
///   - 没配地址 / 网络不通 / JSON 解析失败 → 打一条警告，继续用本地内容；
///   - 远端要求更高的客户端版本 → 只把状态标成 RequiresAppUpdate（要不要强制更新由你决定），不硬卡；
///   - 更新失败也照常进游戏，下次启动再试。
///
/// 不用往场景里放东西：启动后自动生成一个常驻对象（见 Bootstrap），弹窗 UI 也是运行时搭的。
/// 左上角那个调试浮层默认关了（UpdateSettings.showDebugOverlay），需要时再打开。
///
/// 远端文件夹（建议用 GitHub Releases 或 Pages 托管）长这样：
///   release/version.json
///   release/<BuildTarget>/catalog_xxx.json / .hash / *.bundle     ← Addressables 的 ServerData 产物
/// </summary>
public class UpdateManager : MonoBehaviour
{
    public static UpdateManager instance;

    public enum UpdateState
    {
        Idle,               // 还没开始
        CheckingManifest,   // 正在拉 version.json
        UpToDate,           // 已是最新
        UpdateAvailable,    // 有可更新内容，等玩家在弹窗上确认
        RequiresAppUpdate,  // 远端内容需要更新的客户端
        UpdatingCatalog,    // 正在更新 Addressables 目录
        Downloading,        // 正在下载内容
        Updated,            // 更新完成
        Skipped,            // 没配更新源 / 主动跳过
        Failed              // 出错（continueOnFailure 时会继续用本地内容）
    }

    private const string LocalDataVersionKey = "hotfix.dataVersion";

    /// <summary>提示弹窗的 Canvas 排序，比游戏里的 UI 高，确保盖在最上面。</summary>
    private const int PromptSortingOrder = 200;

    private UpdateSettings settings;

    private Coroutine checkRoutine;
    private Coroutine updateRoutine;
    private Coroutine cacheRoutine;
    private UpdatePromptUI prompt;

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public string StatusText { get; private set; } = "未开始";
    public float DownloadProgress { get; private set; }
    public VersionManifest LastManifest { get; private set; }

    /// <summary>状态变了就回调一次，方便接 UI（比如主菜单上的「检查更新中…」）。</summary>
    public event Action OnStateChanged;

    /// <summary>本地记录的热更内容版本（放在 PlayerPrefs，写起来是原子的）。</summary>
    public static int LocalDataVersion
    {
        get => PlayerPrefs.GetInt(LocalDataVersionKey, 0);
        private set
        {
            PlayerPrefs.SetInt(LocalDataVersionKey, value);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("[UpdateManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<UpdateManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        settings = UpdateSettings.Current;

        // 只在「进主菜单」的时候检查更新（游戏里的场景不查，也不会再弹任何东西）
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 游戏本来就是从主菜单启动的话，sceneLoaded 已经过去了，这里补一次
        if (settings.checkInMainMenu && IsMenuScene(SceneManager.GetActiveScene().name))
            CheckForUpdates();
    }

    /// <summary>进主菜单就查一次；进别的场景（游戏里）什么都不做。</summary>
    private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        if (!IsMenuScene(_scene.name))
        {
            // 万一更新途中玩家切走了（主菜单点了开始游戏），把弹窗收掉
            prompt?.Hide();
            return;
        }

        if (settings.checkInMainMenu)
            CheckForUpdates();
    }

    private bool IsMenuScene(string _sceneName)
        => !string.IsNullOrEmpty(settings.menuSceneName) && _sceneName == settings.menuSceneName;

    /// <summary>
    /// 本地已有的热更资产放到 Start 里加载：
    /// 这个对象是运行时创建的，Awake 里启动的协程有时不会被驱动到，Start 里稳。
    /// </summary>
    private void Start()
    {
        StartCoroutine(Tuning.LoadHotfixAsync());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (prompt != null)
            Destroy(prompt.gameObject);

        if (instance == this)
            instance = null;
    }

    #region 对外接口（也给调试浮层用）

    /// <summary>查一次远端 version.json。发现新内容只会弹窗问，不会自己下载。</summary>
    public void CheckForUpdates()
    {
        if (checkRoutine != null)
            StopCoroutine(checkRoutine);

        checkRoutine = StartCoroutine(CheckRoutine());
    }

    /// <summary>玩家在弹窗上按了「更新」之后才真正开始下载。失败后重试也是调它。</summary>
    public void StartUpdate()
    {
        if (updateRoutine != null)
            StopCoroutine(updateRoutine);

        updateRoutine = StartCoroutine(ApplyUpdateRoutine());
    }

    /// <summary>清掉已下载的内容缓存，下次会重新下。</summary>
    public void ClearDownloadedContent()
    {
        if (cacheRoutine != null)
            StopCoroutine(cacheRoutine);

        cacheRoutine = StartCoroutine(ClearCacheRoutine());
    }

    #endregion

    private IEnumerator CheckRoutine()
    {
        DownloadProgress = 0f;

        if (string.IsNullOrWhiteSpace(settings.manifestUrl))
        {
            SetState(UpdateState.Skipped, "没配 version.json 地址，直接用本地内容");
            yield break;
        }

        SetState(UpdateState.CheckingManifest, "正在检查更新…");

        // 1) 拉 version.json（带时间戳防 CDN 缓存）
        using (UnityWebRequest request = UnityWebRequest.Get(AppendCacheBuster(settings.manifestUrl)))
        {
            request.timeout = Mathf.Max(1, Mathf.RoundToInt(settings.timeoutSeconds));

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"拉取 version.json 失败：{request.error}");
                yield break;
            }

            try
            {
                LastManifest = JsonUtility.FromJson<VersionManifest>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Fail("version.json 解析失败：" + e.Message);
                yield break;
            }

            if (LastManifest == null)
            {
                Fail("version.json 是空的");
                yield break;
            }
        }

        // 2) 客户端太旧：只标状态，要不要强制更新交给上层决定
        if (IsRemoteClientRequirementHigher(LastManifest.minClientVersion))
        {
            SetState(UpdateState.RequiresAppUpdate,
                $"远端内容要求客户端 {LastManifest.minClientVersion}，当前是 {Application.version}，请更新游戏本体");
            yield break;
        }

        // 3) 已经是最新：顺手把热更过的调参表加载进来
        if (LastManifest.dataVersion <= LocalDataVersion)
        {
            SetState(UpdateState.UpToDate, $"内容已是最新（{LocalDataVersion}）");
            yield return Tuning.LoadHotfixAsync();
            yield break;
        }

        // 4) 有新内容：只弹窗问一下，等玩家按「更新」再下载
        SetState(UpdateState.UpdateAvailable,
            $"有可更新内容（远端 {LastManifest.dataVersion}，本地 {LocalDataVersion}）");

        ShowUpdatePrompt();
    }

    /// <summary>真正下载内容的流程：玩家在弹窗上按了「更新」才会走到这里。</summary>
    private IEnumerator ApplyUpdateRoutine()
    {
        if (LastManifest == null)
            yield break;

        SetState(UpdateState.UpdatingCatalog, $"正在更新内容目录（版本 {LastManifest.dataVersion}）");

        AsyncOperationHandle<List<string>> catalogCheck = Addressables.CheckForCatalogUpdates(false);
        yield return catalogCheck;

        List<string> catalogsToUpdate = catalogCheck.Status == AsyncOperationStatus.Succeeded ? catalogCheck.Result : null;
        Addressables.Release(catalogCheck);

        if (catalogsToUpdate != null && catalogsToUpdate.Count > 0)
        {
            AsyncOperationHandle<List<IResourceLocator>> updateCatalog = Addressables.UpdateCatalogs(catalogsToUpdate, false);
            yield return updateCatalog;

            bool catalogOk = updateCatalog.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(updateCatalog);

            if (!catalogOk)
            {
                Fail("更新 Addressables 目录失败", true);
                yield break;
            }
        }

        // 5) 预下载（配了标签才做；没配就等真正用到那个资源时按需下载）
        if (!string.IsNullOrWhiteSpace(settings.downloadLabel))
        {
            SetState(UpdateState.Downloading, "正在计算需要下载的大小…");

            AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(settings.downloadLabel);
            yield return sizeHandle;

            long downloadSize = sizeHandle.Status == AsyncOperationStatus.Succeeded ? sizeHandle.Result : 0L;
            Addressables.Release(sizeHandle);

            if (downloadSize > 0)
            {
                AsyncOperationHandle download = Addressables.DownloadDependenciesAsync(settings.downloadLabel, false);

                while (!download.IsDone)
                {
                    DownloadProgress = download.GetDownloadStatus().Percent;
                    StatusText = $"正在下载新内容… {DownloadProgress * 100f:0}%（{downloadSize / 1024f / 1024f:0.#} MB）";
                    prompt?.ShowProgress("正在更新", StatusText);
                    OnStateChanged?.Invoke();
                    yield return null;
                }

                bool downloadOk = download.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(download);

                if (!downloadOk)
                {
                    Fail("下载新内容失败", true);
                    yield break;
                }
            }

            DownloadProgress = 1f;
        }

        // 6) 记下这次更新到的版本，并换上热更下来的调参表
        LocalDataVersion = LastManifest.dataVersion;

        yield return Tuning.ReloadHotfixAsync();

        SetState(UpdateState.Updated, $"热更完成（{LocalDataVersion}）");

        // 更新完成：提示一下再自己收起来
        prompt?.ShowResult("更新完成", $"已更新到内容版本 {LocalDataVersion}。", 2.5f);
    }

    /// <summary>发现新内容时弹的那个窗：一句提示 + 一个「更新」按钮。</summary>
    private void ShowUpdatePrompt()
    {
        if (prompt == null)
        {
            prompt = UpdatePromptUI.Create(settings.popupFont, PromptSortingOrder);
            prompt.transform.SetParent(transform, false);
        }

        int remoteVersion = LastManifest != null ? LastManifest.dataVersion : 0;
        string note = LastManifest != null ? LastManifest.note : null;

        prompt.Show("有可更新内容",
            string.IsNullOrWhiteSpace(note)
                ? $"发现新的游戏内容（版本 {remoteVersion}），点下面的按钮开始更新。"
                : note,
            "更新",
            StartUpdate);
    }

    private IEnumerator ClearCacheRoutine()
    {
        SetState(UpdateState.Idle, "正在清理下载缓存…");

        if (string.IsNullOrWhiteSpace(settings.downloadLabel))
        {
            bool cleared = Caching.ClearCache();
            LocalDataVersion = 0;
            SetState(UpdateState.Idle, cleared ? "缓存已清理（下次会重新下载）" : "缓存清理失败（可能还有内容在用）");
            yield break;
        }

        // 用带 autoReleaseHandle 的重载：只传 key 的那个是旧的 void 重载
        AsyncOperationHandle<bool> handle = Addressables.ClearDependencyCacheAsync(settings.downloadLabel, false);
        yield return handle;

        bool ok = handle.Status == AsyncOperationStatus.Succeeded;

        if (handle.IsValid())
            Addressables.Release(handle);

        LocalDataVersion = 0;
        SetState(UpdateState.Idle, ok ? "缓存已清理（下次会重新下载）" : "缓存清理失败（可能还有内容在用）");
    }

    #region 小工具

    private void SetState(UpdateState _state, string _status)
    {
        State = _state;
        StatusText = _status;

        if (_state != UpdateState.Downloading)
            DownloadProgress = 0f;

        Debug.Log($"[热更] {_state}：{_status}");

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 出错。_allowPrompt = true（更新过程中的失败）时会在弹窗上显示错误并提供「重试」；
    /// 检查阶段的失败只记日志 —— 那种情况玩家本来也没要求更新，不该弹窗打扰。
    /// </summary>
    private void Fail(string _message, bool _allowPrompt = false)
    {
        SetState(UpdateState.Failed, settings.continueOnFailure ? _message + "（已跳过，用本地内容继续）" : _message);

        if (_allowPrompt)
            prompt?.Show("更新失败", _message, "重试", StartUpdate);
    }

    /// <summary>远端的 minClientVersion 比当前客户端高吗。</summary>
    private static bool IsRemoteClientRequirementHigher(string _minClientVersion)
    {
        if (string.IsNullOrWhiteSpace(_minClientVersion))
            return false;

        if (!Version.TryParse(_minClientVersion, out Version required))
            return false;

        if (!Version.TryParse(Application.version, out Version current))
            return false;

        return current < required;
    }

    private static string AppendCacheBuster(string _url)
    {
        string separator = _url.Contains("?") ? "&" : "?";

        return _url + separator + "t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    #endregion

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>开发期的调试浮层。只在编辑器和 Development Build 里编译进来。</summary>
    private void OnGUI()
    {
        if (settings == null || !settings.showDebugOverlay)
            return;

        GUILayout.BeginArea(new Rect(12f, 12f, 360f, 168f), GUI.skin.box);

        GUILayout.Label($"[热更] {State}   本地内容版本 {LocalDataVersion}");
        GUILayout.Label(StatusText ?? string.Empty);

        if (State == UpdateState.Downloading)
            GUILayout.Label($"下载进度 {DownloadProgress * 100f:0}%");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("检查更新"))
            CheckForUpdates();

        if (GUILayout.Button("清缓存"))
            ClearDownloadedContent();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
#endif
}
