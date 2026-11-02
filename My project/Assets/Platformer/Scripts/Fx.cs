using UnityEngine;

/// <summary>一次性粒子特效工具（金币、死亡、落地、踩怪）。</summary>
public static class Fx
{
    static Material particleMaterial;

    static Material GetMaterial()
    {
        if (particleMaterial != null) return particleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        particleMaterial = new Material(shader);
        particleMaterial.name = "PlatformerFx";
        particleMaterial.hideFlags = HideFlags.DontSave;

        Texture2D white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        white.hideFlags = HideFlags.DontSave;
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        white.SetPixels32(pixels);
        white.Apply();

        if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", white);
        if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", white);
        if (particleMaterial.HasProperty("_BaseColor")) particleMaterial.SetColor("_BaseColor", Color.white);
        if (particleMaterial.HasProperty("_Color")) particleMaterial.SetColor("_Color", Color.white);

        return particleMaterial;
    }

    public static void Burst(Vector3 position, Color colorA, Color colorB, int count, float speed,
                             float size = 0.12f, float lifetime = 0.45f)
    {
        Material material = GetMaterial();
        if (material == null) return;

        GameObject go = new GameObject("FxBurst");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = 1.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 30;
        }

        ps.Play();
        Object.Destroy(go, lifetime + 0.6f);
    }

    public static void Dust(Vector3 position, int count)
    {
        Burst(position, new Color(1f, 1f, 1f, 0.55f), new Color(0.82f, 0.9f, 1f, 0.25f), count, 2.6f, 0.16f, 0.32f);
    }
}
