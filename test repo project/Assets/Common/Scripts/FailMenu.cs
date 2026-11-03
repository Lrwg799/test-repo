using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 失败画面：显示「Game Over」木牌插图，并把三条横线填上本次结算数据。
///
///   第一行：坚持了几天            → DevourProgress.Day
///   第二行：一共抓了多少只 NPC     → DevourProgress.TotalDevoured
///   第三行：跑酷一共拾到多少月亮    → DevourProgress.DayCoins
///
/// UI 是运行时用代码生成的，不依赖场景里预先摆好的物件，
/// 所以夜晚场景和跑酷场景都能直接用。
/// </summary>
public class FailMenu : MonoBehaviour
{
    [Header("插图")]
    [Tooltip("Game Over 木牌插图（放在 Resources 下则自动读取）")]
    public Sprite gameOverSprite;
    public string resourcesPath = "GameOver";

    [Header("按钮文字")]
    public string retryText = "重开本关";
    public string menuText = "整局重开";

    [Header("返回提示")]
    [Tooltip("多少秒后显示「点击返回主界面」")]
    public float hintDelay = 3f;
    [Tooltip("提示出现后，再过多少秒才允许点击")]
    public float clickDelay = 1f;
    public string hintMessage = "点击返回主界面";
    [Tooltip("提示文字的颜色（本身较浅，叠了深色描边）")]
    public Color hintColor = new Color(0.97f, 0.93f, 0.82f);
    [Range(0f, 1f)] public float hintAlphaMin = 0.55f;
    [Range(0f, 1f)] public float hintAlphaMax = 1f;

    [Header("木板呼吸")]
    [Range(0.5f, 1f)] public float boardDimMin = 0.86f;

    private Text hintText;
    private Image boardImage;
    private bool canClick;
    private bool returned;

    [Header("场景")]
    public string mainMenuScene = "MainMenuScene";

    // 三条横线在插图上的归一化位置（左下角为原点）
    // 这些值是对 GameOver.png（2048×1152）做连通域分析实测出来的：
    // 下划线 = 「宽≥45、高≤14、宽高比≥7」的扁长块，正好匹配到 3 个，不会误判。
    // 因为数字是木牌的子物体，锚点用的是「相对木牌」的比例，
    // 所以木牌怎么缩放、窗口怎么变，数字都会跟着那条线走。
    //   nights    : 中心像素 (1072.5, 643.0)  宽 148 高 7
    //   ghosts    : 中心像素 ( 959.5, 740.5)  宽 136 高 6
    //   moonlight : 中心像素 (1274.0, 838.0)  宽 137 高 7
    static readonly Vector2[] BlankAnchors = new Vector2[]
    {
        new Vector2(0.5237f, 0.4418f),   // You spent ___ nights
        new Vector2(0.4685f, 0.3572f),   // rallied ___ ghosts
        new Vector2(0.6221f, 0.2726f),   // touched the moonlight ___ times
    };

    private static FailMenu instance;

    public static void Show(string reason)
    {
        if (instance == null)
        {
            var go = new GameObject("~FailMenu");
            instance = go.AddComponent<FailMenu>();
        }
        instance.Build(reason);
    }

    void Build(string reason)
    {
        if (transform.childCount > 0) return;

        EnsureEventSystem();

        // ---- Canvas ----
        var canvasGO = new GameObject("FailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 背景遮罩 ----
        var dim = UImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.85f));
        Stretch(dim.rectTransform);

        // ---- Game Over 插图（等比铺满，居中）----
        var sp = gameOverSprite;
        if (sp == null) sp = Resources.Load<Sprite>(resourcesPath);

