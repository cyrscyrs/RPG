using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour,ISaveManager
{
    public static GameManager instance;

    private Transform player;

    [SerializeField] private Checkpoint[] checkpoints;
    [SerializeField] private string closestCheckpointId;

    [Header("Lost currency")]
    [SerializeField] private GameObject lostCurrencyPrefab;
    public int lostCurrencyAmount;
    [SerializeField] private float lostPositionX;
    [SerializeField] private float lostPositionY;

    private void Awake()
    {
        // 出现重复实例时把旧的那个销毁，然后立刻把 static 指向自己。
        // 少了后面那句赋值，静态字段会继续指着一个已经销毁的对象。
        if (instance != null && instance != this)
            Destroy(instance.gameObject);

        instance = this;
    }

    private void Start()
    {
        checkpoints = FindObjectsOfType<Checkpoint>();

        player = PlayerManager.instance.player.transform;
    }
    public void RestartScene()
    {
        SaveManager.instance.SaveGame();
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    public void LoadData(GameData _data) => StartCoroutine(LoadWithDelay(_data));

    private IEnumerator LoadWithDelay(GameData _data)
    {
        yield return new WaitForSeconds(.1f);

        LoadCheckpoints(_data);

        // 走门换场景：落在“通回出发场景”的那扇门那里；普通读档 / 死亡重开才回存档点
        bool arrivedThroughDoor = ArriveThroughDoor();

        LoadClosestCheckpoint(_data, !arrivedThroughDoor);
        LoadLostCurrency(_data);
    }

    private void LoadCheckpoints(GameData _data)
    {
        foreach (KeyValuePair<string, bool> pair in _data.checkpoints)
        {
            if (!pair.Value)
                continue;

            Checkpoint checkpoint = FindCheckpointById(pair.Key);

            if (checkpoint != null)
                checkpoint.ActivateCheckpoint();
        }
    }

    private void LoadClosestCheckpoint(GameData _data, bool _movePlayer = true)
    {
        if (string.IsNullOrEmpty(_data.closestCheckpointId))
            return;

        closestCheckpointId = _data.closestCheckpointId;

        // 走门进来时只恢复「最新存档点」这条记录，不把角色拽过去
        if (!_movePlayer)
            return;

        Checkpoint checkpoint = FindCheckpointById(closestCheckpointId);

        if (checkpoint != null && player != null)
            player.position = checkpoint.GetRespawnPosition();
    }

    /// <summary>
    /// 如果这次场景加载是「走门」过来的，就把角色放到目标场景里对应的门那里。
    /// 返回 false 表示不是走门（普通读档 / 死亡重开），交给 LoadClosestCheckpoint 处理。
    /// </summary>
    private bool ArriveThroughDoor()
    {
        // 不管找不找得到门，都要把这次的传送信息清掉，免得漏到下一次读档
        if (!SceneTransition.Consume(out string fromScene, out string targetSpawnId))
            return false;

        if (player == null)
            return false;

        SceneDoor arrivalDoor = FindArrivalDoor(fromScene, targetSpawnId);

        if (arrivalDoor == null)
            return false;

        player.position = arrivalDoor.ArrivalPosition;
        return true;
    }

    /// <summary>
    /// 在目标场景里找角色的落点：
    /// 1. 出发的那扇门明确指定了 targetSpawnId 就按 id 找；
    /// 2. 否则找这个场景里「通回出发场景」的那扇门（门是成对的，一般都能对上）。
    /// </summary>
    private SceneDoor FindArrivalDoor(string _fromScene, string _targetSpawnId)
    {
        SceneDoor[] doors = FindObjectsOfType<SceneDoor>(true);

        if (!string.IsNullOrEmpty(_targetSpawnId))
        {
            foreach (SceneDoor door in doors)
            {
                if (door != null && door.SpawnId == _targetSpawnId)
                    return door;
            }
        }

        foreach (SceneDoor door in doors)
        {
            if (door != null && door.TargetSceneName == _fromScene)
                return door;
        }

        return null;
    }

    /// <summary>玩家在存档点按 W 保存时调用：把这个存档点记成最新存档点。</summary>
    public void SetLatestCheckpoint(Checkpoint _checkpoint)
    {
        if (_checkpoint == null)
            return;

        closestCheckpointId = _checkpoint.ResolveId();
    }

    /// <summary>最新存档点的位置。没存过档（或存档点已经不在了）就返回 false。</summary>
    public bool TryGetLatestCheckpointPosition(out Vector3 _position)
    {
        _position = Vector3.zero;

        Checkpoint checkpoint = FindCheckpointById(closestCheckpointId);

        if (checkpoint == null)
            return false;

        _position = checkpoint.GetRespawnPosition();
        return true;
    }

    private Checkpoint FindCheckpointById(string _id)
    {
        if (checkpoints == null || string.IsNullOrEmpty(_id))
            return null;

        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint != null && checkpoint.ResolveId() == _id)
                return checkpoint;
        }

        return null;
    }

    private void LoadLostCurrency(GameData _data)
    {
        lostCurrencyAmount = _data.lostCurrencyAmount;
        lostPositionX = _data.lostPositionX;
        lostPositionY = _data.lostPositionY;   // 以前这里误写成 lostPositionX，Y 一直是旧值

        if (lostCurrencyAmount > 0)
        {
            GameObject newLostCurrency = Instantiate(lostCurrencyPrefab, new Vector3(lostPositionX, lostPositionY), Quaternion.identity);
            newLostCurrency.GetComponent<LostCurrencyController>().currency = lostCurrencyAmount;
        }

        lostCurrencyAmount = 0;
    }

    public void SaveData(ref GameData _data)
    {
        // 场景刚起来时可能有人在自己的 Start 里就存档（比如 BountyBoard.Start），
        // 那时本脚本的 Start 还没跑到，player 是 null。直接不写，
        // 免得 NRE 把「上次的存档位置 / 所在场景」这些字段阵掉。
        if (player == null)
            return;

        _data.lostCurrencyAmount = lostCurrencyAmount;
        _data.lostPositionX = player.position.x;
        _data.lostPositionY = player.position.y;

        // 最新存档点：优先用玩家按 W 记下的那个，没有才退回「离玩家最近的已点亮存档点」
        if (string.IsNullOrEmpty(closestCheckpointId))
        {
            Checkpoint nearest = FindClosestCheckpoint();

            if (nearest != null)
                closestCheckpointId = nearest.ResolveId();
        }

        _data.closestCheckpointId = closestCheckpointId;

        // 记下这次存档在哪个场景：主菜单「继续游戏」要用它回到正确的场景
        _data.lastSceneName = SceneManager.GetActiveScene().name;
        
        foreach(Checkpoint cp in checkpoints)
        {
            // 注意：这里不能 _data.checkpoints.Clear()。字典里还有别的场景的存档点，
            // 清空重写只会保留当前场景的，一进门换场景就会把别的场景点亮过的存档点抹掉。
            if (cp != null && cp.activationStatus)
                _data.checkpoints[cp.ResolveId()] = true;
        }
    }

    private Checkpoint FindClosestCheckpoint()
    {
        float closestDistance = Mathf.Infinity;
        Checkpoint closestCheckpoint = null;

        foreach (var checkpoint in checkpoints)
        {
            if (checkpoint.activationStatus == false) continue;

            float distanceToCheckpoint = Vector2.Distance(player.position, checkpoint.transform.position);

            if (distanceToCheckpoint < closestDistance)
            {
                closestDistance = distanceToCheckpoint;
                closestCheckpoint = checkpoint;
            }
        }

        return closestCheckpoint;
    }

    public void PauseGame(bool _pause)
    {
        if (_pause)
            Time.timeScale = 0;
        else
            Time.timeScale = 1;
    }

}
