using UnityEngine;

/// <summary>尖刺等危险物：碰到就死。碰撞体设为 Trigger。</summary>
public class Hazard : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        Kill(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Kill(other);
    }

    void Kill(Collider2D other)
    {
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null || player.IsDead) return;
        player.Die();
    }
}
