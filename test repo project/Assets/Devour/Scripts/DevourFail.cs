using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 夜晚（吞噬）阶段的统一失败处理。
///
/// 两种触发来源，效果完全一样：
///   1. 蓄力时被 NPC 回头发现（LookBackWatcher.onCaught）
///   2. 夜晚结束时没达成当天目标（OnCountdownFinish 调用）
///
/// 失败后：显示提示 → 停住月亮倒计时 → 延迟重开这一夜。
/// </summary>
public class DevourFail : MonoBehaviour
{
    [Header("显示")]
    [Tooltip("失败时出现的提示物体（比如一个红色的「失败」文本）")]
    public GameObject failMark;

    [Tooltip("失败时额外打印的说明")]
    public bool logOnFail = true;

    [Header("失败后")]
    [Tooltip("是否重开当前场景（重来这一夜）")]
    public bool restartScene = true;

    [Tooltip("失败后等多久再重开（弹出失败菜单时这个值不起作用）")]
    public float restartDelay = 2f;

    [Tooltip("弹出「重来 / 返回开始界面」菜单，而不是自动重开")]
    public bool showMenu = true;

    [Tooltip("失败时是否停住月亮倒计时")]
    public bool pauseCountdown = true;

    [Header("事件")]
    public UnityEvent onFail;

    // ================= 状态 =================
    private bool failed;

    /// <summary>是否已经失败过（防止重复触发）</summary>
    public bool HasFailed { get { return failed; } }

    void Awake()
    {
        if (failMark != null) failMark.SetActive(false);
    }

    /// <summary>被 NPC 回头发现</summary>
    public void FailByCaught()
    {
        Fail("蓄力时被发现了");
    }

    /// <summary>夜晚结束但目标没达成</summary>
    public void FailByTargetMissed()
    {
        Fail("今晚没能达成目标");
    }

    /// <summary>通用失败入口</summary>
    public void Fail(string reason)
    {
        if (failed) return;
        failed = true;

        if (logOnFail) Debug.LogWarning("[Devour] 游戏失败：" + reason);

        if (pauseCountdown)
        {
            var mc = FindAnyObjectByType<MoonCountdown>();
            if (mc != null) mc.Pause();
        }

        if (failMark != null) failMark.SetActive(!showMenu);   // 用菜单时就不再显示旧的失败文字

        if (onFail != null) onFail.Invoke();

        if (showMenu)
        {
            // 让玩家自己选：重来 / 返回开始界面
            FailMenu.Show(reason);
            return;
        }

        if (restartScene) Invoke("RestartNight", Mathf.Max(0f, restartDelay));
    }

    void RestartNight()
    {
        // 变黑过场之后再重开这一夜
        ScreenFade.LoadWithFade(SceneManager.GetActiveScene().name);
    }
}
