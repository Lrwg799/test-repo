using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 夜晚（吞噬）阶段的目标显示。
///
/// 一轮循环 = 一夜 + 一昼：
///   夜晚：在限时内尽力吞噬，目标是「第 N 天的配额」
///   白天：跑酷，结束时天数 +1，回到夜晚
///
/// 配额规则：
///   第 1 天 2 个，之后每天 +1（2 / 3 / 4 / 5 …），目标数值固定不变
///   夜晚结束时，超出目标的部分会保留下来，第二天直接算进「已吞噬」的数量
///   （例：第 1 天目标 2，吞了 5 → 溢出 3 → 第 2 天目标仍是 3，但开局就显示 3/3）
///
/// 显示格式：吞噬数量/目标数量（例如 1/3）
/// 进度（天数、溢出、累计数）存在 DevourProgress 静态类里，跨场景保留。
/// </summary>
public class DevourTarget : MonoBehaviour
{
    [Header("配额")]
    [Tooltip("第 1 天的目标数量")]
    public int baseTargetPerDay = 2;

    [Tooltip("之后每天增加多少")]
    /// <summary>每夜的配额递增基数（X 从 1 开始，每夜 +1 → 三角增长）</summary>
    public int increasePerDay = 2;

    [Tooltip("溢出结转上限的比例：最多把「当夜目标 × 该值」带到下一夜。0.5 = 一半")]
    [Range(0f, 1f)]
    public float carryRatio = 0.5f;

    [Tooltip("留空会自动找场景里的 NPCDevourer")]
    public NPCDevourer devourer;

    [Header("显示")]
    [Tooltip("主文本：吞噬数量/目标数量")]
    public Text label;

    [Tooltip("{0} = 今晚已吞噬，{1} = 今晚目标")]
    public string format = "{0}/{1}";

    [Tooltip("可选的次要文本：显示第几天")]
    public Text dayLabel;

    [Tooltip("{0} = 当前天数")]
    public string dayFormat = "第 {0} 天";

