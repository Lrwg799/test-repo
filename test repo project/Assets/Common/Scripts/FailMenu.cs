using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 失败菜单：游戏失败时弹出，让玩家自己选「重来」还是「返回开始界面」。
///
/// UI 是运行时用代码生成的，不依赖场景里预先摆好的物件，
/// 所以夜晚场景和跑酷场景都能直接用。
/// </summary>
public class FailMenu : MonoBehaviour
{
    [Header("文字")]
    public string titleText = "游戏失败";
    public string retryText = "重开本关";
    public string menuText = "整局重开";

    [Header("场景")]
    [Tooltip("返回的开始界面场景名")]
    public string mainMenuScene = "MainMenuScene";

    private static FailMenu instance;

    /// <summary>弹出失败菜单</summary>
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
        // 已经建过就不重复建
        if (transform.childCount > 0) return;

        // 兜底：场景里没有 EventSystem 的话按钮点不动
        EnsureEventSystem();

        // ---- Canvas ----
        var canvasGO = new GameObject("FailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;                 // 盖在所有 HUD 之上

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 全屏半透明遮罩（同时挡住底下的点击）----
        var dim = UImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(dim.rectTransform);

        // ---- 标题 ----
        var title = UText(titleText, canvasGO.transform, 96, new Color(1f, 0.35f, 0.3f));
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0f, 170f);
        tr.sizeDelta = new Vector2(1200f, 160f);

        // ---- 失败原因 ----
        var sub = UText(reason, canvasGO.transform, 42, new Color(0.85f, 0.88f, 0.95f));
        var sr = sub.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0f, 60f);
        sr.sizeDelta = new Vector2(1200f, 80f);

        // ---- 两个按钮 ----
        MakeButton(canvasGO.transform, retryText, new Vector2(-220f, -110f), new Color(0.25f, 0.55f, 0.95f), OnRetry);
        MakeButton(canvasGO.transform, menuText, new Vector2(220f, -110f), new Color(0.35f, 0.38f, 0.45f), OnBackToMenu);
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

    static Font GetFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>场景里没有 EventSystem 就建一个，否则按钮点不动</summary>
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

    // ================= 按钮行为 =================

    void OnRetry()
    {
        // 重开本关：只重来这一次，天数 / 碎片 / 已吞噬都保留
        Time.timeScale = 1f;
        ScreenFade.LoadWithFade(SceneManager.GetActiveScene().name);
    }

    void OnBackToMenu()
    {
        // 整局重开：清空所有进度，回到开始界面
        Time.timeScale = 1f;
        DevourProgress.ResetAll();
        ScreenFade.LoadWithFade(mainMenuScene);
    }
}
