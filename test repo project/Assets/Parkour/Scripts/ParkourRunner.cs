using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限跑酷：地形向左滚动，玩家原地不动。
///
/// 段被滚出左边界后回收 —— 挪到队尾，随机换一种类型继续铺。
/// 这样关卡可以无限延伸，玩家看起来像在往前跑。
/// </summary>
public class ParkourRunner : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动取本物体上的 ParkourTerrainBuilder")]
    public ParkourTerrainBuilder builder;

    [Tooltip("玩家（用来判断回收位置）")]
    public Transform player;

    [Header("滚动")]
    [Tooltip("地形向左滚动的速度（世界单位/秒）。开了速度爬升时这个值会被自动覆盖")]
    public float scrollSpeed = 5f;

    [Header("速度爬升")]
    [Tooltip("勾上后：白天进度越靠后滚得越快，形成紧张感曲线")]
    public bool useSpeedRamp = true;

    [Tooltip("白天刚开始时的速度")]
    public float speedStart = 6f;

    [Tooltip("白天快结束时的速度")]
    public float speedEnd = 9f;

    [Tooltip("太阳倒计时（提供白天进度）。留空会自动找")]
    public MoonCountdown dayCountdown;

    [Tooltip("段右边缘滚到 玩家X - 这个距离 之后就被回收")]
    public float recycleBehind = 14f;

    [Tooltip("同时存在多少段（越多越吃性能，8~10 够用）")]
    public int segmentCount = 9;

    [Tooltip("第一段相对玩家往左放多少段")]
    public int leadInSegments = 2;

    [Tooltip("开局前几段强制是平地（给玩家反应时间）。要 ≥ leadInSegments + 1，覆盖玩家脚下那一段")]
    public int safeStartSegments = 3;

    [Tooltip("地形起始高度（玩家脚下那层）")]
    public float startY = -2.5f;

    [Header("随机")]
    [Tooltip("0 = 每次启动都不同；填个非 0 值可复现同一串地形")]
    public int seed = 0;

    [Header("状态（只读）")]
    public bool running = true;
    public float distanceTravelled;

    private List<GameObject> segments = new List<GameObject>();
    private float tailX;
    private float tailY;
    private TerrainType lastType = TerrainType.Flat;
    private int nextIndex;

    void Awake()
    {
        if (builder == null) builder = GetComponent<ParkourTerrainBuilder>();
        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }

        if (dayCountdown == null) dayCountdown = FindAnyObjectByType<MoonCountdown>();

        if (seed != 0) Random.InitState(seed);

        BuildInitial();
    }

    /// <summary>重建整条地形（可重复调用）</summary>
    public void BuildInitial()
    {
        // 清掉旧段（连编辑器里遗留的未登记段一起清）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }
        segments.Clear();

        // 让玩家正好落在某一段的中间，而不是两段的接缝上
        float startX = (player != null ? player.position.x : 0f)
                       - builder.segmentWidth * (leadInSegments + 0.5f);
        float y = startY;

        tailX = startX;
        tailY = y;
        nextIndex = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            // 起跑区固定是平地，避免一开局就踩空
            TerrainType type = (i < safeStartSegments) ? TerrainType.Flat
                                                       : builder.PickType(tailY, lastType);

            var seg = builder.CreateSegment(nextIndex++, type, tailX, tailY);
            segments.Add(seg);

            lastType = type;
            tailY = builder.ExitY(type, tailY);
            tailX += builder.segmentWidth;
        }

        Debug.Log("[Runner] 初始生成 " + segmentCount + " 段，起点 x=" + startX.ToString("0.0"));

        // 把玩家放到「正下方地面上」——
        // 否则玩家可能正好落在一段抬高地面上，脚底比地面低 1.5，看起来就是"卡在墙里"，立刻就死
        SnapPlayerToGround();
    }

    /// <summary>把玩家对齐到脚下那块地面的高度</summary>
    public void SnapPlayerToGround()
    {
        if (player == null) return;

        Physics2D.SyncTransforms();

        // 从玩家上方往下打，取最高的那块地面
        int mask = LayerMask.GetMask("ground");
        if (mask == 0) mask = ~0;

        var hit = Physics2D.Raycast(
            new Vector2(player.position.x, player.position.y + 8f), Vector2.down, 40f, mask);

        if (hit.collider == null) return;

        player.position = new Vector3(player.position.x, hit.point.y + 0.8f, player.position.z);

        var pp = player.GetComponent<ParkourPlayer>();
        if (pp != null)
        {
            pp.velocityY = 0f;
            pp.isGrounded = true;
        }
    }

    void Update()
    {
        if (!running || builder == null) return;

        // ---- 速度爬升：白天越往后越快 ----
        if (useSpeedRamp && dayCountdown != null)
        {
            float p = Mathf.Clamp01(dayCountdown.Progress01);
            scrollSpeed = Mathf.Lerp(speedStart, speedEnd, p);
        }

        // 和玩家用同样的 dt 上限，避免卡帧时"地面比人跑得快"造成抖动
        float dt = Time.deltaTime;
        if (dt > 0.05f) dt = 0.05f;

        float dx = scrollSpeed * dt;
        distanceTravelled += dx;

        float limit = (player != null ? player.position.x : 0f) - recycleBehind;

        // 第一遍：先把所有段整体左移
        // （必须全部移完再回收，否则回收时算"最右段"会用到还没移动的旧坐标，
        //   导致新段被放到 maxX + width 时多出/少掉一个 dx）
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg == null) continue;

            var p = seg.transform.localPosition;
            p.x -= dx;
            seg.transform.localPosition = p;
        }

        // 第二遍：统一回收滚出左边界的段
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg == null) continue;

            if (seg.transform.localPosition.x + builder.segmentWidth < limit)
            {
                Recycle(seg);
            }
        }
    }

    void Recycle(GameObject seg)
    {
        // 队尾位置用「当前最右的那段」现算，不要用会随滚动漂移的累加游标
        float maxX = float.MinValue;
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s == null) continue;
            float x = s.transform.localPosition.x;
            if (x > maxX) maxX = x;
        }

        float newX = maxX + builder.segmentWidth;

        TerrainType type = builder.PickType(tailY, lastType);

        seg.transform.localPosition = new Vector3(newX, tailY, 0f);
        builder.RebuildSegment(seg, type, newX, tailY, nextIndex++);

        lastType = type;
        tailY = builder.ExitY(type, tailY);
        tailX = newX;
    }

    /// <summary>停止滚动（掉落/结束时调用）</summary>
    public void Stop()
    {
        running = false;
    }

    /// <summary>重新开始</summary>
    public void Restart()
    {
        running = true;
        distanceTravelled = 0f;
        BuildInitial();
    }
}
