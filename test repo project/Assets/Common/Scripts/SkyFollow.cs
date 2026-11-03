using UnityEngine;

/// <summary>
/// 背景天空：跟随相机 + 缓慢漂移。
///
/// 为什么不做成相机子物体：
///   实测挂在相机下面时，天空 SpriteRenderer 在 URP 2D 里会被裁掉不渲染；
///   作为根物体 + 每帧跟随相机，效果一样，而且渲染正常。
///
/// 漂移：
///   背景比视野大一圈（放大 1.3 倍左右），然后在这个余量里慢慢左右/上下摆动，
///   所以永远看不到图片边框，看起来像地图在缓缓移动。
/// </summary>
[DefaultExecutionOrder(100)]
public class SkyFollow : MonoBehaviour
{
    [Header("跟随")]
    [Tooltip("要跟随的相机（留空自动用 Main Camera）")]
    public Transform target;

    [Tooltip("相对相机的偏移。z 需要是正数，才会在相机前面")]
    public Vector3 offset = new Vector3(0f, 0f, 9f);

    [Tooltip("是否跟随纵向移动")]
    public bool followY = true;

    [Header("缓慢漂移")]
    [Tooltip("左右漂移速度（度/秒，越大越快）")]
    public float driftSpeedX = 8f;

    [Tooltip("左右漂移幅度（世界单位）。必须小于「背景宽度 - 视野宽度」的一半")]
    public float driftRangeX = 1.2f;

    [Tooltip("上下漂移速度")]
    public float driftSpeedY = 3f;

    [Tooltip("上下漂移幅度（世界单位）")]
    public float driftRangeY = 0.4f;

    [Tooltip("初始相位随机，避免每次开局位置一样")]
    public bool randomStart = true;

    private float phaseX;
    private float phaseY;

    void Start()
    {
        if (randomStart)
        {
            phaseX = Random.Range(0f, Mathf.PI * 2f);
            phaseY = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Awake()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (target == null) return;

        phaseX += driftSpeedX * Time.unscaledDeltaTime * Mathf.Deg2Rad;
        phaseY += driftSpeedY * Time.unscaledDeltaTime * Mathf.Deg2Rad;

        float dx = Mathf.Sin(phaseX) * driftRangeX;
        float dy = Mathf.Sin(phaseY) * driftRangeY;

        var p = target.position + offset + new Vector3(dx, dy, 0f);
        if (!followY) p.y = transform.position.y;

        transform.position = p;
    }
}
