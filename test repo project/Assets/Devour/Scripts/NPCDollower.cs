using UnityEngine;

/// <summary>
/// 挂在【NPC】身上。
/// 没被吞噬前什么都不做，NPC 照常回头 / 巡逻；
/// 被吞噬后（BeginFollow）转成跟随者，按队列序号站在玩家【身后】。
///
/// 横向版本：队列沿 X 轴排布 —— 玩家往右走，跟随者排在左侧；往左走则排在右侧。
///
/// ⚠️ 外观走的是「显示用子物体」（NPCVisual）：
///     根物体负责移动，子物体负责贴图和浮动，两者互不打架。
/// </summary>
public class NPCDollower : MonoBehaviour
{
    [Header("跟随设定")]
    [Tooltip("跟随目标，通常由 NPCDevourer 在吞噬时指定")]
    public Transform target;

    [Tooltip("第一个跟随者站在玩家身后（横向）多远")]
    public float followDistance = 1.5f;

    [Tooltip("纵向偏移：正值往上、负值往下（一般留 0）")]
    public float sideOffset = 0f;

    [Tooltip("跟随速度")]
    public float moveSpeed = 7f;

    [Header("队列")]
    [Tooltip("队列序号：0 = 紧贴玩家，1 = 再往后一个……由 NPCDevourer 自动分配")]
    public int queueIndex = 0;

    [Tooltip("相邻两行之间的间距（纵向排布的每个新行）")]
    public float queueSpacing = 1.4f;

    [Tooltip("每行最多放几个跟随者，超过就换到下一行")]
    public int maxPerRow = 3;

    [Tooltip("同一行内上下错开的间距（3 个一行时是 -gap / 0 / +gap）")]
    public float rowGap = 1.7f;

    [Header("散布（不重叠优先，实在没位置才重叠）")]
    [Tooltip("第一个跟随者离玩家多远")]
    public float startRadius = 1.4f;

    [Tooltip("最远排到玩家身后多远")]
    public float maxBackDistance = 10f;

    [Tooltip("候选点的纵深间距（越小越密，越容易排下更多只）")]
    public float backStep = 0.5f;

    [Tooltip("候选点的上下范围（±这个值）")]
    public float lateralRange = 3.2f;

    [Tooltip("候选点的上下间距")]
    public float lateralStep = 0.55f;

    [Tooltip("两只之间至少间隔多少才算「不重叠」。建议略大于幽灵宽度")]
    public float minGap = 1.15f;

    [Tooltip("候选点的随机扰动幅度。0 = 规整网格，越大越杂乱（不会因此重叠）")]
    public float jitter = 0.45f;

    [Tooltip("在「最近的几个够宽位置」里随机挑一个。1 = 总挑最近的（最规整），越大越杂乱")]
    public int pickWindow = 5;

    /// <summary>场上所有跟随幽灵（按加入顺序）。编号和落点都以此为准</summary>
    public static readonly System.Collections.Generic.List<NPCDollower> Followers =
        new System.Collections.Generic.List<NPCDollower>();

    /// <summary>当前场上跟随幽灵总数</summary>
    public static int TotalFollowers { get { return Followers.Count; } }

    /// <summary>换场景/重开时清空注册表</summary>
    public static void ClearFollowers()
    {
        Followers.Clear();
        Slots = new Vector2[0];
    }

    /// <summary>把编号重排成 0..n-1，并按人数重算落点表</summary>
    static void Reindex()
    {
        for (int i = Followers.Count - 1; i >= 0; i--)
            if (Followers[i] == null) Followers.RemoveAt(i);

        for (int i = 0; i < Followers.Count; i++)
            Followers[i].queueIndex = i;

        if (Followers.Count > 0) Followers[0].RebuildSlots(Followers.Count);
    }

    /// <summary>算好的落点表（相对玩家：x = 身后距离，y = 上下偏移）</summary>
    public static Vector2[] Slots = new Vector2[0];

    /// <summary>
    /// 跟随者可选的贴图（幽灵变体）。由 NPCRespawner 从图集填进来，
    /// BeginFollow 时每只随机挑一张 —— 一群幽灵长得各不相同。
    /// </summary>
    public static Sprite[] GhostSprites;

    [Header("浮现")]
    [Tooltip("从透明到实体用多久")]
    public float fadeInDuration = 1.2f;

    [Tooltip("延迟多久才开始浮现（要等新 NPC 刷新出来）")]
    public float revealDelay = 0.9f;

    private float fadeTimer = -1f;

    [Header("外观")]
    [Tooltip("真正显示贴图的 SpriteRenderer（通常是子物体 NPCVisual）。\n留空会自动找自己和子物体")]
    public SpriteRenderer visual;

