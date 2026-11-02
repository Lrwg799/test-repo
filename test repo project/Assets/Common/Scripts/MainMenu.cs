using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 开始界面。
///
/// 两个按钮各自调用一个 public 方法，在 Inspector 的 Button → On Click() 里绑定：
///   · 开始游戏 → StartGame()
///   · 退出游戏 → QuitGame()
///
/// 进游戏走 ScreenFade 的黑屏过渡，和其它场景切换保持一致。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("流程")]
    [Tooltip("点「开始游戏」进入哪个场景")]
    public string firstScene = "SampleScene";

    [Tooltip("淡出时长，<=0 表示用 ScreenFade 的默认值")]
    public float fadeOutDuration = 0f;

    [Header("表现")]
    [Tooltip("整组按钮，进场时淡入（可留空）")]
    public CanvasGroup buttonsGroup;

    [Tooltip("按钮组淡入时长")]
    public float introFadeDuration = 0.6f;

    private float introTimer;
    private bool introPlaying;

    void Start()
    {
        if (buttonsGroup != null)
        {
            buttonsGroup.alpha = 0f;
            introPlaying = true;
            introTimer = 0f;
        }
    }

    void Update()
    {
        if (!introPlaying || buttonsGroup == null) return;

        introTimer += Time.unscaledDeltaTime;
        buttonsGroup.alpha = Mathf.Clamp01(introTimer / Mathf.Max(0.01f, introFadeDuration));

        if (introTimer >= introFadeDuration) introPlaying = false;
    }

    /// <summary>开始游戏</summary>
    public void StartGame()
    {
        Debug.Log("[MainMenu] 开始游戏 → " + firstScene);
        ScreenFade.LoadWithFade(firstScene, fadeOutDuration > 0f ? fadeOutDuration : -1f);
    }

    /// <summary>退出游戏</summary>
    public void QuitGame()
    {
        Debug.Log("[MainMenu] 退出游戏");

#if UNITY_EDITOR
        // 编辑器里 Application.Quit() 不生效，直接停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
