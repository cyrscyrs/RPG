using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景里的一扇门：玩家靠近时头顶显示「按 W 互动」，按 W 后画面淡出并切换到 targetSceneName 指定的场景。
///
/// 一扇门只对应一个目标场景：这个组件上只有一个 targetSceneName，填什么就去哪。
/// 想再加一扇门就去另一个场景的门，把整个 door 物体复制一份、改一下场景名即可。
/// 目标场景必须已经在 Build Settings 的 Scenes In Build 里（现在有 MainMenu / MainScene / BossScene）。
///
/// 换场景前会先存一次档：新场景的 SaveManager 会在 Start 里重新读存档，
/// 不先存的话，这次进门之前拿到的进度到新场景就没了（和 GameManager.RestartScene 一个道理）。
///
/// 提示气泡用的是和存档点一样的世界空间 Canvas：子物体里放一个叫 Prompt 的物体，
/// 里面随便写个 TextMeshProUGUI 就行，运行时会把「按 {0} 互动」的按键名填进去。
/// </summary>
public class SceneDoor : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("按 W 后前往的场景名，必须加进 Build Settings 的 Scenes In Build")]
    [SerializeField] private string targetSceneName = "";

    [Header("交互")]
    [SerializeField] private KeyCode interactKey = KeyCode.W;

    [Tooltip("玩家离多近才会显示提示")]
    [SerializeField] private float interactionRadius = 2.5f;

    [Tooltip("提示气泡。留空 = 自动找子物体里叫 Prompt 的")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("提示文字格式，{0} 会替换成按键名")]
    [SerializeField] private string promptFormat = "按 {0} 互动";

    [Tooltip("门上方显示的说明，描述这扇门通向哪儿（比如「安全屋」）。留空就按目标场景自动取一个")]
    [SerializeField] private string destinationLabel = "";

    [Header("表现")]
    [Tooltip("淡出动画的时长（秒），到点后才真正换场景")]
    [SerializeField] private float fadeDuration = 1.5f;

    [Tooltip("进门前自动存一次档。新场景的 SaveManager 会重新读存档，不存的话这次进门前的进度会丢")]
    [SerializeField] private bool saveBeforeTravel = true;

    [Tooltip("进门音效在 AudioManager 里的下标，-1 = 不播")]
    [SerializeField] private int travelSfxIndex = 49;

    [Header("到达位置")]
    [Tooltip("这扇门的唯一 id。别的门的 targetSpawnId 可以填它；留空 = 用物体名字")]
    [SerializeField] private string spawnId;

    [Tooltip("从这扇门出发时，落在目标场景里哪个出生点（填那扇门的 spawnId）。留空 = 自动落在「通回本场景」的那扇门")]
    [SerializeField] private string targetSpawnId;

    [Tooltip("从别的场景进这扇门时，角色落在哪个点。留空 = 用门的位置 + 下面的偏移")]
    [SerializeField] private Transform arrivalPoint;

    [Tooltip("没有单独放到达点时，角色相对门的落点偏移（稍微抬高一点，让角色自然落地而不是卡进地板）")]
    [SerializeField] private Vector3 arrivalOffset = new Vector3(0f, 0.5f, 0f);

    public string TargetSceneName => targetSceneName;
    public float InteractionRadius => interactionRadius;

    /// <summary>这扇门的 id：别的门的「目标出生点」会填它。</summary>
    public string SpawnId => string.IsNullOrEmpty(spawnId) ? name : spawnId;

    /// <summary>从别的场景进这扇门时，角色该站的位置。</summary>
    public Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position + arrivalOffset;

    private bool promptVisible;
    private bool travelling;
    private Collider2D interactionCollider;

    #region 生命周期

    private void Start()
    {
        if (promptRoot == null)
        {
            Transform found = transform.Find("Prompt");

            if (found != null)
                promptRoot = found.gameObject;
        }

        SetPromptVisible(false);
    }

    private void Update()
    {
        if (travelling)
        {
            SetPromptVisible(false);
            return;
        }

        // 菜单 / 对话暂停时不提示也不能触发
        if (Time.timeScale <= 0f)
        {
            SetPromptVisible(false);
            return;
        }

        Player player = PlayerManager.instance != null ? PlayerManager.instance.player : null;

        if (player == null || player.stats.isDead)
        {
            SetPromptVisible(false);
            return;
        }

        bool inRange = DistanceToPlayer(player) <= interactionRadius;

        SetPromptVisible(inRange);

        if (inRange && Input.GetKeyDown(interactKey))
            Travel();
    }

    /// <summary>
    /// 玩家到门的距离。门常常是「贴图中心在半空、底部落在地面」，
    /// 有碰撞体就按碰撞体算，站在门正下方的玩家才不会被算得很远。
    /// </summary>
    private float DistanceToPlayer(Player _player)
    {
        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider2D>();

        if (interactionCollider != null)
        {
            Vector3 closest = interactionCollider.bounds.ClosestPoint(_player.transform.position);

            return Vector2.Distance(_player.transform.position, closest);
        }

        return Vector2.Distance(_player.transform.position, transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        // 从别的场景走进来时，角色会落在这里
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(ArrivalPosition, 0.25f);
    }

    #endregion

    #region 传送

    /// <summary>按 W 时执行：淡出 → 存档 → 切场景。</summary>
    public void Travel()
    {
        if (travelling)
            return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning($"门「{name}」没有设置目标场景，先在 Inspector 里填 targetSceneName。", this);
            return;
        }

        if (targetSceneName == SceneManager.GetActiveScene().name)
        {
            Debug.LogWarning($"门「{name}」的目标场景就是当前场景（{targetSceneName}），没有意义。", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"门「{name}」的目标场景「{targetSceneName}」不在 Build Settings 里，加进去才能加载。", this);
            return;
        }

        StartCoroutine(TravelRoutine());
    }

    private IEnumerator TravelRoutine()
    {
        travelling = true;
        SetPromptVisible(false);

        // 进门音效（49）：Inspector 里没配过（-1）就用默认的进门声
        int travelSfx = travelSfxIndex >= 0 ? travelSfxIndex : 49;

        if (AudioManager.instance != null)
            AudioManager.instance.PlaySFX(travelSfx, transform);

        // 先存档：新场景的 SaveManager 会重新读存档，不存的话这次进门前的进度会丢
        if (saveBeforeTravel)
            SaveProgressBeforeTravel();

        UI_FadeScreen fade = FindObjectOfType<UI_FadeScreen>();

        if (fade != null)
        {
            fade.FadeOut();
            yield return new WaitForSeconds(fadeDuration);
        }

        // 把「从哪个场景来、要落在哪个出生点」带到新场景去。
        // 新场景的 GameManager 读到后会把角色放在对应的门那里，而不是拽去存档点。
        SceneTransition.BeginDoorTravel(SceneManager.GetActiveScene().name, targetSpawnId);

        SceneManager.LoadScene(targetSceneName);
    }

    #endregion

    /// <summary>进门前的存档。没有 SaveManager 就只警告、不挡着进门。</summary>
    private void SaveProgressBeforeTravel()
    {
        if (SaveManager.instance == null)
        {
            Debug.LogWarning($"门「{name}」想进门存档，但场景里没有 SaveManager，跳过了。", this);
            return;
        }

        SaveManager.instance.SaveGame();
        Debug.Log($"门「{name}」：进门前已存一次档。");
    }

    #region 提示

    private void SetPromptVisible(bool _visible)
    {
        if (promptVisible == _visible)
            return;

        promptVisible = _visible;

        if (promptRoot == null)
            return;

        promptRoot.SetActive(_visible);

        if (_visible)
        {
            TMPro.TextMeshProUGUI label = promptRoot.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

            if (label != null)
                label.text = BuildPromptText();
        }
    }

    /// <summary>提示文字：第一行是这扇门通向哪儿，第二行是按什么键。</summary>
    private string BuildPromptText()
    {
        string hint = string.Format(promptFormat, interactKey.ToString());
        string destination = GetDestinationLabel();

        return string.IsNullOrEmpty(destination) ? hint : destination + "\n" + hint;
    }

    /// <summary>这扇门的去向说明：优先用门上手填的，没填就按目标场景取默认叫法。</summary>
    public string GetDestinationLabel()
    {
        if (!string.IsNullOrEmpty(destinationLabel))
            return destinationLabel;

        return DefaultSceneLabel(targetSceneName);
    }

    /// <summary>
    /// 没填去向说明时，按目标场景名给的默认叫法。
    /// 想改名字：直接改这里，或者在具体某扇门的 destinationLabel 上填（会覆盖这里）。
    /// </summary>
    private static string DefaultSceneLabel(string _sceneName)
    {
        switch (_sceneName)
        {
            case "MainScene": return "主地图";
            case "BossScene": return "Boss 战场";
            case "SafeRoom": return "安全屋";
            case "stage0": return "野外地图";
            case "MainMenu": return "主菜单";
        }

        return string.IsNullOrEmpty(_sceneName) ? string.Empty : _sceneName;
    }

    #endregion
}
