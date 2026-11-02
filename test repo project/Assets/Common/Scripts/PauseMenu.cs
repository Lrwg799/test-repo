using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 游戏中按 ESC 弹出的暂停菜单：继续游戏 / 重新开始本场景。
///
/// UI 运行时生成，自动挂到任何非主菜单场景，不需要在场景里摆物件。
/// 注意：原来挂在 SceneSwitcher 上的 BackToMenu 也在抢 ESC 键，需要关掉它。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("文字")]
    public string titleText = "暂停";
    public string resumeText = "继续游戏";
    public string restartLevelText = "重开本关";
    public string restartRunText = "整局重开";

    [Tooltip("打开菜单时是否冻结游戏（Time.timeScale = 0）")]
    public bool pauseTime = true;

    [Tooltip("主菜单场景名（在这个场景里不响应 ESC）")]
    public string mainMenuScene = "MainMenuScene";

    private static PauseMenu instance;
    private GameObject root;
    private bool open;

    public bool IsOpen { get { return open; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;

        var go = new GameObject("~PauseMenu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenu>();
    }

    void Update()
    {
        // 主菜单里不需要暂停菜单
        if (SceneManager.GetActiveScene().name == mainMenuScene) return;

        // 失败菜单已经打开时，不要再叠一个
        if (!open && Object.FindAnyObjectByType<FailMenu>() != null) return;

        if (!EscapePressed()) return;

        if (open) Close();
        else Open();
    }

    bool EscapePressed()
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

    void Open()
    {
        if (root == null) Build();

        root.SetActive(true);
        open = true;

        if (pauseTime) Time.timeScale = 0f;
    }

    void Close()
    {
        if (root != null) root.SetActive(false);
        open = false;

        if (pauseTime) Time.timeScale = 1f;
    }

    void RestartLevel()
    {
        // 重开本关：重新加载当前场景，天数 / 碎片 / 已吞噬都保留
        Time.timeScale = 1f;
        ScreenFade.LoadWithFade(SceneManager.GetActiveScene().name);
    }

    void RestartRun()
    {
        // 整局重开：清空所有进度，回到开始界面重新来
        Time.timeScale = 1f;
        DevourProgress.ResetAll();
        ScreenFade.LoadWithFade(mainMenuScene);
    }

    // ================= UI =================

    void Build()
    {
        FailMenu.EnsureEventSystem();     // 没有 EventSystem 的话按钮点不动

        root = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 半透明遮罩
        var dim = UImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);

        // 标题
        var title = UText(titleText, root.transform, 90, new Color(0.95f, 0.97f, 1f));
        Anchor(title.rectTransform, new Vector2(0f, 170f), new Vector2(1200f, 150f));

        // 三个按钮：继续 / 重开本关 / 整局重开
        MakeButton(resumeText, new Vector2(-380f, -80f), new Color(0.25f, 0.55f, 0.95f), Close);
        MakeButton(restartLevelText, new Vector2(0f, -80f), new Color(0.55f, 0.45f, 0.3f), RestartLevel);
        MakeButton(restartRunText, new Vector2(380f, -80f), new Color(0.45f, 0.35f, 0.38f), RestartRun);

        root.SetActive(false);
    }

    void MakeButton(string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
        go.transform.SetParent(root.transform, false);

        var img = go.GetComponent<Image>();
        img.color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360f, 120f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var txt = UText(label, go.transform, 46, Color.white);
        Stretch(txt.rectTransform);
    }

    Text UText(string content, Transform parent, int size, Color color)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = GetFont();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        return t;
    }

    Image UImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Anchor(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
