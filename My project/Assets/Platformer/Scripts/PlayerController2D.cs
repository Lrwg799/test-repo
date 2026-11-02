using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 平台跳跃角色控制器：加速/减速、可变跳跃高度、郊狼时间、跳跃缓冲、
/// 单向平台下落、跳跃拉伸与落地挤压、死亡/重生。
/// 移动使用 Rigidbody2D 速度驱动，手感参数全部可以在 Inspector 里调。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("引用")]
    public Transform visual;
    public SpriteRenderer spriteRenderer;
    public float visualScale = 1f;

    [Header("跑动")]
    public float moveSpeed = 6.5f;
    public float groundAcceleration = 95f;
    public float groundDeceleration = 130f;
    public float airAcceleration = 60f;
    public float airDeceleration = 35f;

    [Header("跳跃")]
    public float jumpHeight = 3.2f;
    public float gravityScale = 3f;
    public float maxFallSpeed = 24f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    [Range(0f, 4f)] public float jumpCutGravity = 1.9f;

    [Header("地面检测")]
    public LayerMask groundMask = 1;

    [Header("其它")]
    public float stompBounceHeight = 3.6f;
    public float killY = -9f;

    Rigidbody2D rb;
    Collider2D col;
    readonly List<Collider2D> ignoredOneWays = new List<Collider2D>();

    bool grounded;
    bool wasGrounded;
    float coyoteCounter;
    float bufferCounter;
    int facing = 1;
    float stretch = 1f;
    float stretchTarget = 1f;
    bool controlLocked;
    bool dead;
    bool dropping;
    float lastFallSpeed;

    public bool IsGrounded { get { return grounded; } }
    public bool IsDead { get { return dead; } }
    public bool IsFalling { get { return rb != null && rb.linearVelocity.y < 0.5f; } }
    public Rigidbody2D Body { get { return rb; } }
    public int Facing { get { return facing; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (dead) return;

        float dt = Time.deltaTime;

        if (transform.position.y < killY)
        {
            Die();
            return;
        }

        lastFallSpeed = rb.linearVelocity.y;

        wasGrounded = grounded;
        grounded = CheckGrounded();

        if (grounded) coyoteCounter = coyoteTime;
        else coyoteCounter -= dt;

        if (!wasGrounded && grounded && lastFallSpeed < -9f)
        {
            stretchTarget = 0.72f;
            Fx.Dust(transform.position + Vector3.down * 0.45f, 6);
            SfxPlayer.Play(SfxPlayer.Land, 0.35f);
        }

        float input = controlLocked ? 0f : GameInput.Horizontal;
        bool jumpDown = !controlLocked && GameInput.JumpPressed;
        bool jumpHeld = !controlLocked && GameInput.JumpHeld;
        bool downHeld = !controlLocked && GameInput.DownHeld;

        if (input > 0.01f) facing = 1;
        else if (input < -0.01f) facing = -1;

        if (jumpDown) bufferCounter = jumpBufferTime;
        else bufferCounter -= dt;

        if (jumpDown && downHeld && grounded && !dropping)
            StartCoroutine(DropThroughRoutine());

        Move(input, dt);

        if (bufferCounter > 0f && coyoteCounter > 0f)
            Jump();

        // 松开跳跃键时的额外向下重力 = 可变跳跃高度
        if (!jumpHeld && rb.linearVelocity.y > 0.1f)
            rb.linearVelocity += Vector2.up * (-Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale * jumpCutGravity * dt);

        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);

        UpdateStretch(dt);

        if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
    }

    void Move(float input, float dt)
    {
        float target = input * moveSpeed;
        float rate;
        if (Mathf.Abs(target) > 0.01f) rate = grounded ? groundAcceleration : airAcceleration;
        else rate = grounded ? groundDeceleration : airDeceleration;

        float vx = Mathf.MoveTowards(rb.linearVelocity.x, target, rate * dt);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    bool CheckGrounded()
    {
        if (rb.linearVelocity.y > 0.6f) return false;

        Bounds b = col.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y + 0.06f);
        Vector2 size = new Vector2(b.size.x * 0.9f, 0.08f);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.1f, groundMask);
        if (hit.collider == null) return false;
        return hit.normal.y > 0.5f;
    }

    void Jump()
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity < 0.01f) gravity = 9.81f;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Sqrt(2f * gravity * jumpHeight));
        grounded = false;
        coyoteCounter = 0f;
        bufferCounter = 0f;
        stretchTarget = 1.28f;

        SfxPlayer.Play(SfxPlayer.Jump, 0.55f);
        Fx.Dust(transform.position + Vector3.down * 0.45f, 5);
    }

    /// <summary>踩到敌人后的小跳。</summary>
    public void Bounce(float height)
    {
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Sqrt(2f * gravity * Mathf.Max(1f, height)));
        grounded = false;
        stretchTarget = 1.25f;
        SfxPlayer.Play(SfxPlayer.Stomp, 0.7f);
    }

    IEnumerator DropThroughRoutine()
    {
        dropping = true;
        coyoteCounter = 0f;
        bufferCounter = 0f;

        Bounds b = col.bounds;
        Vector2 point = new Vector2(b.center.x, b.min.y - 0.02f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(point, new Vector2(b.size.x * 0.85f, 0.14f), 0f, groundMask);
        ignoredOneWays.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null || hits[i] == col) continue;
            if (hits[i].GetComponent<PlatformEffector2D>() == null) continue;
            Physics2D.IgnoreCollision(col, hits[i], true);
            ignoredOneWays.Add(hits[i]);
        }

        Vector2 startPos = rb.position;
        float timer = 0f;
        while (timer < 0.32f)
        {
            timer += Time.deltaTime;
            yield return null;
            if (dead) break;
        }

        RestoreOneWayCollisions();
        // 如果几乎没移动（例如被卡住），轻轻往下推一把
        if (!dead && Mathf.Abs(rb.position.y - startPos.y) < 0.25f)
            rb.position = startPos + Vector2.down * 0.3f;

        dropping = false;
    }

    void RestoreOneWayCollisions()
    {
        for (int i = 0; i < ignoredOneWays.Count; i++)
        {
            if (ignoredOneWays[i] != null && col != null)
                Physics2D.IgnoreCollision(col, ignoredOneWays[i], false);
        }
        ignoredOneWays.Clear();
    }

    void UpdateStretch(float dt)
    {
        stretchTarget = Mathf.Lerp(stretchTarget, 1f, 1f - Mathf.Exp(-6f * dt));
        stretch = Mathf.Lerp(stretch, stretchTarget, 1f - Mathf.Exp(-20f * dt));

        if (visual != null)
        {
            float sx = Mathf.Clamp(1f / Mathf.Max(0.5f, stretch), 0.7f, 1.35f);
            visual.localScale = new Vector3(sx * visualScale, stretch * visualScale, 1f);
        }
    }

    /// <summary>碰到陷阱 / 掉出地图 / 被敌人撞到。</summary>
    public void Die()
    {
        if (dead) return;
        dead = true;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        Fx.Burst(transform.position, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.4f, 0.5f), 18, 7f, 0.16f, 0.5f);
        SfxPlayer.Play(SfxPlayer.Hurt, 0.8f);
        if (GameManager.Instance != null) GameManager.Instance.ShakeCamera(0.2f, 0.3f);

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (GameManager.Instance != null) GameManager.Instance.RespawnPlayer();
    }

    public void Respawn(Vector3 position)
    {
        StopAllCoroutines();
        RestoreOneWayCollisions();

        dead = false;
        dropping = false;
        controlLocked = false;
        grounded = false;
        coyoteCounter = 0f;
        bufferCounter = 0f;
        stretch = 1f;
        stretchTarget = 1f;
        if (visual != null) visual.localScale = new Vector3(visualScale, visualScale, 1f);

        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
        transform.position = position;

        if (spriteRenderer != null) spriteRenderer.enabled = true;

        StartCoroutine(BlinkRoutine(0.9f));
    }

    IEnumerator BlinkRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (spriteRenderer != null) spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.08f);
            timer += 0.08f;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    /// <summary>通关后锁住操作。</summary>
    public void LockInput(bool value)
    {
        controlLocked = value;
        if (value) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void FreezeMotion()
    {
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
    }

    void OnDrawGizmosSelected()
    {
        if (col == null) return;
        Bounds b = col.bounds;
        Gizmos.color = grounded ? new Color(0.2f, 1f, 0.3f, 0.8f) : new Color(1f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - 0.04f, 0f), new Vector3(b.size.x * 0.9f, 0.08f, 0.1f));
    }
}
