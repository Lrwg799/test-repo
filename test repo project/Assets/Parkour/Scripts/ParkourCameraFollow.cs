using UnityEngine;

/// <summary>
/// 跑酷相机的纵向跟随。
///
/// 为什么需要：地形 y 范围是 -6 ~ +3（每层高 1.5），而固定相机只能看到 ±5。
/// 一旦连续下坡两三层，地面就整段沉到屏幕外面；从高平台起跳时玩家又会冲出屏幕上方。
///
/// 做法：相机纵向平滑跟随玩家，但
///   · 带死区，小幅起伏不动相机（避免抖动）
///   · 用 SmoothDamp 平滑，不会瞬移
///   · 玩家死亡后停止跟随（不然会跟着掉进虚空）
///
/// 太阳 / 天空是相机的子物体，会跟着相机走，但在**屏幕上位置不变**，所以不受影响。
/// </summary>
public class ParkourCameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("留空会自动找场景里的 ParkourPlayer")]
    public Transform target;

    [Tooltip("相机比玩家高多少（越大看到的脚下地面越多）")]
    public float offsetY = 1.75f;

    [Tooltip("死区：玩家上下偏移在这么小范围内时相机不动")]
    public float deadzone = 0.45f;

    [Tooltip("平滑时间，越大越拖沓")]
    public float smoothTime = 0.22f;

    [Tooltip("玩家死亡后是否停止跟随")]
    public bool stopOnDeath = true;

    [Header("范围限制")]
    [Tooltip("相机 y 的下限")]
    public float minY = -2.5f;

    [Tooltip("相机 y 的上限")]
    public float maxY = 7f;

    private ParkourPlayer player;
    private float velocityY;
    private float baseX;
    private float baseZ;

    void Awake()
    {
        if (target == null)
        {
            player = FindAnyObjectByType<ParkourPlayer>();
            if (player != null) target = player.transform;
        }
        else
        {
            player = target.GetComponent<ParkourPlayer>();
        }

        baseX = transform.position.x;
        baseZ = transform.position.z;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 死亡后不再跟随，避免相机跟着掉进虚空
        if (stopOnDeath && player != null && player.isDead) return;

        float want = Mathf.Clamp(target.position.y + offsetY, minY, maxY);

        // 死区内不动作，避免地形小幅起伏时相机一直抖
        if (Mathf.Abs(want - transform.position.y) <= deadzone) return;

        float y = Mathf.SmoothDamp(transform.position.y, want, ref velocityY, smoothTime);
        transform.position = new Vector3(baseX, y, baseZ);
    }
}
