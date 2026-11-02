using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 按 Esc 返回主菜单。
///
/// 用法：游戏内的两个场景（吞噬 / 跑酷）各放一个，通常挂在 SceneSwitcher 那个物体上。
/// 返回时会清空进度（回到"全新一局"），不需要的话关掉 resetProgress。
/// </summary>
public class BackToMenu : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("返回哪个场景")]
    public string menuScene = "MainMenuScene";

    [Tooltip("返回时是否清空天数 / 目标 / 碎片进度")]
    public bool resetProgress = true;

    [Tooltip("按键时打日志")]
    public bool logOnPress = true;

    private bool leaving;

    void Update()
    {
        if (leaving) return;
        if (!EscPressed()) return;

        leaving = true;

        if (resetProgress) DevourProgress.ResetAll();

        if (logOnPress) Debug.Log("[BackToMenu] 返回主菜单（进度已重置）");

        ScreenFade.LoadWithFade(menuScene);
    }

    bool EscPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
