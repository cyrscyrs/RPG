using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName = "MainScene";
    [SerializeField] private GameObject continueButton;
    [SerializeField] UI_FadeScreen fadeScreen;


    private void Start()
    {
        Invoke(nameof(SetContinueButton), .1f);
        //SetContinueButton();
    }

    private void SetContinueButton()
    {
        if (SaveManager.instance.HasSavedData() == false)
            continueButton.SetActive(false);
    }

    public void ContinueGame()
    {
        // 存档里记着玩家最后一次存档在哪个场景，就回哪个场景；
        // 进去之后 GameManager 会把角色放到存档点（closestCheckpointId）。
        StartCoroutine(LoadSceneWithFadeEffect(1.5f, ResolveContinueScene()));
    }
    public void NewGame()
    {
        SaveManager.instance.DeleteSavedData();
        StartCoroutine(LoadSceneWithFadeEffect(1.5f, sceneName));
    }

    /// <summary>
    /// 「继续游戏」要进的场景：存档里记的那个场景。
    /// 没有存档 / 老存档没这个字段 / 场景已不在 Build Settings 里，就退回默认场景。
    /// </summary>
    private string ResolveContinueScene()
    {
        string saved = SaveManager.instance != null ? SaveManager.instance.PeekSavedSceneName() : null;

        if (!string.IsNullOrEmpty(saved) && Application.CanStreamedLevelBeLoaded(saved))
        {
            Debug.Log($"继续游戏：回到存档场景「{saved}」");
            return saved;
        }

        Debug.Log($"继续游戏：存档里没有可用场景（'{saved}'），退回「{sceneName}」");
        return sceneName;
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    IEnumerator LoadSceneWithFadeEffect(float _delay, string _targetScene)
    {
        fadeScreen.FadeOut();

        yield return new WaitForSeconds(_delay);

        SceneManager.LoadScene(_targetScene);
    }
}
