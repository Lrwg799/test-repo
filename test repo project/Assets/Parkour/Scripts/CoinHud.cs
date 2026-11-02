using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 白天跑酷阶段的月亮碎片计数 HUD。
///
/// 挂在跑酷场景的 Canvas 上即可。
/// 进入场景时会把 DevourProgress.DayCoins 归零（新一轮白天从 0 开始收集）。
/// </summary>
public class CoinHud : MonoBehaviour
{
    [Tooltip("显示数量的文本")]
    public Text label;

    [Tooltip("格式，{0} = 已拾取数量")]
    public string format = "月亮 × {0}";

    [Tooltip("进入白天时把计数归零")]
    public bool resetOnStart = true;

    void Awake()
    {
        if (resetOnStart) DevourProgress.DayCoins = 0;
    }

    void Update()
    {
        if (label != null)
        {
            label.text = string.Format(format, DevourProgress.DayCoins);
        }
    }
}
