using UnityEngine;

/// <summary>
/// 跨场景的进度存储（夜晚/白天切换时会重新加载场景，普通字段会被清空）。
///
/// 静态字段在「一次 Play 会话」内跨场景保留，
/// 退出 Play / 重新进入 Play 时会重置（除非关闭了 Domain Reload）。
/// </summary>
public static class DevourProgress
{
    /// <summary>当前第几天（从 1 开始）</summary>
    public static int Day = 1;

    /// <summary>昨天超出目标的量，用来抵扣今天的目标</summary>
    public static int Carry = 0;

    /// <summary>累计吞噬总数</summary>
    public static int TotalDevoured = 0;

    /// <summary>白天（跑酷）这一轮收集到的月亮碎片数量，决定下一夜的长度</summary>
    public static int DayCoins = 0;

    /// <summary>清空重来</summary>
    public static void ResetAll()
    {
        Day = 1;
        Carry = 0;
        TotalDevoured = 0;
        DayCoins = 0;
    }

    /// <summary>白天结束 → 天数 +1</summary>
    public static void NextDay()
    {
        Day++;
        Debug.Log("[Cycle] 进入第 " + Day + " 天（结转 -" + Carry + "）");
    }
}