    [Tooltip("被吞噬后换上的贴图（通常是玩家贴图），由 NPCDevourer 自动填入")]
    public Sprite devouredSprite;

    [Tooltip("是否把缩放也改成玩家的大小")]
    public bool matchPlayerScale = false;

    [Tooltip("玩家的缩放值")]
    public Vector3 playerScale = Vector3.one;

    [Header("状态（只读）")]
    public bool isFollower = false;

    private LookBackWatcher watcher;
    private float facing = 1f;          // 玩家横向移动方向：+1 向右 / -1 向左
    private Vector3 lastTargetPos;

    void Awake()
    {
        // 优先用显示用子物体，没有就退回自己
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
        if (visual == null) visual = GetComponent<SpriteRenderer>();

        // 防错：显示层如果被拖离了根节点（在场景里直接拖精灵很容易这样），
        // 把这段偏移烘焙回根节点，否则"逻辑位置"和"画面位置"会错位。
        if (visual != null && visual.transform.parent == transform)
        {
            Vector3 off = visual.transform.position - transform.position;
            if (off.sqrMagnitude > 0.0001f)
            {
                transform.position += off;
                visual.transform.localPosition = Vector3.zero;
            }
        }

        watcher = GetComponent<LookBackWatcher>();
        lastTargetPos = transform.position;
    }

    /// <summary>被吞噬：转成跟随者</summary>
    public void BeginFollow(Transform newTarget)
    {
        if (newTarget == null) return;

        bool wasAlreadyFollower = isFollower;

        target = newTarget;
        isFollower = true;
        lastTargetPos = target.position;

        // 注册进全局列表 → 编号重排成 0..n-1、落点表按总人数重算
        // 关键：不能用各自维护的局部计数，否则"上一夜恢复的"和"本局吞掉的"会撞号
        if (!wasAlreadyFollower && !Followers.Contains(this))
        {
            Followers.Add(this);
            Reindex();
        }

        // 被吞了就不再当"路人"回头
        if (watcher != null)
        {
            // 先 Cancel 复位：藏起头顶预警标志、朝向转回去
            // （只把 enabled 设 false 的话，正在亮的感叹号会被永久留下）
            watcher.Cancel();

            // 再把整个标志物体关掉，免得它还在后台跑浮动动画
            if (watcher.warnMark != null) watcher.warnMark.gameObject.SetActive(false);

            watcher.enabled = false;
        }

        // 换形象（换到显示用子物体上）
        // 优先用「幽灵变体」里随机挑一张，让每只跟随者长得不一样；没有的话退回玩家贴图
        if (visual != null)
        {
            if (GhostSprites != null && GhostSprites.Length > 0)
            {
                // 用「在跟随者列表里的序号」当种子 —— 换场景/重进本关后
                // 同一只幽灵长得还是同一张图，不会变来变去
                int seedIndex = Followers.IndexOf(this);
                if (seedIndex < 0) seedIndex = Followers.Count;
                visual.sprite = GhostSprites[seedIndex % GhostSprites.Length];
            }
            else if (devouredSprite != null)
            {
                visual.sprite = devouredSprite;
            }
        }

        // 朝向和尺寸都要跟玩家一致：
        //   贴图是同一张，如果这里不把 flipX / 子物体缩放对齐，
        //   就会出现"幽灵比玩家大三倍、还面朝反方向"的情况
        if (visual != null)
        {
            visual.flipX = false;
            visual.transform.localScale = Vector3.one;
        }

        if (matchPlayerScale)
        {
            transform.localScale = playerScale;
        }

        // ---- 直接出现在自己的散布位，不从这里"走过去"----
        // （原来是从 NPC 原地慢慢挪到玩家身后，看起来很怪）
        transform.position = WantedPosition();

        // ---- 从透明开始浮现 ----
        fadeTimer = 0f;
        SetAlpha(0f);

        Debug.Log("[Devour] " + name + " 成为第 " + (queueIndex + 1) + " 个跟随者（浮现中）");
    }

    /// <summary>自己被销毁时从注册表移除并重排（否则换场景后编号会一直累积）</summary>
    void OnDestroy()
    {
        if (Followers.Remove(this)) Reindex();
    }

    /// <summary>自己该待的位置：身后纵深递增，左右/上下交替错开（之字形）</summary>
    Vector3 WantedPosition()
    {
        if (target == null) return transform.position;

        // 用算好的落点表：优先不重叠，实在排不下才挤在最空的地方
        Vector2 slot;

        if (queueIndex >= 0 && queueIndex < Slots.Length)
        {
            slot = Slots[queueIndex];
        }
        else
        {
            // 兜底（落点表还没算出来时）
            slot = new Vector2(startRadius + queueIndex * minGap, 0f);
        }

        return target.position
             + new Vector3(-slot.x * facing,
                           sideOffset + slot.y,
                           0f);
    }

