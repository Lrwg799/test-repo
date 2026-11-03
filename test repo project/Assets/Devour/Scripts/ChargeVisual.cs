using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 蓄力动画显示。
///
/// 把 ChargeController 的进度（0~1）映射到一组蓄力图片上：
///   进度 0%   → 第 1 张
///   进度 50%  → 中间那张
///   进度 100% → 最后一张
///
/// 图片两种给法（任选其一）：
///   A. 直接把图按顺序拖进 Frames 数组（推荐，顺序你自己定）
///   B. 把图丢进 Assets/Resources/ChargeFrames/ 文件夹，自动按文件名排序读取
///      —— 这样以后"导入图片"就是拖进文件夹，不用改任何东西
///
/// 对齐：各帧尺寸不一致时，默认逐帧保持「底边」不动，避免换帧上下跳动。
/// 没放图时不会报错，只在 Console 提示一次。
/// </summary>
public class ChargeVisual : MonoBehaviour
{
    [Header("数据源")]
    [Tooltip("留空会自动在场景里找 ChargeController")]
    public ChargeController charge;

    [Header("蓄力图片（按 0% → 100% 的顺序）")]
    [Tooltip("按顺序拖进来；留空则自动从 Resources 读")]
    public Sprite[] frames;

    [Header("显示在哪里（二选一即可）")]
    [Tooltip("2D 世界空间：挂个 SpriteRenderer 填这里")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("UI：挂个 Image 填这里")]
    public Image image;

    [Header("自动读图")]
    [Tooltip("Frames 为空时，自动从 Resources 里读")]
    public bool autoLoadFromResources = true;

    [Tooltip("Resources 下的子文件夹名（不要写 Assets/ 和 Resources/）")]
    public string resourcesFolder = "ChargeFrames";

    [Header("对齐")]
    [Tooltip("逐帧保持底边对齐，避免各帧尺寸不同导致上下跳动")]
    public bool alignBottom = true;

    [Header("表现")]
    [Tooltip("不在蓄力时隐藏自己")]
    public bool hideWhenNotCharging = true;

    // ================= 内部状态 =================
    private Sprite[] runtimeFrames;   // 实际用哪组图
    private bool warnedNoFrames;

    private Sprite idleSprite;        // 进入蓄力前的常态贴图
    private bool idleCaptured;

    private Vector3 baseLocalPos;     // 原始 localPosition
    private float baseSizeY;          // 第 0 帧的高度（底边对齐的基准）
    private float basePivotY = 0.5f;  // 归一化的 pivot.y

    void Awake()
    {
        // 没手动指定就自动找
        if (charge == null)
        {
            charge = FindAnyObjectByType<ChargeController>();
        }

        runtimeFrames = frames;

        // Frames 没配 → 尝试从 Resources 自动读
        if ((runtimeFrames == null || runtimeFrames.Length == 0) && autoLoadFromResources)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcesFolder);

            if (loaded != null && loaded.Length > 0)
            {
                System.Array.Sort(loaded,
                    (a, b) => string.CompareOrdinal(a.name, b.name));

                runtimeFrames = loaded;
                Debug.Log("[ChargeVisual] 从 Resources/" + resourcesFolder
                          + " 读到 " + loaded.Length + " 张蓄力图。");
            }
        }

        if (spriteRenderer == null && image == null)
        {
            Debug.LogWarning("[ChargeVisual] SpriteRenderer 和 Image 都没填，画不出来。");
        }

        // 记录基准（底边对齐用）
        baseLocalPos = transform.localPosition;

        if (runtimeFrames != null && runtimeFrames.Length > 0 && runtimeFrames[0] != null)
        {
            baseSizeY = runtimeFrames[0].bounds.size.y;
            if (runtimeFrames[0].rect.height > 0f)
            {
                basePivotY = runtimeFrames[0].pivot.y / runtimeFrames[0].rect.height;
            }
        }

        if (hideWhenNotCharging)
        {
            SetVisible(false);
        }
    }

    void Update()
    {
        if (charge == null) return;

        if (!charge.IsCharging)
        {
            // 关键：不蓄力时要把贴图还原成常态图，
            // 否则中途取消蓄力会一直卡在最后一张蓄力图上
            RestoreIdle();

            if (hideWhenNotCharging)
            {
                SetVisible(false);
            }
            return;
        }

        SetVisible(true);
        CaptureIdle();
        ApplyProgress(charge.Progress01);
    }

    /// <summary>第一次要换图之前，先把当时的常态贴图记下来</summary>
    void CaptureIdle()
    {
        if (idleCaptured) return;
        if (spriteRenderer == null) return;

        idleSprite = spriteRenderer.sprite;
        idleCaptured = true;
    }

    /// <summary>还原成进入蓄力前的常态贴图</summary>
    void RestoreIdle()
    {
        if (!idleCaptured) return;

        if (spriteRenderer != null) spriteRenderer.sprite = idleSprite;
        if (image != null) image.sprite = idleSprite;
    }

    void ApplyProgress(float progress)
    {
        if (runtimeFrames == null || runtimeFrames.Length == 0)
        {
            if (!warnedNoFrames)
            {
                warnedNoFrames = true;
                Debug.LogWarning("[ChargeVisual] 还没有蓄力图片。把图拖进 Frames，"
                                 + "或放进 Assets/Resources/" + resourcesFolder + "/ 里。");
            }
            return;
        }

        // 例如 8 张图：0~12.5% 第1张 …… 100% 最后一张
        int index = Mathf.FloorToInt(progress * runtimeFrames.Length);
        if (index < 0) index = 0;
        if (index >= runtimeFrames.Length) index = runtimeFrames.Length - 1;

        Sprite current = runtimeFrames[index];
        if (current == null) return;

        if (spriteRenderer != null) spriteRenderer.sprite = current;
        if (image != null) image.sprite = current;

        // ---- 底边对齐：各帧高度不同，挪 localPosition 让"脚底"始终在同一高度 ----
        if (alignBottom && baseSizeY > 0f)
        {
            float deltaY = basePivotY * (current.bounds.size.y - baseSizeY) * transform.localScale.y;
            transform.localPosition = baseLocalPos + new Vector3(0f, deltaY, 0f);
        }
    }

    void SetVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
        if (image != null) image.enabled = visible;
    }
}
