using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 屏幕淡入淡出过渡（黑夜 ↔ 白天切换用）。
///
/// 用法：
///   ScreenFade.LoadWithFade("SampleScene");     // 淡出到全黑 → 切场景 → 新场景淡入
///
/// 它会自己挂在 DontDestroyOnLoad 上，跨场景常驻，
/// 所以每个场景只需要放一个（多余的会自动销毁）。
/// </summary>
public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance;

    [Header("时长")]
    [Tooltip("变黑要多久")]
    public float fadeOutDuration = 0.8f;

    [Tooltip("切场景后从黑变亮要多久")]
    public float fadeInDuration = 0.8f;

    [Tooltip("全黑后停多久再切场景")]
    public float holdBlack = 0.2f;

    [Header("外观")]
    public Color fadeColor = Color.black;

    [Header("场景加载时自动淡入")]
    public bool autoFadeInOnlyFirstScene = true;

    [Tooltip("切换时在 Console 打日志，方便排查")]
    public bool logTransitions = true;

    private Image image;
    private bool transitioning;

    /// <summary>是否正在做切换过渡</summary>
    public bool IsTransitioning { get { return transitioning; } }

    // ================= 初始化 =================
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);      // 场景里重复的，交给常驻那个
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Build();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    /// <summary>保证场景里有一个 ScreenFade</summary>
    public static ScreenFade Ensure()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("ScreenFade");
        return go.AddComponent<ScreenFade>();
    }

    /// <summary>淡出到全黑 → 切换场景（切完后自动淡入）</summary>
    public static void LoadWithFade(string sceneName, float fadeOut = -1f)
    {
        var f = Ensure();
        f.StartFadeOutAndLoad(sceneName, fadeOut > 0f ? fadeOut : f.fadeOutDuration);
    }

    // ================= 内部 =================
    void Build()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        Transform child = transform.Find("FadeImage");
        GameObject imgGo;

        if (child == null)
        {
            imgGo = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(transform, false);
        }
        else imgGo = child.gameObject;

        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = imgGo.GetComponent<Image>();
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoFadeInOnlyFirstScene) return;

        transitioning = false;
        StopAllCoroutines();
        StartCoroutine(FadeInRoutine());
    }

    public void StartFadeOutAndLoad(string sceneName, float duration)
    {
        if (transitioning) return;

        transitioning = true;
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(sceneName, Mathf.Max(0.01f, duration)));
    }

    IEnumerator FadeOutRoutine(string sceneName, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Clamp01(t / duration));
            yield return null;
        }

        SetAlpha(1f);
        yield return new WaitForSecondsRealtime(holdBlack);

        if (logTransitions) Debug.Log("[ScreenFade] 淡出完成 → 加载场景「" + sceneName + "」");
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FadeInRoutine()
    {
        SetAlpha(1f);
        yield return null;      // 先给新场景一帧，避免闪一下

        float t = 0f;
        float d = Mathf.Max(0.01f, fadeInDuration);

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / d));
            yield return null;
        }

        SetAlpha(0f);
    }

    void SetAlpha(float a)
    {
        if (image == null) return;

        var c = fadeColor;
        c.a = a;
        image.color = c;
    }
}