        if (sp != null)
        {
            var board = UImage("GameOver", canvasGO.transform, Color.white);
            board.sprite = sp;
            board.preserveAspect = false;          // 图是 16:9，参考分辨率也是 16:9，铺满不会变形
            Stretch(board.rectTransform);          // 铺满整个屏幕
            var br = board.rectTransform;
            boardImage = board;

            // ---- 三个数字，压在三条横线上 ----
            int[] values = new int[]
            {
                Mathf.Max(0, DevourProgress.Day - 1),      // 坚持了几天
                Mathf.Max(0, DevourProgress.TotalDevoured), // 抓了多少鬼
                Mathf.Max(0, DevourProgress.DayCoins),      // 月亮碎片
            };

            // 数字要写在横线「上面」一点，不然会盖住那条线。
            // 用「抬高锚点比例」而不是像素偏移 —— 这样木牌缩放时偏移也跟着缩，
            // 而且不依赖 rect 是否已经布局完成。
            // 0.017 ≈ 半个字高 / 1080，正好让字底贴住横线。
            const float TextLift = 0.017f;

            for (int i = 0; i < 3; i++)
            {
                var num = UText(values[i].ToString(), br, 52, new Color(0.18f, 0.09f, 0.04f));
                num.fontStyle = FontStyle.Bold;                       // 加粗，更醒目
                var nr = num.rectTransform;

                var a = BlankAnchors[i];
                a.y += TextLift;                                      // 抬高到横线上方
                nr.anchorMin = nr.anchorMax = a;

                nr.pivot = new Vector2(0.5f, 0.5f);
                nr.anchoredPosition = Vector2.zero;
                nr.sizeDelta = new Vector2(260f, 90f);
            }
        }
        else
        {
            Debug.LogWarning("[FailMenu] 找不到 Game Over 插图，退回纯文字");
            var txt = UText("游戏失败", canvasGO.transform, 96, new Color(1f, 0.35f, 0.3f));
            var tr = txt.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0f, 170f);
            tr.sizeDelta = new Vector2(1200f, 160f);
        }

        // 不再放「重开本关 / 整局重开」按钮 —— 失败画面只做结算展示

        StartCoroutine(HintRoutine(canvasGO.transform, boardImage));
    }

    /// <summary>3 秒后淡入「点击返回主界面」，再过 1 秒才允许点击返回</summary>
    System.Collections.IEnumerator HintRoutine(Transform canvas, Image board)
    {
        hintText = null;
        canClick = false;

        // ---- 等 3 秒（用 realtime，暂停时也照常走）----
        float t = 0f;
        while (t < hintDelay) { t += Time.unscaledDeltaTime; yield return null; }

        // ---- 提示文字 ----
        var hint = UText(hintMessage, canvas, 44, hintColor);
        hint.fontStyle = FontStyle.Bold;

        // 加个深色描边，浅色字压在木牌上也能看清
        var outline = hint.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.07f, 0.03f, 0.9f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        var hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 48f);
        hr.sizeDelta = new Vector2(900f, 70f);

        hintText = hint;

        // ---- 再过 1 秒才允许点击 ----
        t = 0f;
        while (t < clickDelay) { t += Time.unscaledDeltaTime; yield return null; }
        canClick = true;
    }

    void Update()
    {
        float wave = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;   // 0~1 缓慢起伏

        // 文字：颜色本身浅，再叠一层深浅变化
        if (hintText != null)
        {
            var c = hintColor;
            c.a = Mathf.Lerp(hintAlphaMin, hintAlphaMax, wave);
            hintText.color = c;
        }

        // 木板：跟着做同样的呼吸，让"可以点了"这件事更明显
        if (boardImage != null)
        {
            float b = Mathf.Lerp(boardDimMin, 1f, wave);
            boardImage.color = new Color(b, b, b, 1f);
        }

        // ---- 可点击后，点任意位置回主界面 ----
        if (!canClick) return;

        bool clicked = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) clicked = true;
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) clicked = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0)) clicked = true;
#endif
        if (clicked) ReturnToMenu();
    }

    void ReturnToMenu()
    {
        if (returned) return;
        returned = true;
        canClick = false;

        Time.timeScale = 1f;
        DevourProgress.ResetAll();

        if (ScreenFade.Instance != null) ScreenFade.LoadWithFade(mainMenuScene);
        else SceneManager.LoadScene(mainMenuScene);
    }

    void MakeButton(Transform canvas, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
        go.transform.SetParent(canvas, false);

        var img = go.GetComponent<Image>();
        img.color = color;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420f, 110f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var txt = UText(label, go.transform, 44, Color.white);
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

    static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    internal static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void OnRetry()
    {
        Time.timeScale = 1f;
        ScreenFade.LoadWithFade(SceneManager.GetActiveScene().name);
    }

    void OnBackToMenu()
    {
        Time.timeScale = 1f;
        DevourProgress.ResetAll();
        ScreenFade.LoadWithFade(mainMenuScene);
    }
}
