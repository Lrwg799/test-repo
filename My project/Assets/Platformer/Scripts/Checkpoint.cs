using UnityEngine;

/// <summary>检查点旗子：碰到后点亮，并把重生点设到这里。</summary>
public class Checkpoint : MonoBehaviour
{
    public Color inactiveColor = new Color(0.72f, 0.76f, 0.82f, 1f);
    public Color activeColor = new Color(0.35f, 0.95f, 0.5f, 1f);
    public Transform flag;
    public SpriteRenderer flagRenderer;
    public Vector3 respawnOffset = new Vector3(0f, 1.1f, 0f);

    bool activated;

    void Start()
    {
        if (flagRenderer != null) flagRenderer.color = inactiveColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController2D player = other.GetComponent<PlayerController2D>();
        if (player == null || activated) return;
        Activate();
    }

    public void Activate()
    {
        activated = true;

        if (flagRenderer != null) flagRenderer.color = activeColor;
        if (flag != null) flag.localScale = new Vector3(1.25f, 1.25f, 1f);

        if (GameManager.Instance != null)
            GameManager.Instance.SetCheckpoint(transform.position + respawnOffset);

        Fx.Burst(transform.position + Vector3.up, activeColor, Color.white, 12, 3.5f, 0.12f, 0.4f);
        SfxPlayer.Play(SfxPlayer.Checkpoint, 0.7f);
    }
}
