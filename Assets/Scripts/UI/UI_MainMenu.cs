using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class UI_MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName = "MainScene";
    [SerializeField] private GameObject continueButton;
    [SerializeField] UI_FadeScreen fadeScreen;

    [Tooltip("存档界面用的字体。留空会自动用场景里已有的 TMP 字体（项目的中文字体）")]
    [SerializeField] private TMP_FontAsset saveSlotFont;

    private SaveSlotUI saveSlotUI;


    private void Start()
    {
        // 主菜单所有按钮点一下都响一声（7 = 普通 UI 点击）
        RegisterButtonClickSfx();

        Invoke(nameof(SetContinueButton), .1f);
        //SetContinueButton();
    }

    private void SetContinueButton()
    {
        if (SaveManager.instance == null)
            return;

        // 任意槽位有存档才显示「继续游戏」（没存档时点了也没东西可选）
        continueButton.SetActive(SaveManager.instance.HasSavedData());
    }

    public void ContinueGame()
    {
        // 先进「选择存档」界面，选一个有存档的槽位再读档
        OpenSaveSlots(SaveSlotUI.Mode.LoadGame);
    }
    public void NewGame()
    {
        // 同样先进存档界面：这次是选一个槽位存新档（非空要先确认覆盖）
        OpenSaveSlots(SaveSlotUI.Mode.NewGame);
    }

    /// <summary>
    /// 「继续游戏」要进的场景：存档里记的那个场景。
    /// <summary>打开存档槽位界面（没有就现建一个）。</summary>
    private void OpenSaveSlots(SaveSlotUI.Mode _mode)
    {
        if (SaveManager.instance == null)
        {
            Debug.LogError("主菜单：找不到 SaveManager，打不开存档界面。");
            return;
        }

        if (saveSlotUI == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = FindObjectOfType<Canvas>();

            saveSlotUI = SaveSlotUI.Create(canvas != null ? canvas.transform : transform, ResolveSaveSlotFont());
        }

        if (_mode == SaveSlotUI.Mode.LoadGame)
            saveSlotUI.OpenLoad(EnterGameFromSlot);
        else
            saveSlotUI.OpenNewGame(StartNewGameInSlot);
    }

    /// <summary>选中一个已有存档的槽位：读档进游戏。</summary>
    private void EnterGameFromSlot(int _slot)
    {
        SaveManager.instance.SelectSlot(_slot);

        // 存档里记着玩家最后一次存档在哪个场景，就回哪个场景；
        // 进去之后 GameManager 会把角色放到存档点（closestCheckpointId）。
        StartCoroutine(LoadSceneWithFadeEffect(1.5f, ResolveContinueScene()));
    }

    /// <summary>选中一个槽位开新档：先把该槽位的旧存档清掉（空槽位天然没事），再进游戏。</summary>
    private void StartNewGameInSlot(int _slot)
    {
        SaveManager.instance.SelectSlot(_slot);
        SaveManager.instance.DeleteSlot(_slot);

        StartCoroutine(LoadSceneWithFadeEffect(1.5f, sceneName));
    }

    /// <summary>存档界面用的字体：优先用 Inspector 里指定的，留空就借场景里已有的那个。</summary>
    private TMP_FontAsset ResolveSaveSlotFont()
    {
        if (saveSlotFont != null)
            return saveSlotFont;

        // 菜单上本来就摆着带中文字体的 TMP 文字，直接借用，省得多配一份资源引用
        TMP_Text sample = FindObjectOfType<TMP_Text>();

        return sample != null ? sample.font : TMP_Settings.defaultFontAsset;
    }

    /// <summary>给主菜单里所有按钮挂上点击音效（菜单上现有的按钮 + 以后加的都算）。</summary>
    private void RegisterButtonClickSfx()
    {
        Transform root = transform;

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            root = canvas.transform;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button != null)
                button.onClick.AddListener(AudioManager.PlayClickSfx);
        }

        Debug.Log($"主菜单：已给 {buttons.Length} 个按钮挂上点击音效（7）");
    }

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
