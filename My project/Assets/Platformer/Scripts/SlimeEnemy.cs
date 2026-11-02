using System.Collections;
using UnityEngine;

/// <summary>
/// 史莱姆：在出生点左右巡逻；从上方踩到会被踩扁并给玩家一个弹跳，
/// 从侧面/下方碰到则玩家死亡。
/// </summary>
public class SlimeEnemy : MonoBehaviour
{
    public float speed = 2.1f;
    public float patrolDistance = 3f;
    public LayerMask groundMask = 1;
    public int scoreValue = 0;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Collider2D col;
    Vector2 startPosition;
    int direction = 1;
    bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void Start()
    {
        startPosition = transform.position;
        if (rb != null) rb.freezeRotation = true;
    }

    void FixedUpdate()
    {
        if (dead || rb == null) return;

        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

        float offset = transform.position.x - startPosition.x;
        if (offset > patrolDistance) direction = -1;
        else if (offset < -patrolDistance) direction = 1;

        // 撞墙就掉头
        Bounds b = col.bounds;
        Vector2 wallCheck = new Vector2(direction > 0 ? b.max.x + 0.06f : b.min.x - 0.06f, b.center.y);
        RaycastHit2D wall = Physics2D.Raycast(wallCheck, new Vector2(direction, 0f), 0.12f, groundMask);
        if (wall.collider != null && wall.collider.gameObject != gameObject) direction = -direction;

        if (spriteRenderer != null) spriteRenderer.flipX = direction < 0;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (dead) return;

        PlayerController2D player = collision.collider.GetComponent<PlayerController2D>();
        if (player == null || player.IsDead) return;

        bool stomping = player.IsFalling && player.transform.position.y > transform.position.y + 0.35f;

        if (stomping)
        {
            player.Bounce(3.6f);
            Squash();
        }
        else
        {
            player.Die();
        }
    }

    public void Squash()
    {
        if (dead) return;
        dead = true;

        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Fx.Burst(transform.position, new Color(1f, 0.45f, 0.62f), new Color(0.85f, 0.25f, 0.5f), 14, 5f, 0.13f, 0.45f);
        if (GameManager.Instance != null) GameManager.Instance.ShakeCamera(0.08f, 0.15f);

        StartCoroutine(SquashRoutine());
    }

    IEnumerator SquashRoutine()
    {
        Vector3 startScale = transform.localScale;
        float timer = 0f;
        while (timer < 0.12f)
        {
            timer += Time.deltaTime;
            float k = Mathf.Clamp01(timer / 0.12f);
            transform.localScale = new Vector3(startScale.x * (1f + k * 0.4f), startScale.y * (1f - k * 0.8f), 1f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
