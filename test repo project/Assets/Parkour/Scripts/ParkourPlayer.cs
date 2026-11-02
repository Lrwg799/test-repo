using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 跑酷玩家：只上下动（重力 + 跳跃），横向不动 —— 视觉上的"往前跑"由地形滚动实现。
///
/// 地面检测用三根向下的射线（左/中/右），比单个射线更能站住边缘。
/// 掉到 killY 以下判定失败。
/// </summary>
public class ParkourPlayer : MonoBehaviour
{
    [Header("物理")]
    [Tooltip("重力加速度（越小越飘）")]
    public float gravity = 12f;

    [Tooltip("起跳初速度（配合小重力，得到「低而久」的漂浮跳）")]
    public float jumpSpeed = 8.6f;

    [Tooltip("最大下落速度（防止穿透薄地面）")]
    public float maxFallSpeed = 14f;

    [Header("地面检测")]
    [Tooltip("脚下多长距离内算落地")]
    public float groundCheckDistance = 0.18f;

    [Tooltip("哪些层算地面")]
    public LayerMask groundMask = ~0;

    [Tooltip("能直接迈上去的最大高度差。超过这个就算撞墙（斜坡不受影响，因为它是渐变升高）")]
    public float maxStepUp = 0.55f;

    [Tooltip("算作「墙」的最小方块高度。比这更薄的视为浮空平台，可以穿过它的侧面/底面")]
    public float minWallHeight = 1.2f;

    [Tooltip("失败时保留多少白天金币（0.5 = 摔了只保留一半，作为冒险的代价）")]
    [Range(0f, 1f)]
    public float coinKeepOnFail = 0.5f;

    [Header("失败判定")]
    [Tooltip("掉到这个高度以下判定失败")]
    public float killY = -12f;

    [Tooltip("失败后按什么键重来")]
    public KeyCode restartKey = KeyCode.R;

    [Header("引用")]
    [Tooltip("失败时用来停住地形滚动")]
    public ParkourRunner runner;

    [Header("事件")]
    public UnityEvent onJump;
    public UnityEvent onDeath;

    [Header("状态（只读）")]
    public bool isGrounded;
    public bool isDead;
    public float velocityY;

    private float halfH = 0.5f;
    private float halfW = 0.5f;

    void Start()
    {
        if (runner == null) runner = FindAnyObjectByType<ParkourRunner>();

        // 从贴图算半高半宽，供射线和落地对齐用
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            halfH = sr.sprite.bounds.size.y * transform.lossyScale.y * 0.5f;
            halfW = sr.sprite.bounds.size.x * transform.lossyScale.x * 0.5f;
        }