    [Header("颜色")]
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.85f, 0.3f);

    [Header("事件")]
    [Tooltip("今晚达到目标时触发一次")]
    public UnityEvent onTargetMet;

    [Tooltip("每吞噬一个触发，参数是今晚已吞噬数")]
    public UnityEvent<int> onDevoured;

    // ================= 状态 =================
    private int devouredTonight;
    private bool metFired;

    /// <summary>今晚已吞噬多少</summary>
    public int DevouredTonight { get { return devouredTonight; } }

    /// <summary>当前第几天（跨场景保留）</summary>
    public int CurrentDay { get { return DevourProgress.Day; } }

    /// <summary>昨晚超出目标的量，会直接算进今天的已吞噬数</summary>
    public int Carry { get { return DevourProgress.Carry; } }

    /// <summary>
    /// 第 day 天的配额。
    ///
    /// 增长方式：每过一夜配额 +X，而 X 自身也 +1（X 从 1 开始）。
    ///   X(1)=1 → 第 2 夜 +1
    ///   X(2)=2 → 第 3 夜 +2
    ///   X(3)=3 → 第 4 夜 +3
    /// 所以是三角形增长：配额(day) = 基础 + (day-1)×day÷2
    ///   第1夜 2 → 第2夜 3 → 第3夜 5 → 第4夜 8 → 第5夜 12 …
    /// </summary>
    public int BaseTargetFor(int day)
    {
        if (day < 1) day = 1;
        return baseTargetPerDay + (day - 1) * day / 2;
    }

    /// <summary>当晚的 X（每夜 +1，从 1 开始）</summary>
    public int XFor(int day)
    {
        return Mathf.Max(1, day);
    }

    /// <summary>从第 day-1 夜进入第 day 夜时，配额增加了多少（= 上一夜的 X）</summary>
    public int IncrementFor(int day)
    {
        return day <= 1 ? 0 : XFor(day - 1);
    }

    /// <summary>今晚的目标：固定按天数算，不会因为昨天吞多了而变小</summary>
    public int TargetTonight
    {
        get { return Mathf.Max(0, BaseTargetFor(CurrentDay)); }
    }

    /// <summary>今晚还差几个</summary>
    public int RemainingTonight { get { return Mathf.Max(0, TargetTonight - devouredTonight); } }

    /// <summary>今晚是否已达标</summary>
    public bool IsTargetMet { get { return devouredTonight >= TargetTonight; } }

    void Awake()
    {
        if (devourer == null) devourer = FindAnyObjectByType<NPCDevourer>();

        if (devourer != null)
        {
            devourer.onDevoured.AddListener(OnDevoured);
        }
        else
        {
            Debug.LogWarning("[DevourTarget] 没找到 NPCDevourer，计数不会增加");
        }
    }

    void OnDestroy()
    {
        if (devourer != null)
        {
            devourer.onDevoured.RemoveListener(OnDevoured);
        }
    }

    void Start()
    {
        // 回到夜晚 = 新的一夜。
        // 昨晚超出目标的部分，直接算进今天「已吞噬」的数量（目标数值本身不变）
        devouredTonight = Mathf.Max(0, DevourProgress.Carry);
        metFired = devouredTonight >= TargetTonight;

        if (DevourProgress.Day <= 0) DevourProgress.ResetAll();

        Refresh();

        if (devouredTonight > 0)
        {
            Debug.Log("[DevourTarget] 第 " + CurrentDay + " 天开始：昨晚溢出 " + devouredTonight
                      + " 个已计入 → " + devouredTonight + "/" + TargetTonight);
        }
    }

    void OnDevoured()
    {
        devouredTonight++;
        DevourProgress.TotalDevoured++;

        Refresh();

        if (onDevoured != null) onDevoured.Invoke(devouredTonight);

        if (!metFired && IsTargetMet)
        {
            metFired = true;
            Debug.Log("[DevourTarget] 第 " + CurrentDay + " 天目标达成（"
                      + devouredTonight + "/" + TargetTonight + "）");
            if (label != null) label.color = highlightColor;
            if (onTargetMet != null) onTargetMet.Invoke();
        }
    }

    void Refresh()
    {
        if (label != null)
        {
            label.text = string.Format(format, devouredTonight, TargetTonight);
            if (!metFired) label.color = normalColor;
        }

        if (dayLabel != null)
        {
            dayLabel.text = string.Format(dayFormat, CurrentDay);
        }
    }

    /// <summary>
    /// 夜晚结束时调用：把超出目标的部分记下来，第二天直接算作「已吞噬」。
    /// 由 OnCountdownFinish 在切换场景前调用。
    /// </summary>
    public void CommitNight()
    {
        int target = TargetTonight;
        int rawExcess = Mathf.Max(0, devouredTonight - target);

        // 溢出结转上限：最多只带走进度的一半，避免"一夜超神 → 后面几夜免打"
        int carryCap = Mathf.Max(0, Mathf.FloorToInt(target * Mathf.Clamp01(carryRatio)));
        int excess = Mathf.Min(rawExcess, carryCap);

        DevourProgress.Carry = excess;
        int nextTarget = BaseTargetFor(CurrentDay + 1);

        Debug.Log("[DevourTarget] 第 " + CurrentDay + " 夜结算：吞噬 " + devouredTonight
                  + " / 目标 " + target + " → 超出 " + rawExcess
                  + "，结转 " + excess + "（上限 " + target + " × " + carryRatio + " = " + carryCap + "）"
                  + " → 第 " + (CurrentDay + 1) + " 夜目标 " + nextTarget
                  + "，开局记 " + excess + "/" + nextTarget);
    }

    /// <summary>清空当前这一夜的计数（不改变天数/结转）</summary>
    public void ResetTonight()
    {
        devouredTonight = 0;
        metFired = false;
        Refresh();
    }
}
