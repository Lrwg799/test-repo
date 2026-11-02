using UnityEngine;

/// <summary>
/// 往返移动平台（Kinematic Rigidbody2D + MovePosition）。
/// 玩家站在上面时会被吸附为子物体，跟着平台一起走。
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    public Vector2 offset = new Vector2(4f, 0f);
    public float speed = 0.35f;
    public float carryCheckHeight = 0.45f;

    Rigidbody2D rb;
    Vector2 startPosition;
    float timer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;
    }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;
        float t = Mathf.PingPong(timer * speed, 1f);
        Vector2 target = startPosition + offset * t;

        if (rb != null) rb.MovePosition(target);
        else transform.position = target;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerController2D player = collision.collider.GetComponent<PlayerController2D>();
        if (player == null || player.IsDead) return;

        Bounds platformBounds = GetComponent<Collider2D>().bounds;
        Bounds playerBounds = collision.collider.bounds;

        if (playerBounds.min.y >= platformBounds.max.y - 0.15f)
            player.transform.SetParent(transform, true);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        PlayerController2D player = collision.collider.GetComponent<PlayerController2D>();
        if (player == null) return;
        if (player.transform.parent == transform) player.transform.SetParent(null, true);
    }
}
