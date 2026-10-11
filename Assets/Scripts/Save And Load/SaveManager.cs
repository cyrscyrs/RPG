using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    [SerializeField] private string fileName;
    [SerializeField] private bool encryptData = false;

    private GameData gameData;
    private List<ISaveManager> saveManagers;
    private FileDataHandler dataHandler;

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
        dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);
        dataHandler.Delete();
    }

    private void Awake()
    {
        // 出现重复实例时把旧的那个销毁，然后立刻把 static 指向自己。
        // 少了后面那句赋值，静态字段会继续指着一个已经销毁的对象。
        if (instance != null && instance != this)
            Destroy(instance.gameObject);

        instance = this;

        // Start 之前就可能有人用到，先把读取器建好
        if (dataHandler == null)
            dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);
    }

    private void Start()
    {
        if (dataHandler == null)
            dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);

        //Invoke(nameof(LoadGame), .5f);
        LoadGame();
    }

    public void NewGame()
    {
        gameData = new GameData();
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
            dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);

        // 读档之前就被调到（比如别的脚本的 Start 里存档）：这时还没数据可写，
        // 直接返回，免得把已有存档覆盖成空的
        if (gameData == null)
        {
            Debug.Log("存档数据还没准备好，这次 SaveGame 先跳过");
            return;
        }

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
        if (gameData != null)
            return gameData.lastSceneName;

        if (dataHandler == null)
            dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);

        GameData data = dataHandler.Load();

        return data != null ? data.lastSceneName : null;
    }

    public bool HasSavedData()
    {
        if (dataHandler == null)
            dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, encryptData);

        if (dataHandler.Load() != null)
        {
            return true;
        }

        return false;
    }

}
