using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombExplosionEffect : MonoBehaviour
{
    private readonly List<Material> _runtimeMaterials = new List<Material>();
    private Texture2D _particleTexture;
    private Light _flashLight;
    private float _flashRemaining;

    private void Update()
    {
        if (!_flashLight || _flashRemaining <= 0f)
        {
            return;
        }

        _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.deltaTime);
        float normalized = _flashRemaining / 0.45f;
        _flashLight.intensity = 26000f * normalized * normalized;
        _flashLight.range = Mathf.Lerp(12f, 38f, normalized);
        if (_flashRemaining <= 0f)
        {
            _flashLight.enabled = false;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _runtimeMaterials.Count; i++)
        {
            if (_runtimeMaterials[i])
            {
                Destroy(_runtimeMaterials[i]);
            }
        }

        if (_particleTexture)
        {
            Destroy(_particleTexture);
        }
    }

    public static void Spawn(Vector3 worldPosition, float scale)
    {
        GameObject root = new GameObject("C4_Explosion");
        root.transform.position = worldPosition + Vector3.up * 0.3f;
        BombExplosionEffect effect = root.AddComponent<BombExplosionEffect>();
        effect.Build(Mathf.Max(1f, scale));
        Destroy(root, 6f);
    }

    private void Build(float scale)
    {
        _particleTexture = CreateSoftParticleTexture();
        Material fireMaterial = CreateMaterial(
            "C4 Fire",
            new Color(1f, 0.22f, 0.015f, 1f));
        Material smokeMaterial = CreateMaterial(
            "C4 Smoke",
            new Color(0.12f, 0.1f, 0.085f, 0.78f));
        Material sparkMaterial = CreateMaterial(
            "C4 Sparks",
            new Color(1f, 0.58f, 0.08f, 1f));

        CreateFireball(scale, fireMaterial);
        CreateSmoke(scale, smokeMaterial);
        CreateSparks(scale, sparkMaterial);
        CreateShockwave(scale, fireMaterial);
        CreateFlash(scale);
    }

    private void CreateFireball(float scale, Material material)
    {
        ParticleSystem particles = CreateSystem("Fireball", material);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 11f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(4f * scale, 10f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.78f, 0.14f, 1f),
            new Color(1f, 0.08f, 0.01f, 0.92f));
        main.maxParticles = 100;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.7f * scale;

        SetBurst(particles, 86);
        SetFadeGradient(
            particles,
            new Color(1f, 0.82f, 0.18f, 1f),
            new Color(0.72f, 0.04f, 0.005f, 0f));
        particles.Play(true);
    }

    private void CreateSmoke(float scale, Material material)
    {
        ParticleSystem particles = CreateSystem("Smoke", material);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 7f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(5f * scale, 13f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.24f, 0.21f, 0.18f, 0.8f),
            new Color(0.055f, 0.05f, 0.045f, 0.72f));
        main.gravityModifier = -0.22f;
        main.maxParticles = 80;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.1f * scale;

        SetBurst(particles, 58);
        SetFadeGradient(
            particles,
            new Color(0.25f, 0.2f, 0.16f, 0.72f),
            new Color(0.03f, 0.03f, 0.03f, 0f));
        particles.Play(true);
    }

    private void CreateSparks(float scale, Material material)
    {
        ParticleSystem particles = CreateSystem("Sparks", material);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f * scale, 30f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f * scale, 0.38f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.42f, 1f),
            new Color(1f, 0.2f, 0.015f, 1f));
        main.gravityModifier = 1.6f;
        main.maxParticles = 180;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.9f * scale;

        SetBurst(particles, 150);
        SetFadeGradient(
            particles,
            new Color(1f, 0.9f, 0.32f, 1f),
            new Color(1f, 0.08f, 0.005f, 0f));
        particles.Play(true);
    }

    private void CreateShockwave(float scale, Material material)
    {
        ParticleSystem particles = CreateSystem("Shockwave", material);
        particles.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(22f * scale, 32f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.8f * scale);
        main.startColor = new Color(1f, 0.56f, 0.09f, 0.82f);
        main.maxParticles = 100;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.3f * scale;
        shape.radiusThickness = 0f;

        SetBurst(particles, 84);
        SetFadeGradient(
            particles,
            new Color(1f, 0.72f, 0.18f, 0.82f),
            new Color(1f, 0.1f, 0.01f, 0f));
        particles.Play(true);
    }

    private ParticleSystem CreateSystem(string objectName, Material material)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystemRenderer particleRenderer =
            child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = material;
        return particles;
    }

    private void CreateFlash(float scale)
    {
        _flashLight = gameObject.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.color = new Color(1f, 0.31f, 0.055f, 1f);
        _flashLight.intensity = 26000f * scale;
        _flashLight.range = 38f * scale;
        _flashLight.shadows = LightShadows.None;
        _flashRemaining = 0.45f;
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!shader)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }
        if (!shader)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        material.mainTexture = _particleTexture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", _particleTexture);
        }
        material.renderQueue = 3100;
        _runtimeMaterials.Add(material);
        return material;
    }

    private static void SetBurst(ParticleSystem particles, short count)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    private static void SetFadeGradient(
        ParticleSystem particles,
        Color startColor,
        Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true);
        texture.name = "C4 Explosion Soft Particle";
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float inverseRadius = 2f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) * inverseRadius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
