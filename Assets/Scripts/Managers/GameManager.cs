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
        if (instance != null)
            Destroy(instance.gameObject);
        else
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
        LoadClosestCheckpoint(_data);
        LoadLostCurrency(_data);
    }

    private void LoadCheckpoints(GameData _data)
    {
        foreach (KeyValuePair<string, bool> pair in _data.checkpoints)
        {
            foreach (Checkpoint checkpoint in checkpoints)
            {
                if (checkpoint.id == pair.Key && pair.Value == true)
                    checkpoint.ActivateCheckpoint();
            }
        }
    }

    private void LoadClosestCheckpoint(GameData _data)
    {
        if (_data.closestCheckpointId == null)
            return;


        closestCheckpointId = _data.closestCheckpointId;

        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (closestCheckpointId == checkpoint.id)
                player.position = checkpoint.transform.position;
        }
    }

    private void LoadLostCurrency(GameData _data)
    {
        lostCurrencyAmount = _data.lostCurrencyAmount;
        lostPositionX = _data.lostPositionX;
        lostPositionX = _data.lostPositionX;

        if (lostCurrencyAmount > 0)
        {
            GameObject newLostCurrency = Instantiate(lostCurrencyPrefab, new Vector3(lostPositionX, lostPositionY), Quaternion.identity);
            newLostCurrency.GetComponent<LostCurrencyController>().currency = lostCurrencyAmount;
        }

        lostCurrencyAmount = 0;
    }

    public void SaveData(ref GameData _data)
    {
        _data.lostCurrencyAmount = lostCurrencyAmount;
        _data.lostPositionX = player.position.x;
        _data.lostPositionY = player.position.y;

        if(FindClosestCheckpoint() != null)
            _data.closestCheckpointId = FindClosestCheckpoint().id;
        
        _data.checkpoints.Clear();

        foreach(Checkpoint cp in checkpoints)
        {
            _data.checkpoints.Add(cp.id, cp.activationStatus);
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
