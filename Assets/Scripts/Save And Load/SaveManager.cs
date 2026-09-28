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
        if (instance != null)
            Destroy(instance.gameObject);
        else
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
            saveManage.LoadData(gameData);
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
            saveManager.SaveData(ref gameData);
        }

        dataHandler.Save(gameData);
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private List<ISaveManager> FindAllSaveManagers()
    {
        IEnumerable<ISaveManager> saveManagers = FindObjectsOfType<MonoBehaviour>().OfType<ISaveManager>();

        return new List<ISaveManager>(saveManagers);
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
