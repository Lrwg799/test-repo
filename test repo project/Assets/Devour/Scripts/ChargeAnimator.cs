using UnityEngine;

/// <summary>
/// 蓄力动画播放（Animator 版）。
///
/// 用「蓄力进度」驱动 AnimationClip 的归一化时间：
///   进度 0%   → 动画第 0 帧
///   进度 100% → 动画最后一帧
/// 动画本身设 speed = 0，不让它自己跑，完全由进度控制。
///
/// 蓄力期间会隐藏玩家自己的立绘，只保留这条动画，避免两层贴图叠在一起。
/// </summary>
public class ChargeAnimator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("留空会自动找场景里的 ChargeController")]
    public ChargeController charge;

    [Tooltip("留空会自动取本物体上的 Animator")]
    public Animator animator;

    [Tooltip("蓄力期间要隐藏的玩家立绘（通常是 GhostVisual 的 SpriteRenderer）")]
    public SpriteRenderer playerSprite;

    [Tooltip("承载蓄力动画的 SpriteRenderer")]
    public SpriteRenderer fxRenderer;

    [Header("动画")]
    [Tooltip("AnimatorController 里的状态名")]
    public string stateName = "Charge";

    [Header("表现")]
    [Tooltip("蓄力期间隐藏玩家立绘（推荐，避免叠图）")]
    public bool hidePlayerWhileCharging = true;

    private int stateHash;
    private bool lastCharging;

    void Awake()
    {
        if (charge == null) charge = FindAnyObjectByType<ChargeController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (fxRenderer == null) fxRenderer = GetComponent<SpriteRenderer>();

        stateHash = Animator.StringToHash(stateName);

        ApplyState(false);
    }

    void Update()
    {
        if (charge == null || animator == null) return;

        bool charging = charge.IsCharging;

        if (charging != lastCharging)
        {
            lastCharging = charging;
            ApplyState(charging);
        }

        if (!charging) return;

        // 完全由进度驱动：speed 设 0，然后每帧把归一化时间设到对应位置
        animator.speed = 0f;
        animator.Play(stateHash, 0, Mathf.Clamp01(charge.Progress01));
    }

    void ApplyState(bool charging)
    {
        // 玩家立绘：蓄力时藏起来，不蓄力时露出来
        if (playerSprite != null)
        {
            playerSprite.enabled = !(charging && hidePlayerWhileCharging);
        }

        // 蓄力特效只在蓄力时出现
        if (fxRenderer != null) fxRenderer.enabled = charging;
        if (animator != null) animator.enabled = charging;

        if (charging && animator != null)
        {
            animator.speed = 0f;
            animator.Play(stateHash, 0, 0f);
        }
    }

    /// <summary>动画跑完一轮后（比如蓄力被取消）立刻复位到第 0 帧</summary>
    public void ResetToStart()
    {
        if (animator == null) return;
        animator.speed = 0f;
        animator.Play(stateHash, 0, 0f);
    }
}
