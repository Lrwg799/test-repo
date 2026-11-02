using UnityEngine;

/// <summary>
/// 随时间变化的渐变天空。
///
/// 用当前场景的倒计时进度（0~1）去采样两条 Gradient：
///   · 顶部颜色
///   · 底部颜色
/// 然后运行时生成一张竖向渐变贴图贴到 SpriteRenderer 上。
///
/// 这样两个场景就能接成一个完整的循环：
///   白天：清晨 → 正午 → 黄昏
///   夜晚：入夜 → 深夜 → 破晓
///
/// 用法：挂在原来的天空物体上（DaySky / NightSky），把 Sky Renderer 指给它自己。
/// </summary>
public class SkyGradient : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要上色的天空 SpriteRenderer，留空自动取本物体上的")]
    public SpriteRenderer skyRenderer;

    [Tooltip("用来驱动进度的倒计时，留空自动找场景里的")]
    public MoonCountdown countdown;

    [Tooltip("会让场景变亮的相机（同步 clearColor 兜底）")]
    public Camera targetCamera;

    [Header("颜色曲线（按进度 0~1 采样）")]
    [Tooltip("天空顶部颜色")]
    public Gradient topColor = new Gradient();

    [Tooltip("天空底部颜色")]
    public Gradient bottomColor = new Gradient();

    [Tooltip("反转进度（想让它越走越暗就勾上）")]
    public bool invertProgress;

    [Header("性能")]
    [Tooltip("贴图高度（越大渐变越细腻，256 已足够）")]
    public int textureHeight = 256;

    [Tooltip("多久更新一次颜色，不需要每帧")]
    public float updateInterval = 0.05f;

    // 贴图宽 32px / PPU 32 → 1 个世界单位宽，正好配合原来天空物体的 (32, 1.5) 缩放
    private const int TextureWidth = 32;

    private Texture2D texture;
    private Color32[] pixels;
    private Sprite runtimeSprite;
    private float timer;
    private Color lastTop;
    private Color lastBottom;
    private bool hasApplied;

    void Awake()
    {
        if (skyRenderer == null) skyRenderer = GetComponent<SpriteRenderer>();
        if (countdown == null) countdown = FindAnyObjectByType<MoonCountdown>();
        if (targetCamera == null) targetCamera = Camera.main;

        int h = Mathf.Max(8, textureHeight);
        texture = new Texture2D(TextureWidth, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.name = "SkyGradientTex";

        pixels = new Color32[TextureWidth * h];

        runtimeSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureWidth, h),
                                      new Vector2(0.5f, 0.5f), 32f);
        runtimeSprite.name = "SkyGradientSprite";

        if (skyRenderer != null) skyRenderer.sprite = runtimeSprite;

        Refresh(true);
    }

    void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (texture != null) Destroy(texture);
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < updateInterval) return;

        timer = 0f;
        Refresh(false);
    }

    void Refresh(bool force)
    {
        if (texture == null) return;

        float p = countdown != null ? Mathf.Clamp01(countdown.Progress01) : 0f;
        if (invertProgress) p = 1f - p;

        Color top = topColor.Evaluate(p);
        Color bottom = bottomColor.Evaluate(p);

        if (!force && hasApplied && top == lastTop && bottom == lastBottom) return;

        lastTop = top;
        lastBottom = bottom;
        hasApplied = true;

        int h = texture.height;

        for (int y = 0; y < h; y++)
        {
            Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
            Color32 c32 = c;

            int baseIndex = y * TextureWidth;
            for (int x = 0; x < TextureWidth; x++) pixels[baseIndex + x] = c32;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);

        // 相机底色跟着底部颜色走，防止极宽窗口下边缘露底
        if (targetCamera != null) targetCamera.backgroundColor = bottom;
    }
}
