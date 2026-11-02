using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 按 Tab 在多个场景之间循环切换。
///
/// 用法：每个场景里放一个挂着本脚本的空物体即可（两个场景都要放）。
/// 场景名必须和 Build Settings 里的一致。
/// </summary>
public class SceneSwitcher : MonoBehaviour
{
    [Header("要轮换的场景（按顺序循环）")]
    public string[] sceneNames = { "SampleScene", "ParkourScene" };

    [Header("提示")]
    [Tooltip("切换时在 Console 打一行日志")]
    public bool logOnSwitch = true;

    void Update()
    {
        if (!SwitchPressed()) return;
        if (sceneNames == null || sceneNames.Length < 2) return;

        string current = SceneManager.GetActiveScene().name;
        int index = System.Array.IndexOf(sceneNames, current);

        // 当前场景不在列表里就从第一个开始
        int next = (index < 0) ? 0 : (index + 1) % sceneNames.Length;

        if (logOnSwitch)
        {
            Debug.Log("[SceneSwitcher] " + current + " → " + sceneNames[next]);
        }

        SceneManager.LoadScene(sceneNames[next]);
    }

    bool SwitchPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.tabKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Tab);
#else
        return false;
#endif
    }
}
