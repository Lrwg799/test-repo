using UnityEngine;

/// <summary>
/// 程序化音效：运行时合成短音，不需要任何音频资源文件。
/// </summary>
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    public static AudioClip Coin;
    public static AudioClip Jump;
    public static AudioClip Hurt;
    public static AudioClip Land;
    public static AudioClip Stomp;
    public static AudioClip Checkpoint;
    public static AudioClip Win;

    AudioSource source;

    void Awake()
    {
        Instance = this;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        if (Coin == null) Coin = Notes("sfx_coin", new float[] { 1046.5f, 1568f }, 0.055f, 0.22f);
        if (Jump == null) Jump = Sweep("sfx_jump", 0.12f, 320f, 780f, 0.18f);
        if (Hurt == null) Hurt = Sweep("sfx_hurt", 0.26f, 520f, 110f, 0.28f, true);
        if (Land == null) Land = Thud("sfx_land", 0.07f, 0.16f);
        if (Stomp == null) Stomp = Thud("sfx_stomp", 0.09f, 0.22f);
        if (Checkpoint == null) Checkpoint = Notes("sfx_checkpoint", new float[] { 659f, 784f, 988f }, 0.07f, 0.18f);
        if (Win == null) Win = Notes("sfx_win", new float[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.13f, 0.2f);
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (Instance == null || clip == null || Instance.source == null) return;
        Instance.source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    static AudioClip Build(string name, float duration, System.Func<float, float> sample)
    {
        const int rate = 44100;
        int count = Mathf.Max(1, Mathf.RoundToInt(duration * rate));
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Clamp(sample(t), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Notes(string name, float[] frequencies, float noteDuration, float amplitude)
    {
        float total = frequencies.Length * noteDuration + 0.08f;
        return Build(name, total, t =>
        {
            int index = Mathf.Clamp((int)(t / noteDuration), 0, frequencies.Length - 1);
            float local = t - index * noteDuration;
            float envelope = Mathf.Exp(-local * 9f) * Mathf.Min(1f, local * 400f);
            return Mathf.Sin(2f * Mathf.PI * frequencies[index] * local) * envelope * amplitude;
        });
    }

    static AudioClip Sweep(string name, float duration, float from, float to, float amplitude, bool square = false)
    {
        return Build(name, duration, t =>
        {
            float k = Mathf.Clamp01(t / duration);
            float frequency = Mathf.Lerp(from, to, k);
            float phase = 2f * Mathf.PI * (from * t + 0.5f * (to - from) / duration * t * t);
            float wave = square ? (Mathf.Sin(phase) > 0f ? 0.6f : -0.6f) : Mathf.Sin(phase);
            float envelope = (1f - k) * (1f - k);
            return wave * amplitude * envelope;
        });
    }

    static AudioClip Thud(string name, float duration, float amplitude)
    {
        return Build(name, duration, t =>
        {
            float envelope = Mathf.Exp(-t * 40f);
            float noise = Mathf.Sin(t * 3000f) * 0.3f;
            return (Mathf.Sin(2f * Mathf.PI * 110f * t) + noise) * envelope * amplitude;
        });
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
