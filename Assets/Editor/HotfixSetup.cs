using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// 热更配置的一键初始化 / 打包工具。
///
/// 菜单「Tools/热更/初始化热更配置」会做四件事（都幂等，重复执行安全）：
///   1. 生成 Resources/Tuning/GameTuning.asset（战斗调参表，随客户端发的兜底）；
///   2. 生成 Resources/Update/UpdateSettings.asset（热更地址等本地配置）；
///   3. 建（或复用）Addressables 组「Hotfix Content」：Build/Load 都走 Remote，
///      把调参表按 key = GameTuning 放进去，并打上标签 hotfix；
///   4. 打开 Addressables 的 Build Remote Catalog（不然客户端拉不到新目录）。
///
/// 菜单「Tools/热更/构建热更内容」会跑一次 Addressables 打包，产出 ServerData/，
/// 并写一份 version.json 模板，方便直接传到 GitHub Releases / Pages。
/// </summary>
public static class HotfixSetup
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string TuningFolder = ResourcesFolder + "/Tuning";
    private const string TuningPath = TuningFolder + "/GameTuning.asset";
    private const string UpdateFolder = ResourcesFolder + "/Update";
    private const string UpdateSettingsPath = UpdateFolder + "/UpdateSettings.asset";

    private const string GroupName = "Hotfix Content";
    private const string HotfixLabel = "hotfix";
    private const string TuningAddress = Tuning.AddressableKey;

    private const string EquipmentGrowthPath = "Assets/Data/EquipmentGrowthConfig.asset";
    private const string EquipmentGrowthAddress = "EquipmentGrowth";

    /// <summary>
    /// 热更内容托管在哪儿。这里用 GitHub Releases 的 latest 别名：
    /// 客户端烘进去的地址固定不变，内容随新 release 更新，每次推包不用改客户端。
    ///
    /// ⚠ Release 里的资源是平铺的（不能带目录），所以地址里不能有 [BuildTarget]；
    /// 对应的 Remote.BuildPath 也必须去掉 [BuildTarget]，产物才会平铺在 ServerData/ 下。
    /// </summary>
    private const string RemoteLoadPath = "https://github.com/cyrscyrs/RPG/releases/latest/download";

    private const string RemoteBuildPath = "ServerData";

    /// <summary>
    /// 本机模拟用的更新源：双击项目根目录的 serve-hotfix.bat 起一个静态服务，
    /// 就把 ServerData 用这个地址暴露出来。菜单「Tools/热更/更新源：切到本机」会把地址换成它。
    /// </summary>
    private const string LocalUpdateHost = "http://127.0.0.1:8000";

    /// <summary>工程根目录（Assets 的上一级）。</summary>
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    /// <summary>
    /// 远端 catalog 的固定文件名（拼成 catalog_client.json / catalog_client.hash）。
    /// Addressables 默认拿「构建时刻的时间戳」当 catalog 名，客户端里烘死的 URL
    /// 只在那一次发布里存在，下次发新包名字就变了 → 客户端 404；固定成常量后就一直有效。
    /// ⚠ 这个名字一旦发过版就不能再改，否则老客户端同样会 404。
    /// </summary>
    private const string StableCatalogVersion = "client";

    #region 初始化配置

    [MenuItem("Tools/热更/初始化热更配置")]
    private static void SetupHotfix()
    {
        EnsureFolders();

        GameTuningConfig tuning = CreateAssetIfMissing<GameTuningConfig>(TuningPath);
        UpdateSettings updateSettings = CreateAssetIfMissing<UpdateSettings>(UpdateSettingsPath);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

        if (settings == null)
        {
            Debug.LogError("热更配置：拿不到 Addressables 设置（com.unity.addressables 没装好？）。");
            return;
        }

        // 1) 打开远程目录：客户端才能发现新内容
        settings.BuildRemoteCatalog = true;

        // 1.1) 固定 catalog 文件名（见 StableCatalogVersion 的说明）：
        //      这是让 releases/latest 能一直生效的关键，不然每次发版客户端都会 404。
        settings.OverridePlayerVersion = StableCatalogVersion;

        // 1.2) Remote 路径改成「平铺 + GitHub Releases」：
        //      Release 的资源不能带目录，Build/Load 都不能留 [BuildTarget]，
        //      否则拼出来的 URL 在 GitHub 上会 404。
        SetRemoteProfileValues(settings);

        // 1.5) 编辑器里换成「Use Asset Database (fastest)」：
        // 默认的「Use Existing Build」要先 Build 过才能跑，没打包的话 Addressables 取资源会一直等
        int fastModeIndex = -1;

        for (int i = 0; i < settings.DataBuilders.Count; i++)
        {
            if (settings.DataBuilders[i] != null && settings.DataBuilders[i].name.Contains("Fast"))
            {
                fastModeIndex = i;
                break;
            }
        }

        // Unity 默认把 BuildScriptFastMode 放在列表第一个，名字匹配不到就退回 0
        if (fastModeIndex < 0 && settings.DataBuilders.Count > 0)
            fastModeIndex = 0;

        if (fastModeIndex >= 0)
        {
            settings.ActivePlayModeDataBuilderIndex = fastModeIndex;
            Debug.Log($"热更配置：编辑器 Play Mode Script 已切换到「{settings.DataBuilders[fastModeIndex].name}」");
        }
        else
        {
            Debug.LogWarning("热更配置：没找到 Play Mode Script 列表，请手动在 Addressables 的 Groups 窗口里选「Use Asset Database (fastest)」。");
        }

        // 2) 建/复用热更组，路径走 Remote
        AddressableAssetGroup group = settings.FindGroup(GroupName);

        if (group == null)
        {
            group = settings.CreateGroup(GroupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();

        if (schema != null)
        {
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            // 只改内存字段不会自动落盘：不显式标脏的话，磁盘上的 schema 还是 Local 路径
            EditorUtility.SetDirty(schema);
        }

        EditorUtility.SetDirty(group);

        // 3) 可热更的数值资产进组：都打上 hotfix 标签（客户端按标签预下载）
        AddToHotfixGroup(settings, group, TuningPath, TuningAddress);
        AddToHotfixGroup(settings, group, EquipmentGrowthPath, EquipmentGrowthAddress);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        // 4) 把可热更的调参表路径和节点写进 UpdateSettings？不需要——UpdateSettings 只管「去哪拿更新」。
        EditorUtility.SetDirty(tuning);
        EditorUtility.SetDirty(updateSettings);

        Debug.Log(
            "热更配置：初始化完成。\n" +
            $"- 调参表：{TuningPath}（key = {TuningAddress}，标签 = {HotfixLabel}，已在「{GroupName}」组里）\n" +
            $"- 热更设置：{UpdateSettingsPath}（manifestUrl 填 {RemoteLoadPath}/version.json）\n" +
            $"- Remote 路径（已按 GitHub Releases 配好）：BuildPath = {RemoteBuildPath}（平铺），LoadPath = {RemoteLoadPath}\n" +
            $"- 远端目录文件名已固定：catalog_{StableCatalogVersion}.json / catalog_{StableCatalogVersion}.hash\n" +
            "接下来：\n" +
            "   1. 菜单「Tools/热更/构建热更内容」打包，产出平铺的 ServerData/；\n" +
            "   2. 把 ServerData/* 作为资源传到一个新 Release，例如：gh release create data-v1 --title \"热更内容 v1\" ServerData/*；\n" +
            "   3. 以后每次更新都发新 release，version.json 里的 dataVersion 记得 +1。");
    }

    #endregion

    #region 打包

    [MenuItem("Tools/热更/构建热更内容")]
    private static void BuildHotfixContent()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("热更打包：没有 Addressables 设置，先执行「Tools/热更/初始化热更配置」。");
            return;
        }

        if (!settings.BuildRemoteCatalog)
        {
            Debug.LogWarning("热更打包：Build Remote Catalog 没开，客户端会拉不到新目录。已自动打开。");
            settings.BuildRemoteCatalog = true;
        }

        // 每次打包都兜一遍：中途手动改过设置的话拉回来，免得打出一个时间戳名字的目录。
        if (settings.OverridePlayerVersion != StableCatalogVersion)
        {
            Debug.LogWarning($"热更打包：catalog 文件名是「{settings.OverridePlayerVersion}」，已强制拉回「{StableCatalogVersion}」（releases/latest 靠它稳定）。");
            settings.OverridePlayerVersion = StableCatalogVersion;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        // 构建不会清掉上一次留下的旧 bundle（文件名带的是内容 hash，改了内容就是新名字），
        // 不清的话 ServerData 会越堆越大，发版时一堆用不上的垃圾也跟着传上去。
        PruneStaleBundles();


        if (result == null)
        {
            Debug.LogError("热更打包：打包失败（看 Console 里 Addressables 的报错）。");
            return;
        }

        // ⚠ 不能用 result.OutputPath：它指的是「玩家 build 数据目录里的 settings.json」，
        //   不是远端产物目录（往那儿写文件会报 Cannot create …/settings.json）。
        //   远端产物固定的位置就是 Remote.BuildPath。
        // 用 ProfileValueReference 解析；EvaluateString 传变量名只会原样返回字符串。
        string remoteBuildPath = settings.RemoteCatalogBuildPath.GetValue(settings);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputPath = Path.GetFullPath(Path.Combine(projectRoot, remoteBuildPath));
        Directory.CreateDirectory(outputPath);

        // 写一份 version.json 模板：dataVersion 要自己按次 +1
        // version.json 的版本号 = 「上一次构建的版本 + 1」。上一次优先看 ServerData 里已有的 version.json，
        // 其次才是本机的已更新版本 —— 只看本机的话，清过一次缓存（LocalDataVersion 归零）
        // 就会把版本号倒回去，客户端看着「版本变大不了」反而收不到更新。
        int previousDataVersion = Mathf.Max(UpdateManager.LocalDataVersion, ReadExistingDataVersion(outputPath));

        VersionManifest manifest = new VersionManifest
        {
            dataVersion = Mathf.Max(1, previousDataVersion + 1),
            minClientVersion = Application.version,
            note = ""
        };

        string manifestPath = Path.Combine(outputPath, "version.json");
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

        AssetDatabase.Refresh();

        string fileList = "";

        foreach (string file in Directory.GetFiles(outputPath))
            fileList += $"\n   - {Path.GetFileName(file)}";

        Debug.Log(
            $"热更打包完成：{outputPath}\n" +
            $"- 产出（GitHub Release 是平铺的，这一层的每个文件都要上传）：{fileList}\n" +
            $"- 远端目录名固定为 catalog_{StableCatalogVersion}.json / .hash，客户端烘的 {RemoteLoadPath}/catalog_{StableCatalogVersion}.json 永远有效；\n" +
            $"- 顺手写了一份 version.json 模板：{manifestPath}（发布前记得把 dataVersion 按次 +1）。");

        EditorUtility.RevealInFinder(outputPath);
    }

    #endregion

    #region 更新源切换（模拟热更用）

    [MenuItem("Tools/热更/更新源：切到本机（模拟热更用）")]
    private static void UseLocalUpdateHost()
    {
        SwitchUpdateHost(LocalUpdateHost, LocalUpdateHost + "/version.json",
            "本机静态服务（先双击项目根目录的 serve-hotfix.bat 把服务起起来）");
    }

    [MenuItem("Tools/热更/更新源：切回 GitHub Releases")]
    private static void UseGitHubUpdateHost()
    {
        SwitchUpdateHost(RemoteLoadPath, RemoteLoadPath + "/version.json",
            "GitHub Releases（正式发布用）");
    }

    /// <summary>
    /// 换更新源：改 Addressables 的 Remote.LoadPath（这个会被烘进客户端）+ 本地配置里的 manifestUrl。
    /// ⚠ 换完必须重新构建一次热更内容 —— catalog 里带的还是旧地址，发正式包一样要重新构建客户端。
    /// </summary>
    private static void SwitchUpdateHost(string _loadPath, string _manifestUrl, string _description)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("改更新源：没有 Addressables 设置，先执行「Tools/热更/初始化热更配置」。");
            return;
        }

        settings.profileSettings.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, _loadPath);
        EditorUtility.SetDirty(settings);

        UpdateSettings updateSettings = AssetDatabase.LoadAssetAtPath<UpdateSettings>(UpdateSettingsPath);

        if (updateSettings != null)
        {
            updateSettings.manifestUrl = _manifestUrl;
            EditorUtility.SetDirty(updateSettings);
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"热更更新源已切换：{_description}\n" +
            $"- Addressables Remote.LoadPath = {_loadPath}\n" +
            $"- UpdateSettings.manifestUrl = {_manifestUrl}\n" +
            "⚠ 换完地址要重新跑一次「Tools/热更/构建热更内容」，否则新 catalog 里还是旧地址。");
    }

    [MenuItem("Tools/热更/启动本地热更服务（模拟用）")]
    private static void StartLocalUpdateServer()
    {
        string batPath = Path.Combine(ProjectRoot, "serve-hotfix.bat");

        if (!File.Exists(batPath))
        {
            Debug.LogError($"找不到 {batPath}（项目根目录下的 serve-hotfix.bat）");
            return;
        }

        // 另开一个 cmd 窗口跑它：看得见日志，关掉那个窗口就是停服务
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
            $"/c start \"热更本地服务\" \"{batPath}\"") { UseShellExecute = false });

        Debug.Log($"已打开本地热更服务窗口：{batPath}（它就是把 ServerData 用 http://127.0.0.1:8000 暴露出来）");
    }

    #endregion

    #region 小工具

    /// <summary>
    /// 打包前先清掉上一次构建留在 ServerData 里的产物：
    /// 所有 .bundle（新构建会重新生成），以及以前时间戳命名的旧 catalog。
    /// version.json 不动（那是我们自己写的模板，清掉反而把你手改的版本号弄丢）。
    /// </summary>
    private static void PruneStaleBundles()
    {
        string folder = Path.Combine(ProjectRoot, RemoteBuildPath);

        if (!Directory.Exists(folder))
            return;

        int removed = 0;
        long freed = 0L;

        foreach (string file in Directory.GetFiles(folder, "*.bundle"))
        {
            freed += new FileInfo(file).Length;
            File.Delete(file);
            removed++;
        }

        // 旧的时间戳 catalog（比如 catalog_2026.10.11.12.59.12.json）也清掉；
        // 固定名字的 catalog_<StableCatalogVersion>.* 要留着，等下面的构建覆盖
        foreach (string file in Directory.GetFiles(folder, "catalog_*"))
        {
            string name = Path.GetFileName(file);

            if (name == $"catalog_{StableCatalogVersion}.json" || name == $"catalog_{StableCatalogVersion}.hash")
                continue;

            freed += new FileInfo(file).Length;
            File.Delete(file);
            removed++;
        }

        if (removed > 0)
            Debug.Log($"热更打包：先清掉了 {removed} 个上一次留下的产物（{freed / 1024f / 1024f:0.#} MB），避免发版时把没用的包一起传上去。");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder(TuningFolder))
            AssetDatabase.CreateFolder(ResourcesFolder, "Tuning");

        if (!AssetDatabase.IsValidFolder(UpdateFolder))
            AssetDatabase.CreateFolder(ResourcesFolder, "Update");
    }

    /// <summary>读上一次构建留在 ServerData/version.json 里的 dataVersion（没有 / 读坏了就当 0）。</summary>
    private static int ReadExistingDataVersion(string _folder)
    {
        string path = Path.Combine(_folder, "version.json");

        if (!File.Exists(path))
            return 0;

        try
        {
            VersionManifest existing = JsonUtility.FromJson<VersionManifest>(File.ReadAllText(path));
            return existing != null ? existing.dataVersion : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static T CreateAssetIfMissing<T>(string _path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(_path);

        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, _path);
        AssetDatabase.SaveAssets();

        Debug.Log($"热更配置：已生成 {_path}");

        return asset;
    }

    /// <summary>把某个资产放进热更组，设好 key 和标签（已存在就复用）。</summary>
    /// <summary>把 Remote.BuildPath / Remote.LoadPath 设成 GitHub Releases 能用的平铺方案。</summary>
    private static void SetRemoteProfileValues(AddressableAssetSettings _settings)
    {
        _settings.profileSettings.SetValue(_settings.activeProfileId,
            AddressableAssetSettings.kRemoteBuildPath, RemoteBuildPath);

        _settings.profileSettings.SetValue(_settings.activeProfileId,
            AddressableAssetSettings.kRemoteLoadPath, RemoteLoadPath);

        Debug.Log($"热更配置：Remote.BuildPath = {RemoteBuildPath}，Remote.LoadPath = {RemoteLoadPath}");

        EditorUtility.SetDirty(_settings);
    }

    private static void AddToHotfixGroup(AddressableAssetSettings _settings, AddressableAssetGroup _group, string _assetPath, string _address)
    {
        string guid = AssetDatabase.AssetPathToGUID(_assetPath);

        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"热更配置：找不到 {_assetPath}，跳过（这份数值就不会跟着热更）。");
            return;
        }

        AddressableAssetEntry entry = _settings.CreateOrMoveEntry(guid, _group, false, false);

        if (entry == null)
            return;

        entry.address = _address;
        entry.SetLabel(HotfixLabel, true, true);
    }

    #endregion
}
