using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour, ISaveManager
{
    [Header("End screen")]
    [SerializeField] private UI_FadeScreen fadeScreen;
    [SerializeField] private GameObject endText;
    [SerializeField] private GameObject restartButton;
    [Space]

    [SerializeField] private GameObject characterUI;
    [SerializeField] private GameObject skillTreeUI;
    [SerializeField] private GameObject craftUI;
    [SerializeField] private GameObject optionsUI;
    [SerializeField] private GameObject InGameUI;

    [Tooltip("怪物图鉴面板。运行时由 MonsterBestiaryUI 自动注册，也可以手动拖")]
    [SerializeField] private GameObject bestiaryUI;

    public UI_ItemToolTip itemToolTip;
    public UI_StatToolTip statToolTip;
    public UI_CraftWindow craftWindow;
    public UI_SkillToolTip skillToolTip;

    [SerializeField] private UI_VolumeSlider[] volumeSettings;

    private void Awake()
    {
        SwitchTo(skillTreeUI);

        fadeScreen.gameObject.SetActive(true);
    }
    void Start()
    {
        SwitchTo(InGameUI);

        itemToolTip.gameObject.SetActive(false);
        statToolTip.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            SwitchWithKeyTo(characterUI);

        if (Input.GetKeyDown(KeyCode.B))
            SwitchWithKeyTo(craftUI);


        if (Input.GetKeyDown(KeyCode.K))
            SwitchWithKeyTo(skillTreeUI);

        if (Input.GetKeyDown(KeyCode.O))
            SwitchWithKeyTo(optionsUI);

        if (Input.GetKeyDown(KeyCode.L) && bestiaryUI != null)
            SwitchWithKeyTo(bestiaryUI);
    }

    /// <summary>MonsterBestiaryUI 在运行时把图鉴面板注册进来，不用手动拖。</summary>
    public void SetBestiaryPanel(GameObject _panel) => bestiaryUI = _panel;

    public void SwitchTo(GameObject _menu)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            //防止淡入效果失效
            bool fadeScreen = transform.GetChild(i).GetComponent<UI_FadeScreen>() != null;

            // 自己管理显示的面板（对话、强化等）不要被菜单切换关掉，否则面板上的脚本就不跑了
            if (transform.GetChild(i).GetComponent<UI_SelfManagedPanel>() != null)
            {
                transform.GetChild(i).gameObject.SetActive(true);
                continue;
            }

            if (fadeScreen == false)
                transform.GetChild(i).gameObject.SetActive(false);
        }

        if (_menu != null)
        {
            AudioManager.instance.PlaySFX(7, null);
            _menu.SetActive(true);

            // 切到别的菜单时，让对话 / 强化这种自管理面板把自己收起来
            if (_menu != InGameUI)
                UI_SelfManagedPanel.NotifyOtherMenuOpened();
        }

        if (GameManager.instance != null)
        {
            if (_menu == InGameUI)
            {
                GameManager.instance.PauseGame(false);
            }
            else
            {
                GameManager.instance.PauseGame(true);
            }
        }

    }

    public void SwitchWithKeyTo(GameObject _menu)
    {
        if (_menu != null && _menu.activeSelf)
        {
            _menu.SetActive(false);
            CheckForInGameUI();
            return;
        }

        SwitchTo(_menu);
    }

    private void CheckForInGameUI()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            // 自己管理显示的面板不算「开着的菜单」（它们的根节点一直是激活的），要先跳过
            if (child.GetComponent<UI_SelfManagedPanel>() != null)
                continue;

            if (child.gameObject.activeSelf && child.GetComponent<UI_FadeScreen>() == null)
                return;
        }

        SwitchTo(InGameUI);
    }

    public void SwitchOnEndScreen()
    {
        fadeScreen.FadeOut();
        StartCoroutine(EndScreenCorutione());
    }

    IEnumerator EndScreenCorutione()
    {
        yield return new WaitForSeconds(1);
        endText.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        restartButton.SetActive(true);

    }

    public void RestartGameButton() => GameManager.instance.RestartScene();

    public void LoadData(GameData _data)
    {
        foreach (KeyValuePair<string, float> pair in _data.volumeSettings)
        {
            foreach (UI_VolumeSlider vs in volumeSettings)
            {
                if (pair.Key == vs.parameter)
                    vs.LoadSlider(pair.Value);
            }

        }

    }

    public void SaveData(ref GameData _data)
    {
        _data.volumeSettings.Clear();

        foreach (UI_VolumeSlider vs in volumeSettings)
        {
            _data.volumeSettings.Add(vs.parameter, vs.slider.value);
        }
    }

    public void SaveAndExit()
    {
        SaveManager.instance.SaveGame();
        Debug.Log("saved game");
        GameManager.instance.PauseGame(false);

        //SceneManager.LoadScene("MainMenu");
        StartCoroutine(LoadMainMenuWithFadeEffect(1.5f));
        //Invoke(nameof(LoadMainMenu), 1.2f);
    }

    IEnumerator LoadMainMenuWithFadeEffect(float _delay)
    {
        fadeScreen.FadeOut();
        Debug.Log("turn");
        yield return new WaitForSeconds(_delay);

        Debug.Log("load mainmenu");
        SceneManager.LoadScene("MainMenu");
    }

    public void LoadMainMenu()
    {
        Debug.Log("turn");
        SceneManager.LoadScene("MainMenu");
    }
}
