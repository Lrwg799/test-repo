using UnityEngine;

/// <summary>金币：上下浮动 + 旋转（用横向缩放模拟），碰到玩家后被收集。</summary>
public class CoinPickup : MonoBehaviour
{
    public int value = 1;
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 2.4f;
    public float spinSpeed = 3.2f;

    Vector3 basePosition;
    float time;
    bool taken;

    void Start()
    {
        basePosition = transform.position;
        time = Random.Range(0f, 10f);
    }

    void Update()
    {
        if (taken) return;
        time += Time.deltaTime;
        transform.position = basePosition + Vector3.up * (Mathf.Sin(time * bobSpeed) * bobAmplitude);
        float spin = Mathf.Cos(time * spinSpeed);
        transform.localScale = new Vector3(Mathf.Sign(spin) * Mathf.Lerp(0.25f, 1f, Mathf.Abs(spin)), 1f, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Collect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Collect(other);
    }

    void Collect(Collider2D other)
    {
        if (taken) return;
        if (other.GetComponent<PlayerController2D>() == null) return;

        taken = true;
        if (GameManager.Instance != null) GameManager.Instance.AddCoin(value);

        Fx.Burst(transform.position, new Color(1f, 0.9f, 0.35f), new Color(1f, 0.75f, 0.1f), 10, 4.5f, 0.11f, 0.35f);
        SfxPlayer.Play(SfxPlayer.Coin, 0.7f);

        gameObject.SetActive(false);
    }
}
