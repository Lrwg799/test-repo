using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 开始界面：一张整图铺满屏幕（等比裁切，不留黑边）+ 图上「Game Start」木牌可点击。
///
/// 点下去时木牌会压暗（按下反馈），松手后进游戏。
/// 点击区和压暗层都挂在「背景图」下面，锚点用图片自身的比例，
/// 所以不管窗口怎么变、图片怎么裁切，点到的永远是那块木牌。
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    [Header("插图")]
    public string resourcesPath = "MainMenu";
    public Sprite backgroundSprite;

    [Header("进入的场景")]
    public string gameScene = "SampleScene";

    [Header("Game Start 木牌在「图片内」的位置（归一化，左下角为原点）")]
    [Tooltip("由 measure_board.py 从 MainMenu.png 实测：bbox(567,569)-(893,792)")]
    public Rect boardRect = new Rect(0.555f, 0.142f, 0.352f, 0.208f);

    [Tooltip("按下时木牌区域的压暗程度")]
    [Range(0f, 0.8f)] public float pressDarken = 0.35f;

    [Header("铺满裁切")]
    [Tooltip("裁切时保证图片底部这个比例以上的内容留在画面里（木牌下沿约 0.12）")]
    [Range(0f, 0.5f)] public float keepBottom = 0.09f;

    private Image darkOverlay;
    private bool loading;

    void Start()
    {
        Build();
    }

    void Build()
    {
        // 清掉场景里原来的按钮（StartButton / QuitButton 之类）
        foreach (var btn in FindObjectsByType<Button>(FindObjectsInactive.Include))
            Destroy(btn.gameObject);

        // ---- Canvas ----
        var canvasGO = new GameObject("~MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 背景整图：铺满 + 等比裁切（cover）----
        var sp = backgroundSprite;
        if (sp == null) sp = Resources.Load<Sprite>(resourcesPath);

        Transform boardParent = canvasGO.transform;
        Image bgImage = null;
        Sprite bgSprite = sp;

        if (sp != null)
        {
            var bgGO = new GameObject("Background", typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);

            var bg = bgGO.GetComponent<Image>();
            bg.sprite = sp;
            bg.color = Color.white;
            bg.raycastTarget = false;

            // 尺寸和位置在 CoverRoutine 里自己算（不用 AspectRatioFitter，
            // 因为它布局时会强制居中，会把我们想要的向下偏移覆盖掉）
            var brt = bg.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);

            boardParent = brt;                // 木牌相关 UI 挂到图片下面
            bgImage = bg;
        }
        else
        {
            Debug.LogWarning("[MainMenuScreen] 找不到开始界面插图：" + resourcesPath);
        }

        // ---- 木牌压暗层（按下时显示）----
        darkOverlay = NewImage("BoardPress", boardParent, new Color(0f, 0f, 0f, 0f));
        AnchorTo(darkOverlay.rectTransform, boardRect);
        darkOverlay.raycastTarget = false;

        // ---- 木牌上的点击区 ----
        var hit = NewImage("GameStartHit", boardParent, new Color(1f, 1f, 1f, 0f));
        AnchorTo(hit.rectTransform, boardRect);

        var trigger = hit.gameObject.AddComponent<EventTrigger>();
        AddEvent(trigger, EventTriggerType.PointerDown, _ => SetDarken(true));
        AddEvent(trigger, EventTriggerType.PointerUp, _ => SetDarken(false));
        AddEvent(trigger, EventTriggerType.PointerExit, _ => SetDarken(false));
        AddEvent(trigger, EventTriggerType.PointerClick, _ => EnterGame());

        EnsureEventSystem();

        // 压暗层放到最后，确保盖在点击区上面也看得见
        darkOverlay.transform.SetAsLastSibling();

        // 图是接近 1:1 的，要铺满 16:9 就得上下裁掉一大截。
        // 居中裁会把「Game Start」木牌裁出画面，所以要往下偏一点。
        if (bgImage != null && bgSprite != null) StartCoroutine(CoverRoutine(bgImage, bgSprite));
    }

    /// <summary>
    /// 等比缩放到「完全覆盖父级」，多出来的部分裁掉（不留黑边，也不变形），
    /// 然后纵向偏移，保证图片底部 keepBottom 以上的内容都还在画面里。
    /// 等 2 帧是因为 CanvasScaler 要等布局才算出参考分辨率下的真实尺寸。
    /// </summary>
    System.Collections.IEnumerator CoverRoutine(Image bg, Sprite sp)
    {
        yield return null;
        yield return null;

        var rt = bg.rectTransform;
        var parent = rt.parent as RectTransform;
        if (parent == null) yield break;

        float cw = parent.rect.width;
        float ch = parent.rect.height;
        if (cw <= 1f || ch <= 1f) yield break;

        float iw = sp.rect.width;
        float ih = sp.rect.height;

        // cover：先按宽度铺满，不够高再按高度铺满
        float scale = Mathf.Max(cw / iw, ch / ih);
        float drawW = iw * scale;
        float drawH = ih * scale;

        rt.sizeDelta = new Vector2(drawW, drawH);

        // 让「可见区域的下边缘」落在图片高度的 keepBottom 位置
        //   图片下边缘 y = y0 - drawH/2，父级下边缘 = -ch/2
        //   ( -ch/2 - (y0 - drawH/2) ) / drawH = keepBottom
        float y0 = drawH * 0.5f - ch * 0.5f - keepBottom * drawH;
        rt.anchoredPosition = new Vector2(0f, y0);
    }

    void AddEvent(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(action));
        trigger.triggers.Add(entry);
    }

    void SetDarken(bool on)
    {
        if (darkOverlay == null) return;
        darkOverlay.color = new Color(0f, 0f, 0f, on ? pressDarken : 0f);
    }

    void EnterGame()
    {
        if (loading) return;
        loading = true;
        StartCoroutine(GoRoutine());
    }

    System.Collections.IEnumerator GoRoutine()
    {
        yield return new WaitForSecondsRealtime(0.12f);

        if (ScreenFade.Instance != null) ScreenFade.LoadWithFade(gameScene);
        else SceneManager.LoadScene(gameScene);
    }

    // ---------------- 小工具 ----------------

    Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>把 Rect 当作「相对父级」的归一化区域来定位</summary>
    static void AnchorTo(RectTransform rt, Rect r)
    {
        rt.anchorMin = new Vector2(r.xMin, r.yMin);
        rt.anchorMax = new Vector2(r.xMax, r.yMax);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem",
            typeof(EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }
}
