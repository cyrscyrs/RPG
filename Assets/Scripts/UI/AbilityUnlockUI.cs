using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 解锁能力时的弹窗：屏幕中间弹出「解锁新能力！/ 蹬墙跳 / 说明」，同时暂停游戏；
/// 按「确定」按钮或空格 / W / 回车 / Esc 关掉并恢复。
///
/// 挂在 Canvas 下的 AbilityUnlockPopup 上（带 UI_SelfManagedPanel，切菜单时不会被整个关掉）。
/// 拾取解锁道具时由 ItemObject 调用 Show(...)。
/// </summary>
public class AbilityUnlockUI : MonoBehaviour
{
    public static AbilityUnlockUI Instance;

    [Header("界面引用（预制体里已经接好）")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI abilityNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button closeButton;

    [Header("文案")]
    [SerializeField] private string title = "解锁新能力！";

    [Header("关闭按键")]
    [Tooltip("弹窗刚出现的这段时间内不接受按键，免得拾取时按着的方向键顺手把它关了")]
    [SerializeField] private float inputGuardTime = 0.25f;

    [SerializeField] private KeyCode[] closeKeys =
    {
        KeyCode.Space, KeyCode.W, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape
    };

    private bool pausedByUs;
    private float openTime;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        // 玩家去开别的菜单时把自己收掉，不然会卡在暂停里
        UI_SelfManagedPanel.OnOtherMenuOpened += CloseIfOpen;

        if (windowRoot != null)
            windowRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        UI_SelfManagedPanel.OnOtherMenuOpened -= CloseIfOpen;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen || Time.unscaledTime - openTime < inputGuardTime)
            return;

        foreach (KeyCode key in closeKeys)
        {
            if (Input.GetKeyDown(key))
            {
                Close();
                return;
            }
        }
    }

    private void CloseIfOpen()
    {
        if (IsOpen)
            Close();
    }

    #endregion

    #region 显示 / 关闭

    /// <summary>弹出某个能力解锁的提示。</summary>
    public void Show(ItemData_Ability _ability)
    {
        if (_ability == null)
            return;

        Show(_ability.GetAbilityDisplayName(), _ability.GetAbilityDescription());
    }

    public void Show(string _abilityName, string _description)
    {
        if (windowRoot == null)
            return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (titleText != null) titleText.text = title;
        if (abilityNameText != null) abilityNameText.text = _abilityName;
        if (descriptionText != null) descriptionText.text = _description;

        windowRoot.SetActive(true);

        openTime = Time.unscaledTime;

        // 普通 UI 打开声（7）
        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(7, null);
        SetPaused(true);
    }

    public void Close()
    {
        if (windowRoot == null)
            return;

        windowRoot.SetActive(false);
        SetPaused(false);
    }

    private void SetPaused(bool _paused)
    {
        if (GameManager.instance == null)
            return;

        if (_paused && !pausedByUs && Time.timeScale > 0f)
        {
            GameManager.instance.PauseGame(true);
            pausedByUs = true;
        }
        else if (!_paused && pausedByUs)
        {
            GameManager.instance.PauseGame(false);
            pausedByUs = false;
        }
    }

    #endregion
}