        // 开局打印一次跳跃能力，方便对照地形空隙宽度调参
        if (runner != null && gravity > 0f)
        {
            float air = 2f * jumpSpeed / gravity;
            float dist = air * runner.scrollSpeed;
            float h = jumpSpeed * jumpSpeed / (2f * gravity);
            Debug.Log("[Runner] 跳跃能力：高度 " + h.ToString("0.00")
                    + " / 滞空 " + air.ToString("0.00") + " 秒"
                    + " / 可跨 " + dist.ToString("0.00") + " 单位（滚动 " + runner.scrollSpeed + "）");
        }
    }

    void Update()
    {
        if (isDead)
        {
            if (RestartPressed())
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            return;
        }

        // ---- 起跳 ----
        if (isGrounded && JumpPressed())
        {
            velocityY = jumpSpeed;
            isGrounded = false;
            if (onJump != null) onJump.Invoke();
        }

        // ---- 卡帧保护：dt 过大时（编辑器失焦、加载卡顿）一帧就会穿透薄地面 ----
        float dt = Time.deltaTime;
        if (dt > 0.05f) dt = 0.05f;

        // ---- 重力 ----
        velocityY -= gravity * dt;
        if (velocityY < -maxFallSpeed) velocityY = -maxFallSpeed;

        transform.position += new Vector3(0f, velocityY * dt, 0f);

        // ---- 落地检测 ----
        CheckGround();

        // ---- 撞墙检测：身体已经插进地形里（撞上高台阶）----
        if (!isDead) CheckEmbedded();

        // ---- 掉落判定 ----
        if (!isDead && transform.position.y < killY) Die();
    }

    /// <summary>
    /// 身体是否已经陷进地形里。
    ///
    /// 落地射线只会往下打，看不到"比身体中心还高"的台阶，
    /// 于是角色会直接穿进高台里。这里用一次身体大小的重叠检测兜底：
    /// 只要碰到了顶面明显高于脚下的地面，就判定撞墙。
    /// </summary>
    void CheckEmbedded()
    {
        float feetY = transform.position.y - halfH;
        float h = halfH * 1.3f;
        if (h <= 0.01f) return;

        Vector2 center = new Vector2(transform.position.x, feetY + h * 0.5f + 0.06f);
        Vector2 size = new Vector2(Mathf.Max(0.05f, halfW * 1.1f), h);

        var hits = Physics2D.OverlapBoxAll(center, size, 0f, groundMask);

        // 先看有没有斜坡（旋转 45° 的方块）压在身上 ——
        // 站在坡上时，坡后那块平地会比脚底高出一截，不能当撞墙处理
        bool nearSlope = false;
        for (int i = 0; i < hits.Length; i++)
        {
            var b = hits[i] as BoxCollider2D;
            if (b == null) continue;

            if (Mathf.Abs(Mathf.DeltaAngle(0f, b.transform.eulerAngles.z)) > 5f)
            {
                nearSlope = true;
                break;
            }
        }

        float wallLimit = nearSlope ? 2.5f : maxStepUp;

        for (int i = 0; i < hits.Length; i++)
        {
            // 斜坡是旋转 45° 的方块，角色贴着坡面走必然会有轻微重叠 —— 不算撞墙
            var box = hits[i] as BoxCollider2D;
            if (box == null) continue;

            float tilt = Mathf.Abs(Mathf.DeltaAngle(0f, box.transform.eulerAngles.z));
            if (tilt > 5f) continue;

            // 悬空的薄板（浮空平台）连底面都在脚下以上 —— 从它下面跑过不算撞墙
            if (hits[i].bounds.min.y > feetY + 0.2f) continue;

            // 薄的方块一律当浮空平台处理：跳起来撞到它的侧面/底面也不该判失败
            if (hits[i].bounds.size.y < minWallHeight) continue;

            float rise = hits[i].bounds.max.y - feetY;
            if (rise > wallLimit)
            {
                DieAtWall(rise);
                return;
            }
        }
    }

    void CheckGround()
    {
        // 从身体中心往下打三根射线，长度 = 半高 + 检测距离
        float len = halfH + groundCheckDistance;
        float ox = transform.position.x;
        float oy = transform.position.y;

        float[] offsets = { -halfW * 0.6f, 0f, halfW * 0.6f };

        bool hitAny = false;
        float lowY = float.MaxValue;    // 最低的那块地面 = 玩家实际踩着的那块
        float highY = float.MinValue;   // 最高的那块 = 前方台阶顶
        Collider2D lowCol = null;

        for (int i = 0; i < offsets.Length; i++)
        {
            var hit = Physics2D.Raycast(new Vector2(ox + offsets[i], oy),
                                        Vector2.down, len, groundMask);

            if (hit.collider != null)
            {
                if (hit.point.y < lowY)
                {
                    lowY = hit.point.y;
                    lowCol = hit.collider;
                }

                if (hit.point.y > highY) highY = hit.point.y;
                hitAny = true;
            }
        }

        // 只有在下落或站着时才贴地；往上飞的时候不打断跳跃
        if (hitAny && velocityY <= 0f)
        {
            float feetY = transform.position.y - halfH;
            float rise = lowY - feetY;

            // 斜坡是旋转 45° 的方块，表面是连续上升的，永远应该能走上去
            bool onSlope = lowCol != null
                        && Mathf.Abs(Mathf.DeltaAngle(0f, lowCol.transform.eulerAngles.z)) > 5f;

            if (rise <= maxStepUp || (onSlope && rise <= 3f))
            {
                isGrounded = true;
                velocityY = 0f;
                transform.position = new Vector3(transform.position.x, lowY + halfH, transform.position.z);
            }
            else
            {
                // 脚下这块比玩家高太多（掉进台阶内部了）—— 不瞬移，交给 CheckEmbedded 判撞墙
                isGrounded = false;
            }
        }
        else
        {
            isGrounded = false;
        }
    }

    /// <summary>撞到抬高的台阶：算失败（正常玩法应该跳过去）</summary>
    void DieAtWall(float stepUp)
    {
        isDead = true;
        velocityY = 0f;
        isGrounded = false;

        if (runner != null) runner.Stop();

        LoseCoins();

        if (onDeath != null) onDeath.Invoke();

        Debug.Log("[Runner] 撞到了 " + stepUp.ToString("0.00")
                  + " 高的台阶（最大可迈 " + maxStepUp + "）—— 应该跳过去。按 "
                  + restartKey + " 重来");
    }

    void Die()
    {
        isDead = true;
        velocityY = 0f;
        isGrounded = false;

        if (runner != null) runner.Stop();

        LoseCoins();

        if (onDeath != null) onDeath.Invoke();

        Debug.Log("[Runner] 掉下去了 —— 游戏结束，按 " + restartKey + " 重来");
    }

    /// <summary>失败代价：只保留一部分白天收集的月亮碎片</summary>
    void LoseCoins()
    {
        if (coinKeepOnFail >= 0.999f) return;

        int before = DevourProgress.DayCoins;
        DevourProgress.DayCoins = Mathf.RoundToInt(before * Mathf.Clamp01(coinKeepOnFail));

        Debug.Log("[Runner] 失败代价：白天碎片 " + before + " → " + DevourProgress.DayCoins
                  + "（保留 " + Mathf.RoundToInt(coinKeepOnFail * 100f) + "%）");
    }

    bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.spaceKey.wasPressedThisFrame
            || kb.wKey.wasPressedThisFrame
            || kb.upArrowKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.W)
            || Input.GetKeyDown(KeyCode.UpArrow);
#else
        return false;
#endif
    }

    bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(restartKey);
#else
        return false;
#endif
    }
}
