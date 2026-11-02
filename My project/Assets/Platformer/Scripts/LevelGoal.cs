using UnityEngine;

/// <summary>终点旗：玩家进入触发器即通关。</summary>
public class LevelGoal : MonoBehaviour
{
    public Transform flagVisual;
    public float waveSpeed = 3f;
    public float waveAmount = 0.12f;
    float time;
    bool triggered;

    void Update()
    {
        if (flagVisual == null) return;
        time += Time.deltaTime;
        flagVisual.localScale = new Vector3(1f + Mathf.Sin(time * waveSpeed) * waveAmount, 1f, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (other.GetComponent<PlayerController2D>() == null) return;

        triggered = true;
        Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.45f, 0.45f), new Color(1f, 0.9f, 0.4f), 24, 7f, 0.16f, 0.7f);
        if (GameManager.Instance != null) GameManager.Instance.CompleteLevel();
    }
}
