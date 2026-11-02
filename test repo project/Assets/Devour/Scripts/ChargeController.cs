using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 鼠标左键蓄力。
///
/// 规则：
///   1. 按住左键开始蓄力，进度从 0 往上走；
///   2. 蓄满 requiredTime 才算成功（触发 onChargeReady）；
///   3. 中途松手 → 进度清零，必须从头重新蓄力（触发 onChargeReset）。
///
/// 用法：挂在任意空 GameObject 上即可，不需要其它组件。
/// </summary>
public class ChargeController : MonoBehaviour
{
    [Header("蓄力设置")]
    [Tooltip("需要按住多久才算蓄力成功（秒）")]
    public float requiredTime = 1.5f;

    [Tooltip("勾上 = 蓄满那一刻立刻触发；不勾 = 蓄满后松手才触发")]
    public bool fireOnFull = false;

    [Header("可选")]
    [Tooltip("鼠标停在 UI 上时忽略蓄力（避免点按钮时也蓄力）")]
    public bool ignoreWhenOverUI = true;

    [Tooltip("是否允许蓄力。运镜/换场景等期间由外部临时关掉")]
    public bool interactable = true;

    [Tooltip("连续「检测不到按下」超过这个秒数才算真的松手。避免光标短暂移出 Game 视图时蓄力被清零")]
    public float releaseGrace = 0.15f;

    private float releaseTimer;

    [Header("事件（在 Inspector 里挂接，可不接）")]
    [Tooltip("开始蓄力时触发一次")]
    public UnityEvent onChargeStart;

    [Tooltip("蓄力进度 0~1，每帧变化时触发，可直接喂给进度条")]
    public UnityEvent<float> onChargeProgress;

    [Tooltip("刚好蓄满时触发一次")]
    public UnityEvent onChargeReady;

    [Tooltip("蓄满后真正生效（发射/挥砍等）时触发")]
    public UnityEvent onChargeFired;

    [Tooltip("没蓄满就松手、进度被清零时触发")]
    public UnityEvent onChargeReset;

    // ================= 运行时状态 =================
    private bool charging;   // 是否正在蓄力
    private bool full;       // 是否已蓄满
    private float timer;     // 已经蓄了多久

    /// <summary>蓄力进度 0~1（给 UI 进度条用）</summary>
    public float Progress01
    {
        get
        {
            if (full) return 1f;
            if (requiredTime <= 0f) return 0f;

            float p = timer / requiredTime;
            return p > 1f ? 1f : p;
        }
    }

    /// <summary>是否正在蓄力</summary>
    public bool IsCharging { get { return charging; } }

    /// <summary>是否已蓄满</summary>
    public bool IsFull { get { return full; } }

    // ================= 主循环 =================
    void Update()
    {
        // 外部禁止蓄力（NPC 还没走到位等）：不响应输入，并把正在蓄的力清掉
        if (!interactable)
        {
            if (charging) ResetCharge();
            return;
        }

        // 鼠标在 UI 上就别管（防止点按钮时误触发）
        if (ignoreWhenOverUI
            && EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // ---- 读取左键状态：新 / 旧输入系统都兼容 ----
        bool pressed = false;
        bool held = false;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pressed = mouse.leftButton.wasPressedThisFrame;
            held = mouse.leftButton.isPressed;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
#endif

        // ① 按下左键 → 开始（或重新开始）蓄力
        if (pressed)
        {
            StartCharge();
            return;
        }

        // 没在蓄力就什么都不做
        if (!charging) return;

        // ② 还按着 → 累加时间
        if (held)
        {
            releaseTimer = 0f;
            AdvanceCharge();
            return;
        }

        // ③ 这一帧没检测到按住 —— 不要立刻判"松手"
        //    光标短暂移出 Game 视图时，输入系统会有一两帧报未按下，
        //    直接归零会让玩家觉得"我明明没松手却重置了"。
        //    连续松开超过 releaseGrace 秒才算真的松手。
        releaseTimer += Time.unscaledDeltaTime;

        if (releaseTimer >= releaseGrace)
        {
            releaseTimer = 0f;
            ReleaseCharge();
        }
    }

    // ================= 各阶段 =================

    void StartCharge()
    {
        if (charging) return;

        charging = true;
        full = false;
        timer = 0f;

        ReportProgress(0f);
        if (onChargeStart != null) onChargeStart.Invoke();
    }

    void AdvanceCharge()
    {
        if (full) return;

        timer += Time.deltaTime;

        bool reached = (requiredTime <= 0f) || (timer >= requiredTime);

        if (reached)
        {
            if (requiredTime > 0f) timer = requiredTime;

            full = true;
            ReportProgress(1f);
            if (onChargeReady != null) onChargeReady.Invoke();

            // 蓄满即触发
            if (fireOnFull) Fire();
        }
        else
        {
            ReportProgress(Progress01);
        }
    }

    void ReleaseCharge()
    {
        if (full && !fireOnFull)
        {
            // 蓄满了才松手 → 成功触发
            Fire();
        }
        else
        {
            // 没到时间就松手 → 清零，必须重新蓄力
            ResetCharge();
        }
    }

    // 真正生效：把你要做的事挂到 onChargeFired 上
    void Fire()
    {
        charging = false;
        full = false;
        timer = 0f;

        ReportProgress(0f);
        if (onChargeFired != null) onChargeFired.Invoke();
    }

    // 失败：进度清零
    void ResetCharge()
    {
        // 蓄了有一会儿才被打断的话记一笔，方便排查"明明没松手却清零"
        if (timer > 0.25f)
        {
            Debug.Log("[Charge] 蓄力被清零（已蓄 " + timer.ToString("0.00")
                      + " / " + requiredTime + " 秒）");
        }

        charging = false;
        full = false;
        timer = 0f;

        ReportProgress(0f);
        if (onChargeReset != null) onChargeReset.Invoke();
    }

    void ReportProgress(float value)
    {
        if (onChargeProgress != null) onChargeProgress.Invoke(value);
    }

    /// <summary>外部强制中断蓄力（比如角色死亡、切场景）</summary>
    public void CancelCharge()
    {
        if (!charging) return;
        ResetCharge();
    }
}
