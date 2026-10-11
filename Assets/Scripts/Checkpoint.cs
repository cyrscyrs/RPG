using UnityEngine;

/// <summary>
/// 存档点。
///
/// - 玩家靠近时，存档点上方显示「按 W 保存」；
/// - 按 W → 点亮这个存档点（动画 + 音效）→ 把它记成「最新存档点」→ 立刻存一次档；
/// - 之后玩家死亡、或者掉进 DeadZone，都会回到这个最新存档点的位置。
///
/// 改成手动保存后，不再「碰到就自动激活」：必须按 W 才算存过。
/// （读档时 GameManager 会调用 ActivateCheckpoint 把已点亮的存档点恢复成亮的。）
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Tooltip("存档用的 ID。在 Inspector 右键「Generate checkpoint id」生成；留空时按物体位置自动生成")]
    public string id;

    [Tooltip("是否已经点亮过（会写进存档，读档后保持点亮）")]
    public bool activationStatus;

    [Header("交互")]
    [Tooltip("玩家离多近才会出现「按 W 保存」")]
    [SerializeField] private float interactionRadius = 2.5f;

    [SerializeField] private KeyCode interactKey = KeyCode.W;

    [Tooltip("提示文字，{0} 会替换成按键名")]
    [SerializeField] private string promptFormat = "按 {0} 保存";

    [Tooltip("头顶提示，留空会自动找子物体 Prompt")]
    [SerializeField] private GameObject promptRoot;

    [Header("表现")]
    [Tooltip("保存成功时播放的音效在 AudioManager 里的下标，-1 = 不播")]
    [SerializeField] private int saveSfxIndex = 5;

    public float InteractionRadius => interactionRadius;

    private Animator anim;
    private bool promptVisible;

    private Collider2D interactionCollider;

    #region 生命周期

    private void Start()
    {
        anim = GetComponent<Animator>();

        if (promptRoot == null)
        {
            Transform found = transform.Find("Prompt");

            if (found != null)
                promptRoot = found.gameObject;
        }

        SetPromptVisible(false);

        // 读档已经把 activationStatus 设成 true 的，进场就是亮的
        if (activationStatus && anim != null)
            anim.SetBool("active", true);
    }

    private void Update()
    {
        // 菜单 / 对话暂停时不提示也不能存
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
            SaveHere();
    }

    /// <summary>
    /// 玩家到存档点的距离。优先按碰撞体算：存档点常常悬在半空（碰撞体向下伸到地面），
    /// 直接用 transform.position 量的话，站在它正下方的玩家会显得很远。
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
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }

    #endregion

    #region 保存

    /// <summary>按 W 时执行：点亮 → 记为最新存档点 → 存一次档。</summary>
    public void SaveHere()
    {
        ActivateCheckpoint();

        if (GameManager.instance != null)
            GameManager.instance.SetLatestCheckpoint(this);

        if (SaveManager.instance != null)
            SaveManager.instance.SaveGame();
    }

    /// <summary>点亮这个存档点（读档恢复时也会调用）。</summary>
    public void ActivateCheckpoint()
    {
        if (activationStatus == false && saveSfxIndex >= 0 && AudioManager.instance != null)
            AudioManager.instance.PlaySFX(saveSfxIndex, transform);

        activationStatus = true;

        if (anim != null)
            anim.SetBool("active", true);
    }

    /// <summary>玩家复活时要站的位置。</summary>
    public Vector3 GetRespawnPosition() => transform.position;

    /// <summary>存档 / 读档用的 ID：手填优先，留空就按「场景 + 位置」自动生成。</summary>
    public string ResolveId()
    {
        if (!string.IsNullOrEmpty(id))
            return id;

        Vector3 position = transform.position;

        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "/" + name
            + "@" + Mathf.RoundToInt(position.x * 10f) + "," + Mathf.RoundToInt(position.y * 10f);
    }

    [ContextMenu("Generate checkpoint id")]
    private void GenerateId()
    {
        id = System.Guid.NewGuid().ToString();
    }

    #endregion

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
                label.text = string.Format(promptFormat, interactKey.ToString());
        }
    }

    #endregion
}
