using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 白天（跑酷）结束 → 天数 +1 → 回到夜晚场景，即"第二天"。
///
/// 白天有两种结束方式，效果一样：
///   1. 玩家掉下去（ParkourPlayer.onDeath）
///   2. 太阳倒计时走完（MoonCountdown.onFinished，30 秒）
///
/// 挂在跑酷场景任意物体上即可，两个来源都会自动订阅。
/// </summary>
public class DayToNight : MonoBehaviour
{
    [Tooltip("要监听的跑酷玩家，留空会自动找")]
    public ParkourPlayer player;

    [Tooltip("太阳倒计时（走完也算白天结束），留空会自动找场景里的")]
    public MoonCountdown sunCountdown;

    [Tooltip("白天结束后返回的夜晚场景名（必须在 Build Settings 里）")]
    public string nightScene = "SampleScene";

    [Tooltip("延迟几秒再跳，留时间看清结束画面")]
    public float delay = 1.5f;

    private bool triggered;

    void Awake()
    {
        if (player == null) player = FindAnyObjectByType<ParkourPlayer>();
        if (sunCountdown == null) sunCountdown = FindAnyObjectByType<MoonCountdown>();

        if (player != null)
        {
            player.onDeath.AddListener(EndDay);
        }
        else
        {
            Debug.LogWarning("[DayToNight] 没找到 ParkourPlayer，掉落不会结束白天");
        }

        if (sunCountdown != null)
        {
            sunCountdown.onFinished.AddListener(EndDay);
        }
    }

    void OnDestroy()
    {
        if (player != null) player.onDeath.RemoveListener(EndDay);
        if (sunCountdown != null) sunCountdown.onFinished.RemoveListener(EndDay);
    }

    /// <summary>结束白天（掉落 / 太阳走完 都走这里；只会生效一次）</summary>
    public void EndDay()
    {
        if (triggered) return;
        triggered = true;

        DevourProgress.NextDay();

        Debug.Log("[Cycle] 白天结束 → " + delay + " 秒后返回夜晚场景「" + nightScene
                  + "」，进入第 " + DevourProgress.Day + " 天");

        Invoke("LoadNight", Mathf.Max(0f, delay));
    }

    void LoadNight()
    {
        // 变黑过场之后再切回夜晚
        ScreenFade.LoadWithFade(nightScene);
    }
}
