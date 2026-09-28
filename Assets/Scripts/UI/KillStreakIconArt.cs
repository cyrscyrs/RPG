using UnityEngine;

/// <summary>
/// 连续杀敌加速图标的贴图绘制。
///
/// 运行时会用到（预制体没接图标贴图时兜底现画一张），
/// 编辑器生成 PNG 资源（KillStreakPrefabBuilder）时用的也是同一份实现，
/// 所以两条路画出来的图标一定一模一样。
/// </summary>
public static class KillStreakIconArt
{
    public const int IconSize = 64;
    public const int BarSize = 8;

    // 闪电在图标里占的高度比例（1 = 顶到边），描边比本体稍微大一圈
    private const float BoltBodyHeight = 0.50f;
    private const float BoltOutlineHeight = 0.58f;

    private const int SuperSample = 4;

    private static readonly Vector2[] BoltShape =
    {
        new Vector2(0.62f, 1.00f),
        new Vector2(0.25f, 0.55f),
        new Vector2(0.47f, 0.55f),
        new Vector2(0.38f, 0.00f),
        new Vector2(0.78f, 0.50f),
        new Vector2(0.55f, 0.50f),
    };

    /// <summary>画一个「深色圆牌 + 金色闪电」的加速图标。</summary>
    public static Texture2D CreateIconTexture(int _size = IconSize)
    {
        _size = Mathf.Max(8, _size);

        Texture2D tex = NewTexture(_size);
        Color32[] pixels = new Color32[_size * _size];

        for (int y = 0; y < _size; y++)
        {
            for (int x = 0; x < _size; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;

                for (int sy = 0; sy < SuperSample; sy++)
                {
                    for (int sx = 0; sx < SuperSample; sx++)
                    {
                        float u = (x + (sx + 0.5f) / SuperSample) / _size;
                        float v = (y + (sy + 0.5f) / SuperSample) / _size;

                        Color c = SampleIcon(u, v);

                        // 预乘 alpha 再平均，边缘不会发黑
                        r += c.r * c.a;
                        g += c.g * c.a;
                        b += c.b * c.a;
                        a += c.a;
                    }
                }

                float alpha = a / (SuperSample * SuperSample);

                if (alpha <= 0.002f)
                {
                    pixels[y * _size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                pixels[y * _size + x] = new Color32(
                    (byte)(Mathf.Clamp01(r / a) * 255f),
                    (byte)(Mathf.Clamp01(g / a) * 255f),
                    (byte)(Mathf.Clamp01(b / a) * 255f),
                    (byte)(Mathf.Clamp01(alpha) * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return tex;
    }

    /// <summary>时间条用的纯白小图。</summary>
    public static Texture2D CreateBarTexture(int _size = BarSize)
    {
        _size = Mathf.Max(2, _size);

        Texture2D tex = NewTexture(_size);
        Color32[] pixels = new Color32[_size * _size];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        tex.SetPixels32(pixels);
        tex.Apply();

        return tex;
    }

    public static Sprite CreateIconSprite(int _size = IconSize)
    {
        Texture2D tex = CreateIconTexture(_size);

        return Sprite.Create(tex, new Rect(0f, 0f, _size, _size), new Vector2(0.5f, 0.5f), _size);
    }

    public static Sprite CreateBarSprite(int _size = BarSize)
    {
        Texture2D tex = CreateBarTexture(_size);

        return Sprite.Create(tex, new Rect(0f, 0f, _size, _size), new Vector2(0.5f, 0.5f), _size);
    }

    private static Texture2D NewTexture(int _size)
    {
        Texture2D tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        return tex;
    }

    private static Color SampleIcon(float _u, float _v)
    {
        Vector2 p = new Vector2(_u, _v);
        float distance = (p - new Vector2(0.5f, 0.5f)).magnitude;

        if (distance > 0.48f)
            return new Color(0f, 0f, 0f, 0f);

        // 外圈金环
        if (distance > 0.40f)
            return new Color(0.96f, 0.78f, 0.32f, 1f);

        // 金环和底之间压一圈暗色，小小的一圈立体感
        if (distance > 0.355f)
            return new Color(0.30f, 0.20f, 0.05f, 1f);

        if (InsideBolt(p, BoltOutlineHeight))
            return new Color(0.36f, 0.22f, 0.03f, 1f);   // 闪电描边

        if (InsideBolt(p, BoltBodyHeight))
            return new Color(1f, 0.86f, 0.30f, 1f);      // 闪电本体

        return new Color(0.07f, 0.08f, 0.13f, 0.94f);    // 深色圆牌
    }

    /// <summary>
    /// 把点换算回闪电自己的归一化空间，再做奇偶规则的内外判断。
    /// _height 是闪电在图标里占的高度比例（0.6 = 占图标高度的 60%）。
    /// </summary>
    private static bool InsideBolt(Vector2 _p, float _height)
    {
        float scale = Mathf.Max(0.0001f, _height);

        Vector2 local = new Vector2(
            0.5f + (_p.x - 0.5f) / scale,
            0.5f + (_p.y - 0.5f) / scale);

        bool inside = false;

        for (int i = 0, j = BoltShape.Length - 1; i < BoltShape.Length; j = i++)
        {
            Vector2 a = BoltShape[i];
            Vector2 b = BoltShape[j];

            if ((a.y > local.y) != (b.y > local.y) &&
                local.x < (b.x - a.x) * (local.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }

        return inside;
    }
}
