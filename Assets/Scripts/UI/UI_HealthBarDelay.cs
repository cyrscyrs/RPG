using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在血条 Slider 上（和 Slider 同一个物体）：扣血的时候，掉掉的那一段先用黄色填着，
/// 停一下再慢慢缩回真实血量。红色血条本身立刻变短，黄色在它后面负责显示「刚掉了多少」。
///
/// 实现方式：在 Fill Area 里、红色 Fill 的下面插一张同样大小的黄色 Image（Filled 类型），
/// 每帧把它的 fillAmount 往真实血量追。不用改预制体结构，任何用 Slider 做血条的地方都能用。
/// UI_HealthBar（玩家/怪物头顶）和 UI_InGame（屏幕上的玩家血条）都会自动挂上这个组件。
/// </summary>
[RequireComponent(typeof(Slider))]
public class UI_HealthBarDelay : MonoBehaviour
{
    [Header("掉血残影")]
    [Tooltip("残影颜色（黄色）")]
    public Color color = new Color(1f, 0.83f, 0.2f, 1f);

    [Tooltip("扣血后先停多久再开始缩（秒）")]
    public float holdTime = 0.18f;

    [Tooltip("收缩速度：每秒缩掉最大血量的百分之多少（0.35 = 每秒缩 35%）")]
    public float drainSpeed = 0.35f;

    private Slider slider;
    private Image damageFill;

    private float shownValue;
    private float lastValue;
    private float holdTimer;

    private static Sprite whiteSprite;

    private void Awake()
    {
        slider = GetComponent<Slider>();

        BuildDamageFill();
    }

    /// <summary>由血条脚本调用，把 Inspector 上的参数传进来。</summary>
    public void Configure(Color _color, float _holdTime, float _drainSpeed)
    {
        color = _color;
        holdTime = _holdTime;
        drainSpeed = _drainSpeed;

        // 组件可能还没 Awake（血条物体没激活时 AddComponent 不会立刻 Awake），
        // 那就等 Awake 里建图的时候用它自己读到的这些值。
        if (damageFill != null)
            damageFill.color = color;
    }

    private void BuildDamageFill()
    {
        if (slider == null || slider.fillRect == null)
            return;

        RectTransform fillRect = slider.fillRect;

        // 先把 slider 顶到满格再照抄 Fill 的矩形：Slider 是靠改 Fill 的 anchor 画长度的，
        // 满格时 Fill 的矩形才是整条，照抄它黄色条才能和红条完全重合。
        float value = slider.value;
        slider.value = slider.maxValue;

        GameObject go = new GameObject("DamageFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = fillRect.gameObject.layer;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(fillRect.parent, false);
        rect.anchorMin = fillRect.anchorMin;
        rect.anchorMax = fillRect.anchorMax;
        rect.pivot = fillRect.pivot;
        rect.anchoredPosition = fillRect.anchoredPosition;
        rect.sizeDelta = fillRect.sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        // 插在红条的前面：UI 里越靠后的兄弟画在越上面，红条就会盖住黄色
        rect.SetSiblingIndex(Mathf.Max(0, fillRect.GetSiblingIndex()));

        damageFill = go.GetComponent<Image>();
        damageFill.color = color;
        damageFill.raycastTarget = false;
        damageFill.sprite = GetWhiteSprite();
        damageFill.type = Image.Type.Filled;
        damageFill.fillMethod = Image.FillMethod.Horizontal;
        damageFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        damageFill.fillAmount = 1f;

        slider.value = value;

        shownValue = slider.value;
        lastValue = slider.value;
    }

    /// <summary>
    /// Filled 类型的 Image 必须有 sprite：sprite 为空时 Unity 会退化成画一整块矩形，fillAmount 就不起作用了。
    /// 原来的红条本来就没有贴图（纯色矩形），所以给一张纯白图，效果和红条一致。
    /// </summary>
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[16];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }

    private void Update()
    {
        if (damageFill == null || slider == null)
            return;

        float value = slider.value;

        // 又挨了一下：残影先停一下再开始缩，不然连续掉血会一闪而过
        if (value < lastValue)
            holdTimer = holdTime;

        lastValue = value;

        if (shownValue <= value)
        {
            // 回血、或者残影已经追平：直接跟上
            shownValue = value;
        }
        else if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
        }
        else
        {
            float speed = Mathf.Max(0.01f, drainSpeed) * Mathf.Max(1f, slider.maxValue);
            shownValue = Mathf.Max(value, shownValue - speed * Time.deltaTime);
        }

        float max = slider.maxValue;
        damageFill.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(shownValue / max);
    }
}
