using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 可拾取的月亮碎片（白天跑酷阶段）。
///
/// 玩家靠近即自动拾取（不依赖物理引擎，玩家没有刚体也能用），
/// 拾取后会累加到 DevourProgress.DayCoins，这个数量决定下一夜的长度。
///
/// 通常由 ParkourTerrainBuilder 自动生成在每一段地形上。
/// </summary>
public class MoonCoin : MonoBehaviour
{
    [Header("拾取")]
    [Tooltip("玩家进入这个半径就被拾取")]
    public float pickupRadius = 1.2f;

    [Tooltip("一个碎片值多少")]
    public int value = 1;

    [Tooltip("找不到玩家时，用这个标签或名字找")]
    public bool autoFindPlayer = true;

    [Header("表现")]
    [Tooltip("上下浮动幅度")]
    public float bobAmplitude = 0.14f;

    [Tooltip("上下浮动频率")]
    public float bobSpeed = 2.2f;

    [Tooltip("自转速度（度/秒）")]
    public float spinSpeed = 90f;

    [Header("事件")]
    public UnityEvent onCollected;

    private Transform player;
    private Vector3 baseLocal;
    private float phase;
    private bool taken;

    void OnEnable()
    {
        // 碎片槽会被回收复用，所以每次启用都要重新记录基准位置、重置状态
        baseLocal = transform.localPosition;
        phase = Random.Range(0f, Mathf.PI * 2f);
        taken = false;

        if (autoFindPlayer && player == null)
        {
            var p = FindAnyObjectByType<ParkourPlayer>();
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        if (taken) return;

        // 自己浮动 + 自转（改 localPosition，跟着地形段一起滚）
        float bob = Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
        transform.localPosition = baseLocal + new Vector3(0f, bob, 0f);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);

        // 玩家靠近 → 拾取
        if (player != null && Vector2.Distance(transform.position, player.position) <= pickupRadius)
        {
            Collect();
        }
    }

    /// <summary>
    /// 由地形生成器在摆放碎片时调用。
    ///
    /// 必须调用：否则段被回收复用时，Update 里的浮动会用"上一次启用时缓存的旧位置"
    /// 把碎片拉回旧坐标 —— 看起来就像碎片卡进了地面。
    /// </summary>
    public void SetBasePosition(Vector3 localPos)
    {
        baseLocal = localPos;
        transform.localPosition = localPos;
    }

    void Collect()
    {
        taken = true;

        DevourProgress.DayCoins += value;

        if (onCollected != null) onCollected.Invoke();

        Destroy(gameObject);
    }
}
