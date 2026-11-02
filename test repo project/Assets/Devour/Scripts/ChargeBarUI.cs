using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 蓄力条：把 ChargeController 的进度显示在 UI 上。
///
/// 用法：Canvas 下放一个背景 Image，再放一个子 Image 设成
///       Image Type = Filled / Fill Method = Horizontal / Origin = Left，
///       把这个子 Image 拖到 Fill 上即可。
/// </summary>
public class ChargeBarUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动找场景里的 ChargeController")]
    public ChargeController charge;

    [Tooltip("负责填充的 Image（Type 必须是 Filled）")]
    public Image fill;

    [Tooltip("整条蓄力条的容器，留空则不自动显隐")]
    public GameObject barRoot;

    [Header("表现")]
    [Tooltip("不蓄力时隐藏整条")]
    public bool hideWhenIdle = false;

    [Tooltip("是否平滑过渡（关掉就是瞬间跟随）")]
    public bool smooth = true;

    [Tooltip("平滑速度，越大跟得越紧")]
    public float smoothSpeed = 14f;

    [Header("颜色")]
    [Tooltip("进度从空到满时渐变颜色")]
    public bool colorByProgress = true;

    public Color emptyColor = new Color(0.35f, 0.78f, 1f);
    public Color fullColor = new Color(1f, 0.85f, 0.25f);

    private float shown;

    void Awake()
    {
        if (charge == null) charge = FindAnyObjectByType<ChargeController>();

        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    void Update()
    {
        if (charge == null || fill == null) return;

        // 蓄力中跟着进度走，松开后回到 0
        float target = charge.IsCharging ? Mathf.Clamp01(charge.Progress01) : 0f;

        if (smooth)
        {
            shown = Mathf.MoveTowards(shown, target, smoothSpeed * Time.deltaTime);
        }
        else
        {
            shown = target;
        }

        fill.fillAmount = shown;

        if (colorByProgress)
        {
            fill.color = Color.Lerp(emptyColor, fullColor, shown);
        }

        if (barRoot != null && hideWhenIdle)
        {
            barRoot.SetActive(charge.IsCharging);
        }
    }

    /// <summary>蓄力进度（0~1），给外部读</summary>
    public float Shown { get { return shown; } }
}
