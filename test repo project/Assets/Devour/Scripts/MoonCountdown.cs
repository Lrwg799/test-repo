using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 月亮倒计时：月亮沿着一段半圆弧，从屏幕一侧飞到另一侧。
///
/// 轨迹用椭圆参数方程：
///   x = centerX + radiusX * cos(θ)
///   y = centerY + radiusY * sin(θ)
///   θ 从 0 走到 π  →  从右侧出发，中间最高，落到左侧
///
/// 建议把这个物体挂在 Main Camera 下面（相机空间 = 屏幕空间），
/// 这样相机怎么动，倒计时都固定在画面里。
///
/// 可以自动铺一条"虚线轨迹"（像参考图那样的小圆点）。
/// </summary>
public class MoonCountdown : MonoBehaviour
{
    [Header("天体")]
    [Tooltip("月亮本体（留空则自动用子物体里的 Moon）")]
    public Transform moon;

    [Header("轨迹（本地坐标，以本物体为原点）")]
    [Tooltip("弧心 X")]
    public float centerX = 0f;

    [Tooltip("弧心 Y（两端的落点高度）")]
    public float centerY = 1.2f;

    [Tooltip("水平半径：约等于屏幕宽度的一半")]
    public float radiusX = 7.5f;

    [Tooltip("垂直半径：弧顶比两端高多少")]
    public float radiusY = 3f;

    [Tooltip("勾上 = 从右飞到左（参考图方向）")]
    public bool rightToLeft = true;

    [Header("倒计时")]
    [Tooltip("总时长（秒）")]
    public float duration = 90f;

    [Tooltip("进入 Play 就自动开始")]
    public bool autoStart = true;

    [Tooltip("结束后是否循环重来")]
    public bool loop = false;

    [Header("虚线轨迹")]
    [Tooltip("自动铺一排小圆点")]
    public bool buildPathDots = true;

    [Tooltip("小圆点用的贴图（一般是纯圆/纯白）")]
    public Sprite dotSprite;

    [Tooltip("圆点数量")]
    public int dotCount = 16;

    [Tooltip("圆点直径（世界单位）")]
    public float dotSize = 0.13f;

    public Color dotColor = new Color(1f, 1f, 1f, 0.28f);

    [Header("渲染层级")]
    [Tooltip("越大越画在上面")]
    public int sortingOrder = 20;

    [Header("事件")]
    [Tooltip("每帧触发（参数是剩余进度 0~1）")]
    public UnityEvent<float> onTick;

    [Tooltip("倒计时归零时触发一次")]
    public UnityEvent onFinished;

    // ================= 状态 =================
    private float elapsed;
    private bool running;
    private bool fired;

    /// <summary>已经走了多少（0 = 刚开始，1 = 结束）</summary>
    public float Progress01 { get { return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration); } }

    /// <summary>剩余秒数</summary>
    public float Remaining { get { return Mathf.Max(0f, duration - elapsed); } }

    /// <summary>剩余比例（1 = 满，0 = 没了）</summary>
    public float Remaining01 { get { return 1f - Progress01; } }

    public bool IsRunning { get { return running; } }

    void Start()
    {
        if (moon == null)
        {
            var t = transform.Find("Moon");
            if (t != null) moon = t;
        }

        if (buildPathDots) BuildDots();

        if (autoStart) StartCountdown();
        else { running = false; PlaceMoon(0f); }
    }

    void Update()
    {
        if (!running) return;

        elapsed += Time.deltaTime;

        float p = Progress01;
        PlaceMoon(p);

        if (onTick != null) onTick.Invoke(Remaining01);

        if (p >= 1f)
        {
            if (!fired)
            {
                fired = true;
                if (onFinished != null) onFinished.Invoke();
                Debug.Log("[MoonCountdown] 倒计时结束");
            }

            if (loop) ResetCountdown();
            else running = false;
        }
    }

    /// <summary>把月亮放到轨迹上的某个进度</summary>
    public void PlaceMoon(float progress)
    {
        if (moon == null) return;

        float t = Mathf.Clamp01(progress);
        float theta = t * Mathf.PI;

        // rightToLeft：θ 从 0 到 π，cos 从 +1 到 -1（右 → 左）
        // 否则反过来（左 → 右）
        float sign = rightToLeft ? 1f : -1f;

        float x = centerX + sign * radiusX * Mathf.Cos(theta);
        float y = centerY + radiusY * Mathf.Sin(theta);

        moon.localPosition = new Vector3(x, y, 0f);
    }

    /// <summary>铺一排虚线轨迹点（铺在弧线上，位置永远和参数一致）</summary>
    void BuildDots()
    {
        if (dotSprite == null || dotCount < 2) return;

        // 清掉上次生成的
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name.StartsWith("Dot_")) Destroy(c.gameObject);
        }

        for (int i = 0; i < dotCount; i++)
        {
            float t = (i + 0.5f) / dotCount;
            float theta = t * Mathf.PI;
            float sign = rightToLeft ? 1f : -1f;

            var go = new GameObject("Dot_" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                centerX + sign * radiusX * Mathf.Cos(theta),
                centerY + radiusY * Mathf.Sin(theta),
                0f);
            go.transform.localScale = new Vector3(dotSize, dotSize, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.color = dotColor;
            sr.sortingOrder = sortingOrder - 1;
        }
    }

    // ================= 外部控制 =================

    public void StartCountdown()
    {
        running = true;
        fired = false;
        elapsed = 0f;
        PlaceMoon(0f);
    }

    public void Pause() { running = false; }
    public void Resume() { if (Progress01 < 1f) running = true; }

    public void ResetCountdown()
    {
        elapsed = 0f;
        fired = false;
        running = true;
        PlaceMoon(0f);
    }

    /// <summary>直接跳到某个进度（0~1）</summary>
    public void SetProgress(float p)
    {
        elapsed = Mathf.Clamp01(p) * duration;
        fired = false;
        PlaceMoon(Progress01);
    }
}
