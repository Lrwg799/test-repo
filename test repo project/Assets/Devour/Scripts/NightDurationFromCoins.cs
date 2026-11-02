using UnityEngine;

/// <summary>
/// 用白天收集到的月亮碎片数量决定这一夜的时长。
///
/// 规则：
///   夜晚时长 = 基础时长 + 碎片数 × 每个碎片秒数
///   第 1 夜固定为 firstNightSeconds（默认 30 秒），不受碎片影响
///
/// 挂在 MoonCountdown 同一个物体上，在 Awake 里改它的 duration，
/// 这样 MoonCountdown 自己的 Start/ResetCountdown 就会用新时长。
/// </summary>
public class NightDurationFromCoins : MonoBehaviour
{
    [Tooltip("留空 = 本物体上的 MoonCountdown")]
    public MoonCountdown countdown;

    [Header("时长公式")]
    [Tooltip("基础时长（没捡到碎片也有的时间）")]
    public float baseSeconds = 10f;

    [Tooltip("每个碎片增加多少秒")]
    public float secondsPerCoin = 0.5f;

    [Tooltip("夜晚最短多少秒（防止公式算出负数或过短）")]
    public float minimumSeconds = 10f;

    [Header("第一夜")]
    [Tooltip("第 1 夜固定多少秒。<=0 表示不特殊处理，也按公式算")]
    public float firstNightSeconds = 30f;

    [Tooltip("是否打日志")]
    public bool logResult = true;

    void Awake()
    {
        if (countdown == null) countdown = GetComponent<MoonCountdown>();
        if (countdown == null) return;

        int day = Mathf.Max(1, DevourProgress.Day);
        int coins = DevourProgress.DayCoins;

        float byCoins = baseSeconds + coins * secondsPerCoin;
        float final = Mathf.Max(minimumSeconds, byCoins);
        bool firstNight = day <= 1 && firstNightSeconds > 0f;

        if (firstNight) final = firstNightSeconds;

        countdown.duration = final;

        if (logResult)
        {
            if (firstNight)
            {
                Debug.Log("[夜晚时长] 第 1 夜固定 " + firstNightSeconds + " 秒（碎片 "
                          + coins + " 个不参与计算）");
            }
            else
            {
                Debug.Log("[夜晚时长] 第 " + day + " 夜：基础 " + baseSeconds + " 秒 + 碎片 "
                          + coins + " × " + secondsPerCoin + " = " + byCoins.ToString("0.0")
                          + " 秒 → 实际 " + final.ToString("0.0") + " 秒");
            }
        }
    }
}