    /// <summary>
    /// 重新计算落点表。
    ///
    /// 贪心占位：
    ///   1. 生成玩家身后的候选点（按离玩家由近到远排序）
    ///   2. 每只依次挑「第一个离所有人都 ≥ minGap 的候选点」 —— 保证不重叠
    ///   3. 如果所有候选点都太挤，就挑「离最近的人最远」的那个 —— 重叠时也挤在最空的地方
    /// </summary>
    public void RebuildSlots(int count)
    {
        count = Mathf.Max(0, count);

        var candidates = new System.Collections.Generic.List<Vector2>();
        float back = Mathf.Max(0.1f, startRadius);
        float maxBack = Mathf.Max(back + 0.1f, maxBackDistance);

        // 直接在「玩家身后的区域」里随机撒点 —— 不是网格加扰动，所以不会排成列
        int candidateCount = Mathf.Clamp(count * 60, 300, 3000);

        for (int k = 0; k < candidateCount; k++)
        {
            float b = back + Rand(k * 3 + 1) * (maxBack - back);
            float u = (Rand(k * 5 + 2) - 0.5f) * 2f * lateralRange;

            candidates.Add(new Vector2(b, u));
        }

        // 不做「按距离排序」—— 排序会让前几只全挤在离玩家最近的一圈里，
        // 视觉上就是一条竖线。保持随机顺序，铺开在整个区域里才像一团散兵。

        var chosen = new System.Collections.Generic.List<Vector2>();

        var free = new System.Collections.Generic.List<int>();

        for (int i = 0; i < count; i++)
        {
            free.Clear();
            int fallback = -1;
            float bestClearance = -1f;

            for (int c = 0; c < candidates.Count; c++)
            {
                float nearest = float.MaxValue;

                for (int k = 0; k < chosen.Count; k++)
                {
                    float d = Vector2.Distance(candidates[c], chosen[k]);
                    if (d < nearest) nearest = d;
                }

                // ① 够宽的位置全部收集起来
                if (nearest >= minGap) free.Add(c);

                // ② 记下"最空"的那个备选
                if (nearest > bestClearance)
                {
                    bestClearance = nearest;
                    fallback = c;
                }
            }

            int pick;

            if (free.Count > 0)
            {
                // 在「所有够宽的位置」里均匀随机挑一个 —— 铺满整个区域，不会挤成一列
                int r = Mathf.FloorToInt(Rand(i * 31 + 7) * free.Count);
                if (r < 0) r = 0;
                if (r >= free.Count) r = free.Count - 1;

                pick = free[r];
            }
            else
            {
                // 全挤满了 → 放进最空的那个
                pick = fallback;
            }

            if (pick < 0) break;

            chosen.Add(candidates[pick]);
        }

        // 选完之后按「离玩家由近到远」排序：
        // 序号 0 的那只离玩家最近，后面的依次往后 —— 队伍从近到远铺开
        chosen.Sort(delegate (Vector2 a, Vector2 b)
        {
            return (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y);
        });

        Slots = chosen.ToArray();
    }

    /// <summary>把显示贴图的透明度设成 a（0 = 全透明）</summary>
    void SetAlpha(float a)
    {
        if (visual == null) return;

        var c = visual.color;
        c.a = Mathf.Clamp01(a);
        visual.color = c;
    }

    void LateUpdate()
    {
        if (!isFollower || target == null) return;

        // ---- 横向：用目标在 X 上的位移判断"身后"是哪边 ----
        float dx = target.position.x - lastTargetPos.x;
        if (Mathf.Abs(dx) > 0.0005f) facing = Mathf.Sign(dx);
        lastTargetPos = target.position;

        Vector3 wanted = WantedPosition();

        transform.position = Vector3.MoveTowards(
            transform.position, wanted, moveSpeed * Time.deltaTime);

        // ---- 浮现：先等新 NPC 刷新出来，再从透明慢慢变实体 ----
        if (fadeTimer >= 0f)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01((fadeTimer - revealDelay) / Mathf.Max(0.05f, fadeInDuration));
            SetAlpha(t);

            if (t >= 1f) fadeTimer = -1f;
        }
    }

    /// <summary>立即开始浮现（恢复旧幽灵时用，不需要等刷新）</summary>
    public void RevealImmediately()
    {
        revealDelay = 0f;
        fadeTimer = 0f;
        SetAlpha(0f);
    }

    /// <summary>0~1 的确定性伪随机（同一个 seed 永远得到同一个值）</summary>
    public static float Rand(int seed)
    {
        float v = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
