using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 倒计时归零时切到指定场景。
/// 挂在 MoonCountdown 同一个物体上即可（会自动订阅它的 onFinished）。
/// </summary>
public class OnCountdownFinish : MonoBehaviour
{
    [Tooltip("要监听的倒计时，留空 = 本物体上的 MoonCountdown")]
    public MoonCountdown countdown;

    [Tooltip("时间到之后跳转到哪个场景（必须在 Build Settings 里）")]
    public string sceneName = "ParkourScene";

    [Tooltip("延迟几秒再跳，留点时间看月亮走完最后一格")]
    public float delay = 0.5f;

    [Tooltip("切换前先结算这一夜的目标（把超出部分结转给第二天）。留空会自动找")]
    public DevourTarget target;

    [Tooltip("目标没达成时调用的失败处理。留空会自动找")]
    public DevourFail failHandler;

    void Awake()
    {
        if (countdown == null) countdown = GetComponent<MoonCountdown>();

        if (countdown != null)
        {
            countdown.onFinished.AddListener(OnFinished);
        }
        else
        {
            Debug.LogWarning("[OnCountdownFinish] 没找到 MoonCountdown，不会跳转");
        }
    }

    void OnDestroy()
    {
        if (countdown != null)
        {
            countdown.onFinished.RemoveListener(OnFinished);
        }
    }

    void OnFinished()
    {
        if (target == null) target = FindAnyObjectByType<DevourTarget>();
        if (failHandler == null) failHandler = FindAnyObjectByType<DevourFail>();

        // 没达成当天目标 → 和"蓄力被回头发现"一样，视为失败，不进入白天
        if (target != null && !target.IsTargetMet)
        {
            Debug.LogWarning("[Cycle] 夜晚结束，但目标未达成："
                             + target.DevouredTonight + "/" + target.TargetTonight);

            if (failHandler != null)
            {
                failHandler.FailByTargetMissed();
                return;
            }
        }

        // 达成 → 结算这一夜的目标：超出部分结转给第二天
        if (target != null) target.CommitNight();

        Debug.Log("[OnCountdownFinish] 夜晚结束 → " + sceneName
                  + (delay > 0f ? "（" + delay + " 秒后跳转）" : ""));
        Invoke("LoadScene", Mathf.Max(0f, delay));
    }

    void LoadScene()
    {
        // 变黑过场之后再切场景
        ScreenFade.LoadWithFade(sceneName);
    }
}
