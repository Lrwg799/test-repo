using UnityEngine;

/// <summary>
/// 幽灵的上下浮动（idle 呼吸感）。
///
/// ⚠️ 必须挂在「只负责显示」的子物体上：
///     父物体（www）负责真正的移动，这个子物体只改自己的 localPosition，
///     两者互不打架。
/// </summary>
public class FloatBob : MonoBehaviour
{
    [Header("上下浮动")]
    [Tooltip("上下浮动的幅度（世界单位）")]
    public float amplitude = 0.15f;

    [Tooltip("每秒摆动几个来回")]
    public float frequency = 1.2f;

    [Tooltip("勾上则不受 Time.timeScale 影响（暂停时也会飘）")]
    public bool useUnscaledTime = false;

    [Header("缩放呼吸（可选）")]
    [Tooltip("是否同时做轻微的缩放呼吸")]
    public bool scaleBreath = false;

    [Tooltip("缩放幅度（0.04 = ±4%）")]
    public float scaleAmount = 0.04f;

    [Tooltip("缩放呼吸的频率")]
    public float scaleFrequency = 0.8f;

    private Vector3 basePos;
    private Vector3 baseScale;
    private float t;

    void Start()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale;
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        t += dt;

        float offset = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amplitude;
        transform.localPosition = basePos + new Vector3(0f, offset, 0f);

        if (scaleBreath)
        {
            float s = 1f + Mathf.Sin(t * scaleFrequency * Mathf.PI * 2f) * scaleAmount;
            transform.localScale = baseScale * s;
        }
    }
}
