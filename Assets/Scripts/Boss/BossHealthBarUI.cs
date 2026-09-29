using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 专用的屏幕下方血条（不是挂在 Boss 头上的那种）。
/// 出现时从屏幕正中间向两边展开，上面写着 Boss 名字；Boss 掉血就实时变短。
///
/// 挂在 Canvas/InGame_UI/BossHealthBar 上，引用由 BossSceneBuilder 自动接好。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BossHealthBarUI : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private EnemyStats bossStats;

    [Tooltip("会从中间向两边展开的那根血条")]
    [SerializeField] private RectTransform barRoot;

    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("名字")]
    [SerializeField] private string bossName = "死亡使者-戴斯";

    [Header("尺寸与速度")]
    [Tooltip("完全展开后的血条宽度（画布是 1920x1080 的参考分辨率）")]
    [SerializeField] private float expandedWidth = 900f;

    [SerializeField] private float expandTime = 0.55f;
    [SerializeField] private float collapseTime = 0.35f;

    [Tooltip("血条追赶速度，越大越跟手")]
    [SerializeField] private float fillLerpSpeed = 8f;

    [Tooltip("掉血时白色残影的追赶速度，越小拖尾越长")]
    [SerializeField] private float delayFillLerpSpeed = 2.5f;

    [SerializeField] private Image delayFillImage;

    private CanvasGroup canvasGroup;
    private float barHeight = 26f;

    // Image.sprite 为空时兜底用的纯白图
    private static Sprite fallbackFillSprite;

    // 0 = 完全收拢，1 = 完全展开
    private float openAmount;
    private bool wantOpen;

    private float shownFill = 1f;
    private float delayFill = 1f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (barRoot != null)
            barHeight = barRoot.sizeDelta.y;

        if (nameText != null)
            nameText.text = bossName;

        PrepareFill(fillImage);
        PrepareFill(delayFillImage);

        HideImmediate();
    }

    /// <summary>
    /// 把一张 Image 准备成能按 fillAmount 裁切的填充条。
    /// 关键点：Image 用 Filled 类型时必须有 sprite，否则 Unity 会直接退化成画一整块白矩形，
    /// fillAmount 完全不起作用（看起来就是血条永远满）。
    /// </summary>
    private static void PrepareFill(Image _image)
    {
        if (_image == null)
            return;

        if (_image.sprite == null)
        {
            if (fallbackFillSprite == null)
                fallbackFillSprite = BuildWhiteSprite();

            _image.sprite = fallbackFillSprite;
        }

        if (_image.type != Image.Type.Filled)
        {
            _image.type = Image.Type.Filled;
            _image.fillMethod = Image.FillMethod.Horizontal;
            _image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    private static Sprite BuildWhiteSprite()
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[16];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
    }

    /// <summary>立刻收起来，不做动画。</summary>
    public void HideImmediate()
    {
        wantOpen = false;
        openAmount = 0f;

        shownFill = 1f;
        delayFill = 1f;

        ApplyOpenAmount();
    }

    public void Show()
    {
        wantOpen = true;

        // 展开前先把血量对齐，免得从满血滑下来
        if (bossStats != null)
        {
            shownFill = GetHealthRatio();
            delayFill = shownFill;
        }
    }

    public void Hide()
    {
        wantOpen = false;
    }

    private void Update()
    {
        float target = wantOpen ? 1f : 0f;

        if (!Mathf.Approximately(openAmount, target))
        {
            float duration = wantOpen ? expandTime : collapseTime;
            float speed = duration > 0.0001f ? 1f / duration : 1000f;

            openAmount = Mathf.MoveTowards(openAmount, target, speed * Time.unscaledDeltaTime);

            ApplyOpenAmount();
        }

        if (openAmount <= 0.001f)
            return;

        float health = GetHealthRatio();

        shownFill = Mathf.Lerp(shownFill, health, 1f - Mathf.Exp(-fillLerpSpeed * Time.unscaledDeltaTime));

        if (fillImage != null)
            fillImage.fillAmount = shownFill;

        if (delayFillImage != null)
        {
            delayFill = delayFill < shownFill
                ? shownFill
                : Mathf.Lerp(delayFill, shownFill, 1f - Mathf.Exp(-delayFillLerpSpeed * Time.unscaledDeltaTime));

            delayFillImage.fillAmount = delayFill;
        }
    }

    private float GetHealthRatio()
    {
        if (bossStats == null)
            return 1f;

        int max = bossStats.GetMaxHealthValue();

        if (max <= 0)
            return 1f;

        return Mathf.Clamp01((float)bossStats.currentHealth / max);
    }

    /// <summary>把「展开进度」应用到宽度和透明度上。</summary>
    private void ApplyOpenAmount()
    {
        if (barRoot != null)
            barRoot.sizeDelta = new Vector2(expandedWidth * openAmount, barHeight);

        if (canvasGroup != null)
            canvasGroup.alpha = openAmount;
    }
}
