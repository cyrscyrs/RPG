using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    [SerializeField] private string fileName;
    [SerializeField] private bool encryptData = false;

    /// <summary>存档槽位数量（主菜单的「选择存档」界面按这个数量显示）。</summary>
    public const int SlotCount = 3;

    private const string ActiveSlotKey = "save.activeSlot";

    private const string LegacyMigratedKey = "save.legacyMigrated";

    /// <summary>一个槽位的摘要：存档界面拿它显示「创建时间 / 最近保存 / 货币」。</summary>
    public struct SlotInfo
    {
        public bool hasSave;
        public string createdAt;
        public string lastSavedAt;
        public int currency;
        public string sceneName;
    }

    /// <summary>
    /// 当前槽位（0 起）。做成静态 + 存 PlayerPrefs 是有原因的：
    /// 换场景会新建一个 SaveManager，只存实例字段的话，玩家在主菜单选的槽位一进游戏就丢了，
    /// 游戏里的存档 / 读档会全部跑回 1 号槽位。
    /// </summary>
    public static int ActiveSlot
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
        private set
        {
            PlayerPrefs.SetInt(ActiveSlotKey, Mathf.Clamp(value, 0, SlotCount - 1));
            PlayerPrefs.Save();
        }
    }

    private GameData gameData;
    private List<ISaveManager> saveManagers;
    private FileDataHandler dataHandler;

    // 场景这一帧的 Start 都跑完了没。有的脚本（BountyBoard.Start）会在场景刚起来时就存档，
    // 那时别的管理器还没 Start，写出来的数据是缺的（lastSceneName / 背包都可能没有）。
    private bool sceneReady;
    private bool saveRequestedBeforeReady;

    /// <summary>
    /// 别的脚本（比如 BountyBoard.Start）可能在 SaveManager.Start 之前就调 SaveGame，
    /// 所以这里按需再找一次，避免 saveManagers 还是 null。
    /// </summary>
    private List<ISaveManager> SaveManagers
    {
        get
        {
            if (saveManagers == null)
                saveManagers = FindAllSaveManagers();

            return saveManagers;
        }
    }

    [ContextMenu("Delete save file")]
    public void DeleteSavedData()
    {
        DeleteSlot(ActiveSlot);
    }

    #region 存档槽位

    /// <summary>槽位对应的文件名：save.cjj → save.slot0.cjj / save.slot1.cjj …</summary>
    public string FileNameForSlot(int _slot)
    {
        string baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        string extension = System.IO.Path.GetExtension(fileName);

        return $"{baseName}.slot{Mathf.Clamp(_slot, 0, SlotCount - 1)}{extension}";
    }

    /// <summary>切到某个槽位：之后所有读档 / 存档都走它。</summary>
    public void SelectSlot(int _slot)
    {
        ActiveSlot = _slot;
        RefreshDataHandler();

        // 菜单里「继续游戏」要靠 PeekSavedSceneName 决定进哪个场景，这里顺手把数据也读一遍
        gameData = dataHandler.Load();

        Debug.Log($"存档：切到 {ActiveSlot + 1} 号槽位（{FileNameForSlot(ActiveSlot)}）");
    }

    /// <summary>槽位里有没有存档文件（坏档也算有，能不能读由 GetSlotInfo 判断）。</summary>
    public bool HasSavedDataInSlot(int _slot)
    {
        if (_slot < 0 || _slot >= SlotCount)
            return false;

        return System.IO.File.Exists(SlotPath(_slot));
    }

    /// <summary>任意槽位有存档没有。主菜单的「继续游戏」按钮用它决定显不显示。</summary>
    public bool HasAnySave()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (HasSavedDataInSlot(i))
                return true;
        }

        return false;
    }

    /// <summary>读一个槽位的摘要（创建时间 / 最近保存 / 货币 / 场景），给存档界面用。</summary>
    public SlotInfo GetSlotInfo(int _slot)
    {
        SlotInfo info = new SlotInfo();

        if (!HasSavedDataInSlot(_slot))
            return info;

        GameData data = CreateHandler(_slot).Load();

        // 文件在、但解出来是 null（坏档）：按「无存档」显示，免得给个点进去就崩的槽位
        if (data == null)
            return info;

        info.hasSave = true;
        info.createdAt = data.createdAt;
        info.lastSavedAt = data.lastSavedAt;
        info.currency = data.currency;
        info.sceneName = data.lastSceneName;

        // 老存档没记时间：退回文件自己的修改时间，总比显示空白强
        if (string.IsNullOrEmpty(info.createdAt) || string.IsNullOrEmpty(info.lastSavedAt))
        {
            string fallback = System.IO.File.GetLastWriteTime(SlotPath(_slot)).ToString("yyyy-MM-dd HH:mm");

            if (string.IsNullOrEmpty(info.createdAt))
                info.createdAt = fallback;

            if (string.IsNullOrEmpty(info.lastSavedAt))
                info.lastSavedAt = fallback;
        }

        return info;
    }

    /// <summary>清空某个槽位（新游戏 / 覆盖存档时用）。</summary>
    public void DeleteSlot(int _slot)
    {
        if (_slot < 0 || _slot >= SlotCount)
            return;

        CreateHandler(_slot).Delete();

        if (_slot == ActiveSlot)
            gameData = null;

        Debug.Log($"存档：已清空 {_slot + 1} 号槽位");
    }

    private string SlotPath(int _slot)
        => System.IO.Path.Combine(Application.persistentDataPath, FileNameForSlot(_slot));

    private FileDataHandler CreateHandler(int _slot)
        => new FileDataHandler(Application.persistentDataPath, FileNameForSlot(_slot), encryptData);

    private void RefreshDataHandler()
        => dataHandler = CreateHandler(ActiveSlot);

    /// <summary>
    /// 老版本只有一个 save.cjj：第一次跑新版本时把它当成 1 号槽位的存档（只复制，不删原文件），
    /// 否则玩家更新完会以为存档没了。
    /// </summary>
    private void MigrateLegacySave()
    {
        // ⚠ 每个场景都有自己的 SaveManager，而这里判断的是「槽位 1 空了没」——
        // 不加这道一次性的标记，玩家在 1 号槽位开新游戏（槽位变空）时，
        // 一进游戏就会把老存档又搬回来。
        if (PlayerPrefs.GetInt(LegacyMigratedKey, 0) == 1)
            return;

        PlayerPrefs.SetInt(LegacyMigratedKey, 1);
        PlayerPrefs.Save();

        string legacyPath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        string slot0Path = SlotPath(0);

        if (!System.IO.File.Exists(legacyPath) || System.IO.File.Exists(slot0Path))
            return;

        try
        {
            System.IO.File.Copy(legacyPath, slot0Path);
            Debug.Log($"存档：把老的单存档迁移成了 1 号槽位（{FileNameForSlot(0)}）");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存档：迁移老存档失败：{e}");
        }
    }

    #endregion

    private void Awake()
    {
        // 出现重复实例时把旧的那个销毁，然后立刻把 static 指向自己。
        // 少了后面那句赋值，静态字段会继续指着一个已经销毁的对象。
        if (instance != null && instance != this)
            Destroy(instance.gameObject);

        instance = this;

        // 老版本的单文件存档先迁移成 1 号槽位，再把「当前槽位」的读取器建好
        MigrateLegacySave();
        RefreshDataHandler();
    }

    private void Start()
    {
        RefreshDataHandler();

        //Invoke(nameof(LoadGame), .5f);
        LoadGame();

        StartCoroutine(MarkSceneReadyAfterStarts());
    }

    /// <summary>等这一帧所有 Start 跑完，把「场景就绪」标上，并补上场景刚起来时被推迟的那次存档。</summary>
    private IEnumerator MarkSceneReadyAfterStarts()
    {
        yield return null;

        sceneReady = true;

        if (saveRequestedBeforeReady)
        {
            saveRequestedBeforeReady = false;
            SaveGame();
        }
    }

    public void NewGame()
    {
        // 开新档：创建时间和最近保存时间都从现在算起
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        gameData = new GameData();
        gameData.createdAt = now;
        gameData.lastSavedAt = now;
    }

    public void LoadGame()
    {
        gameData = dataHandler.Load();

        if(this.gameData == null)
        {
            Debug.Log("没有存档，无法加载");
            NewGame();
        }

        foreach(ISaveManager saveManage in SaveManagers)
        {
            // 单个存档组件报错不能把后面的人全拖下水：以前这里一个异常就会中断循环，
            // 结果是同一次读档里排在前面的对象读到了、后面的（玩家 / 存档点 / 技能树）没读到，
            // 看起来就成了「读档结果和进门时不一样」。
            try
            {
                saveManage.LoadData(gameData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"读档时「{Describe(saveManage)}」报错，已跳过它继续读：\n{e}");
            }
        }
    }

    public void SaveGame()
    {
        if (dataHandler == null)
            RefreshDataHandler();

        // 读档之前就被调到（比如别的脚本的 Start 里存档）：这时还没数据可写，
        // 直接返回，免得把已有存档覆盖成空的
        if (gameData == null)
        {
            Debug.Log("存档数据还没准备好，这次 SaveGame 先跳过");
            return;
        }

        // 场景还没就绪（有人在自己的 Start 里就存档）：先记一笔，
        // 等这一帧 Start 全部跑完再写一份完整的，免得把 lastSceneName / 背包 写丢
        if (!sceneReady)
        {
            saveRequestedBeforeReady = true;
            return;
        }

        // 记一下这次保存的时间（存档槽位界面要显示）
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        if (string.IsNullOrEmpty(gameData.createdAt))
            gameData.createdAt = now;

        gameData.lastSavedAt = now;

        foreach(ISaveManager saveManager in SaveManagers)
        {
            try
            {
                saveManager.SaveData(ref gameData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"存档时「{Describe(saveManager)}」报错，已跳过它继续存：\n{e}");
            }
        }

        dataHandler.Save(gameData);
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private List<ISaveManager> FindAllSaveManagers()
    {
        // includeInactive = true：技能树、没激活的墙体/面板上的存档组件经常是关着的，
        // 默认的 FindObjectsOfType 会跳过它们，导致这些数据读不到、也存不上。
        IEnumerable<ISaveManager> saveManagers = FindObjectsOfType<MonoBehaviour>(true).OfType<ISaveManager>();

        return new List<ISaveManager>(saveManagers);
    }

    /// <summary>报错信息里给这个存档组件一个能认出来的名字。</summary>
    private static string Describe(ISaveManager _manager)
    {
        if (_manager is Component c && c != null)
            return c.GetType().Name + " on " + c.gameObject.name;

        return _manager == null ? "<null>" : _manager.GetType().Name;
    }


    /// <summary>
    /// 只看一眼存档里记的是哪个场景，不做完整读档（主菜单「继续游戏」要用）。
    /// 没存档、或者老存档没记这个字段时返回 null。
    /// </summary>
    public string PeekSavedSceneName()
    {
        // 直接读选中的那个槽位：菜单里可能刚 SelectSlot 过，
        // 不能拿上面缓存下来的 gameData 凑合
        GameData data = CreateHandler(ActiveSlot).Load();

        return data != null ? data.lastSceneName : null;
    }

    /// <summary>任意槽位里有存档没有（主菜单的「继续游戏」按钮用它决定显不显示）。</summary>
    public bool HasSavedData() => HasAnySave();

}
