using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 「路人回头」事件 + 蓄力被抓判定 + 回头转身 + 预警标志 + 随机性。
///
/// 判定规则：
///   1. 触发回头 → 先给玩家 graceTime（默认 0.5 秒）容错期，期间蓄力不算被发现。
///   2. 容错期一过 → 只要玩家还在蓄力，立刻判定被发现 → 失败。
///   3. 回头持续 watchDuration 秒（含容错期）。期间没被抓到就安全结束。
///
/// 预警与随机性（Trigger 入口）：
///   · suddenChance 概率：不放预警，直接突然回头
///   · 否则先亮出头顶警告标志（warnDuration 秒）
///       · 预警结束时 bluffChance 概率：虚晃一枪，不回头
///       · 否则真的回头
///
/// 朝向：平常背对玩家（flipXAway），回头时转身面向玩家（flipXFacing）。
/// </summary>
public class LookBackWatcher : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动在场景里找 ChargeController")]
    public ChargeController charge;

    [Header("时间设定")]
    [Tooltip("回头后的容错时间（秒）：这段时间内蓄力不算被发现")]
    public float graceTime = 0.5f;

    [Tooltip("回头总共持续多少秒（含前面的容错期）")]
    public float watchDuration = 3f;

    [Header("三阶段警觉度")]
    [Tooltip("勾上后：回头期间还在蓄力不再立刻失败，而是先攒满警觉度（给玩家反应时间）")]
    public bool useSuspicionMeter = true;

    [Tooltip("第 1 阶段「疑惑」持续多久（头顶黄问号，此时停手还能救回来）")]
    public float doubtTime = 0.8f;

    [Tooltip("第 2 阶段「怀疑」持续多久（头顶橙叹号，条在涨）")]
    public float suspicionTime = 1f;

    [Tooltip("停手之后警觉度多久清空")]
    public float recoverTime = 0.7f;

    [Tooltip("自动回头间隔（秒）。0 = 关闭，只能由外部调用 Trigger() / LookBack()")]
    public float autoInterval = 0f;

    [Tooltip("间隔随机浮动比例：实际间隔 = autoInterval × (1 ± 该值)。0.35 = 4 秒可能变成 2.6~5.4 秒")]
    [Range(0f, 1f)]
    public float intervalJitter = 0.35f;

    [Tooltip("开局时计时器从随机位置开始 —— 避免每次都有固定长度的安全开局时间")]
    public bool randomizeStartTimer = true;

    [Header("随夜晚数递减（指数衰减：先快后慢）")]
    [Tooltip("勾上后，autoInterval 会按当前夜晚数自动计算（覆盖上面的手动值）")]
    public bool scaleWithNight = true;

    [Tooltip("第 1 夜的间隔")]
    public float startInterval = 4.5f;

    [Tooltip("无限往后趋近的下限")]
    public float minInterval = 3f;

    [Tooltip("衰减速度。1.67 时：第 4 夜已贴近下限，之后每夜变化 < 0.01")]
    public float decayRate = 1.67f;

    [Header("感知范围（靠近才回头）")]
    [Tooltip("玩家进入这个半径时触发一次。0 = 关闭")]
    public float senseRadius = 0f;

    [Tooltip("要感知的玩家。留空会自动取 ChargeController 所在的物体")]
    public Transform player;

    [Header("预警标志")]
    [Tooltip("头顶的警告图标 SpriteRenderer（平时隐藏，预警时亮起）")]
    public SpriteRenderer warnMark;

    [Tooltip("预警持续多久才决定要不要回头")]
    public float warnDuration = 0.8f;

    [Tooltip("【随机】不放预警、直接突然回头的概率（0~1）")]
    [Range(0f, 1f)]
    public float suddenChance = 0.25f;

    [Tooltip("【随机】出现预警、但最后不回头（虚晃一枪）的概率（0~1）")]
    [Range(0f, 1f)]
    public float bluffChance = 0.3f;

    [Header("朝向（回头转身）")]
    [Tooltip("NPC 的显示用 SpriteRenderer。留空会自动找子物体上的")]
    public SpriteRenderer facingVisual;

    [Tooltip("平常（背对玩家）时的 flipX")]
    public bool flipXAway = true;

    [Tooltip("回头（面向玩家）时的 flipX")]
    public bool flipXFacing = false;

    [Header("事件")]
    [Tooltip("头顶出现预警标志的那一刻")]
    public UnityEvent onWarn;

    [Tooltip("预警结束但没回头")]
    public UnityEvent onBluff;

    [Tooltip("路人回头的那一刻")]
    public UnityEvent onLookBack;

    [Tooltip("容错期刚刚结束")]
    public UnityEvent onGraceEnded;

    [Tooltip("容错期过后还在蓄力 → 被发现 → 游戏失败")]
    public UnityEvent onCaught;

    [Tooltip("回头结束，且全程没被抓到")]
    public UnityEvent onSafe;

    // ================= 运行时状态 =================
    private bool watching;      // 正在回头
    private bool warning;       // 正在亮预警标志
    private float watchTimer;
    private float warnTimer;
    private float graceTimer;
    private float autoTimer;
    private float nextInterval;   // 本轮实际间隔（带随机浮动）
    private bool wasInRange;

    // ---- 全局回头节拍 ----
    // 关键：计时器不能跟着 NPC 走。否则"吞掉 → 换新 NPC → 计时器清零"，
    // 而玩家 3 秒就能吞一个、危险窗口要 4.7 秒才到，NPC 永远等不到回头机会。
    // 改成静态时钟后，台上的 NPC 只是"执行者"，节拍始终连续。
    private static float globalTimer;
    private static bool globalTimerStarted;
    private static float globalNextInterval;
    private static bool globalWarning;
    private static float globalWarnTimer;

    /// <summary>换一只 NPC 时重置全局节拍（回到夜晚/重开时用）</summary>
    public static void ResetGlobalRhythm()
    {
        globalTimer = 0f;
        globalTimerStarted = false;
        globalNextInterval = 0f;
        globalWarning = false;
        globalWarnTimer = 0f;
    }

    public bool IsWatching { get { return watching; } }
    public bool IsWarning { get { return warning; } }

    /// <summary>警觉度 0~1。1 = 被抓</summary>
    public float Suspicion01
    {
        get
        {
            float total = Mathf.Max(0.01f, doubtTime + suspicionTime);
            return Mathf.Clamp01(suspicion / total);
        }
    }

    /// <summary>0 = 安全，1 = 疑惑，2 = 怀疑，3 = 暴露</summary>
    public int StageIndex
    {
        get
        {
            if (suspicion <= 0.001f) return 0;
            if (suspicion < doubtTime) return 1;
            if (suspicion < doubtTime + suspicionTime) return 2;
            return 3;
        }
    }

    public string StageName
    {
        get
        {
            switch (StageIndex)
            {
                case 1: return "疑惑";
                case 2: return "怀疑";
                case 3: return "暴露";
                default: return "安全";
            }
        }
    }

    private float suspicion;

    private Color warnBaseColor = Color.clear;
    private Vector3 warnBaseScale = Vector3.zero;
    public bool InGrace { get { return watching && graceTimer > 0f; } }

    public float GraceRemain { get { return graceTimer > 0f ? graceTimer : 0f; } }
    public float WatchRemain { get { return watching ? watchTimer : 0f; } }
    public float WarnRemain { get { return warning ? warnTimer : 0f; } }

    void Awake()
    {
        if (charge == null) charge = FindAnyObjectByType<ChargeController>();
        if (player == null && charge != null) player = charge.transform;

        if (facingVisual == null) facingVisual = GetComponentInChildren<SpriteRenderer>(true);
        if (facingVisual == null) facingVisual = GetComponent<SpriteRenderer>();

        // 记下预警标志原本的颜色/大小，警觉度视觉结束后要还原
        if (warnMark != null)
        {
            warnBaseColor = warnMark.color;
            warnBaseScale = warnMark.transform.localScale;
        }
    }

    void Start()
    {
        // 按当前夜晚数调整回头间隔（必须在初始化全局节拍之前）
        ApplyNightScaling();

        // 一开始玩家就在范围内的话，不算"走进来"，不立刻触发
        if (senseRadius > 0f && player != null)
        {
            wasInRange = Vector2.Distance(transform.position, player.position) <= senseRadius;
        }

        ApplyFacing(false);
        SetWarnVisible(false);

        // 全局节拍：只有第一只 NPC 负责起拍，之后换 NPC 也沿用原来的时钟
        if (!globalTimerStarted)
        {
            globalNextInterval = RollInterval();
            globalTimer = randomizeStartTimer ? Random.Range(0f, globalNextInterval) : 0f;
            globalTimerStarted = true;
        }

        nextInterval = globalNextInterval;
        autoTimer = globalTimer;
    }

    /// <summary>
    /// 按当前夜晚数计算回头间隔：指数衰减，先快后慢。
    ///
    ///   间隔 = min + (start - min) × e^(-k × (夜数-1))
    ///
    /// 取 k = 1.67 时：
    ///   第1夜 4.500 → 第2夜 3.282 → 第3夜 3.053 → 第4夜 3.010 → 第5夜 3.002
    /// 相邻两夜的差值：1.218 / 0.229 / 0.043 / 0.008 / 0.002
    /// —— 第 4 夜之后每夜变化都小于 0.01，几乎察觉不到。
    /// </summary>
    void ApplyNightScaling()
    {
        if (!scaleWithNight) return;

        int night = Mathf.Max(1, DevourProgress.Day);
        autoInterval = minInterval
                     + (startInterval - minInterval) * Mathf.Exp(-decayRate * (night - 1));

        Debug.Log("[LookBack] 第 " + night + " 夜 → 回头间隔 "
                  + autoInterval.ToString("0.000") + " 秒");
    }

    /// <summary>掷一个带随机浮动的间隔</summary>
    float RollInterval()
    {
        if (autoInterval <= 0f) return float.MaxValue;

        float j = autoInterval * intervalJitter;
        float v = autoInterval + Random.Range(-j, j);
        return Mathf.Max(0.5f, v);
    }

    void Update()
    {
        HandleSense();

        // ---- 自动回头（走全局时钟，换 NPC 不重置）----
        if (autoInterval > 0f && !watching && !warning)
        {
            globalTimer += Time.deltaTime;
            autoTimer = globalTimer;

            if (globalTimer >= globalNextInterval)
            {
                globalTimer = 0f;
                autoTimer = 0f;
                globalNextInterval = RollInterval();
                nextInterval = globalNextInterval;
                Trigger();
            }
        }

        // ---- 预警阶段：时间到了再掷一次骰子 ----
        if (warning)
        {
            warnTimer -= Time.deltaTime;
            if (warnTimer <= 0f)
            {
                warning = false;
                SetWarnVisible(false);

                if (Random.value < bluffChance)
                {
                    if (onBluff != null) onBluff.Invoke();
                    Debug.Log("[LookBack] 预警结束 —— 虚晃一枪，它没有回头");
                }
                else
                {
                    LookBack();
                }
            }
            return;
        }

        if (!watching) return;

        if (watchTimer > 0f) watchTimer -= Time.deltaTime;

        if (graceTimer > 0f)
        {
            graceTimer -= Time.deltaTime;
            if (graceTimer <= 0f)
            {
                graceTimer = 0f;
                if (onGraceEnded != null) onGraceEnded.Invoke();
            }
        }
        else
        {
            bool charging = charge != null && charge.IsCharging;

            if (!useSuspicionMeter)
            {
                // 老行为：容错期一过还在蓄力 → 立刻失败
                if (charging)
                {
                    Caught();
                    return;
                }
            }
            else
            {
                float total = Mathf.Max(0.01f, doubtTime + suspicionTime);

                if (charging)
                {
                    // 正在蓄力 → 警觉度上涨（疑惑 → 怀疑 → 暴露）
                    suspicion += Time.deltaTime;
                }
                else
                {
                    // 停手 → 警觉度回落，能在 recoverTime 内清空就没事
                    suspicion -= Time.deltaTime * (total / Mathf.Max(0.05f, recoverTime));
                }

                suspicion = Mathf.Clamp(suspicion, 0f, total);

                if (suspicion >= total)
                {
                    Caught();
                    return;
                }
            }
        }

        UpdateSuspicionVisual();

        if (watchTimer <= 0f) SafeEnd();
    }

    /// <summary>靠近才触发：只在「刚进入」半径那一刻触发一次</summary>
    void HandleSense()
    {
        if (senseRadius <= 0f || player == null) return;

        bool inRange = Vector2.Distance(transform.position, player.position) <= senseRadius;

        if (inRange && !wasInRange && !watching && !warning)
        {
            Trigger();
        }

        wasInRange = inRange;
    }

    /// <summary>
    /// 触发一次「可能要回头」——带预警和随机性。
    /// 靠近 / 定时器 / 外部调用都走这里。
    /// </summary>
    public void Trigger()
    {
        if (watching || warning) return;

        // ① 不放预警，直接突然回头
        if (Random.value < suddenChance)
        {
            Debug.Log("[LookBack] ⚠ 没有预警 —— 突然回头！");
            LookBack();
            return;
        }

        // ② 先亮出头顶的警告标志
        warning = true;
        warnTimer = warnDuration;
        SetWarnVisible(true);

        if (onWarn != null) onWarn.Invoke();
        Debug.Log("[LookBack] 头顶出现预警标志（" + warnDuration + " 秒后决定是否回头）");
    }

    void SetWarnVisible(bool visible)
    {
        if (warnMark != null) warnMark.enabled = visible;
    }

    /// <summary>
    /// 三阶段警觉度的视觉反馈。
    ///
    /// 关键：只要 NPC 在回头，标志就一直显示 —— 玩家随时能看到当前处于哪一档。
    ///   安全（0）→ 淡蓝小问号：它在看，但没发现你
    ///   疑惑（1）→ 黄色问号，渐亮
    ///   怀疑（2）→ 橙→红，放大 + 14Hz 闪烁
    /// </summary>
    void UpdateSuspicionVisual()
    {
        if (warnMark == null) return;

        if (!watching)
        {
            RestoreWarnMark();
            return;
        }

        SetWarnVisible(true);

        int stage = StageIndex;
        float total = Mathf.Max(0.01f, doubtTime + suspicionTime);
        float t = Mathf.Clamp01(suspicion / total);

        Color c;
        float scaleMul;

        if (stage <= 0)
        {
            // 安全：淡蓝色的小问号 —— 让玩家知道"它在看，但还没起疑"
            c = new Color(0.8f, 0.9f, 1f, 0.35f);
            scaleMul = 0.55f;
        }
        else if (stage == 1)
        {
            // 疑惑：黄色，越来越亮
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, doubtTime / total));
            c = new Color(1f, 0.95f, 0.35f, Mathf.Lerp(0.6f, 0.95f, k));
            scaleMul = Mathf.Lerp(0.75f, 1f, k);
        }
        else
        {
            // 怀疑：橙 → 红，放大并快速闪
            c = Color.Lerp(new Color(1f, 0.68f, 0.15f), new Color(1f, 0.2f, 0.15f),
                           Mathf.InverseLerp(0.44f, 1f, t));
            c.a = Mathf.Clamp01(0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 14f)));
            scaleMul = Mathf.Lerp(1f, 1.28f, Mathf.InverseLerp(0.44f, 1f, t));
        }

        warnMark.color = c;

        if (warnBaseScale != Vector3.zero)
            warnMark.transform.localScale = warnBaseScale * scaleMul;
    }

    /// <summary>恢复预警标志原本的颜色和大小</summary>
    void RestoreWarnMark()
    {
        if (warnMark == null) return;

        if (warnBaseColor.a > 0f) warnMark.color = warnBaseColor;
        if (warnBaseScale != Vector3.zero) warnMark.transform.localScale = warnBaseScale;

        if (!warning) SetWarnVisible(false);
    }

    /// <summary>切换朝向：true = 面向玩家，false = 背对玩家</summary>
    void ApplyFacing(bool facingPlayer)
    {
        if (facingVisual == null) return;
        facingVisual.flipX = facingPlayer ? flipXFacing : flipXAway;
    }

    /// <summary>触发一次「路人回头」（跳过预警，立刻回头）</summary>
    public void LookBack()
    {
        if (watching) return;

        warning = false;
        SetWarnVisible(false);

        watching = true;
        watchTimer = watchDuration;
        graceTimer = graceTime;
        suspicion = 0f;

        ApplyFacing(true);   // 回头看玩家

        if (onLookBack != null) onLookBack.Invoke();
        Debug.Log("[LookBack] 路人回头！容错 " + graceTime + " 秒");
    }

    void Caught()
    {
        watching = false;
        graceTimer = 0f;
        watchTimer = 0f;
        suspicion = 0f;

        SetWarnVisible(false);
        if (warnBaseColor.a > 0f) warnMark.color = warnBaseColor;
        if (warnBaseScale != Vector3.zero && warnMark != null) warnMark.transform.localScale = warnBaseScale;

        ApplyFacing(false);
        if (onCaught != null) onCaught.Invoke();

        Debug.LogWarning("[LookBack] 警觉度满了（暴露）—— 被发现，游戏失败");
    }

    void SafeEnd()
    {
        watching = false;
        graceTimer = 0f;
        watchTimer = 0f;
        suspicion = 0f;

        SetWarnVisible(false);
        if (warnMark != null)
        {
            if (warnBaseColor.a > 0f) warnMark.color = warnBaseColor;
            if (warnBaseScale != Vector3.zero) warnMark.transform.localScale = warnBaseScale;
        }

        ApplyFacing(false);   // 转回去继续背对玩家
        if (onSafe != null) onSafe.Invoke();

        Debug.Log("[LookBack] 回头结束，没被发现，安全");
    }

    /// <summary>
    /// 给一段"喘息时间"：接下来 seconds 秒内不会自动回头。
    /// 新 NPC 刚登场、或玩家刚吞完一只时用，避免一上来就回头。
    /// </summary>
    public void GiveGrace(float seconds)
    {
        globalNextInterval = globalTimer + Mathf.Max(0f, seconds);
        globalWarning = false;
        globalWarnTimer = 0f;
    }

    /// <summary>外部强制中断（重开场景、重置关卡）</summary>
    public void Cancel()
    {
        watching = false;
        warning = false;
        graceTimer = 0f;
        watchTimer = 0f;
        warnTimer = 0f;
        autoTimer = 0f;
        suspicion = 0f;
        nextInterval = RollInterval();

        if (warnMark != null)
        {
            if (warnBaseColor.a > 0f) warnMark.color = warnBaseColor;
            if (warnBaseScale != Vector3.zero) warnMark.transform.localScale = warnBaseScale;
        }

        SetWarnVisible(false);
        ApplyFacing(false);
    }
}
