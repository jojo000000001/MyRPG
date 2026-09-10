using UnityEngine;

/// <summary>
/// 巨龙喷火口的运行时火焰粒子。资源包只有喷火骨骼动画，不含火焰特效。
/// </summary>
[DisallowMultipleComponent]
public sealed class DragonFlameBreath : MonoBehaviour
{
    private Transform owner;
    private Transform mouth;
    private ParticleSystem core;
    private ParticleSystem embers;
    private Light glow;
    private static Texture2D softParticleTexture;
    private static Material additiveMaterial;

    public static DragonFlameBreath Ensure(Transform owner, Transform mouth)
    {
        if (owner == null)
            return null;

        DragonFlameBreath existing = owner.GetComponentInChildren<DragonFlameBreath>(true);
        if (existing != null)
        {
            existing.owner = owner;
            existing.mouth = mouth != null ? mouth : existing.mouth;
            return existing;
        }

        GameObject host = new GameObject("DragonFlameBreath");
        host.transform.SetParent(owner, false);
        DragonFlameBreath breath = host.AddComponent<DragonFlameBreath>();
        breath.owner = owner;
        breath.mouth = mouth;
        breath.Build();
        return breath;
    }

    public void Play()
    {
        SetRenderersEnabled(true);
        AlignToMouth();
        if (core != null)
            core.Play(true);
        if (embers != null)
            embers.Play(true);
        if (glow != null)
            glow.enabled = true;
    }

    public void Stop()
    {
        if (core != null)
            core.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (embers != null)
            embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (glow != null)
            glow.enabled = false;
        SetRenderersEnabled(false);
    }

    private void LateUpdate()
    {
        if (core == null || !core.isPlaying)
            return;

        AlignToMouth();
        if (glow == null || !glow.enabled)
            return;

        glow.intensity = 3.4f + Mathf.Sin(Time.time * 18f) * 0.7f;
    }

    private void AlignToMouth()
    {
        Vector3 origin = mouth != null ? mouth.position : transform.position;
        Vector3 forward = owner != null ? owner.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        transform.position = origin + forward * 0.45f + Vector3.up * 0.08f;
        transform.rotation = Quaternion.LookRotation((forward + Vector3.down * 0.12f).normalized, Vector3.up);
        transform.localScale = Vector3.one;
    }

    private void Build()
    {
        Material material = GetAdditiveMaterial();
        core = CreateSystem(
            "Core",
            material,
            duration: 1f,
            lifetime: new Vector2(0.55f, 0.85f),
            speed: new Vector2(14f, 22f),
            size: new Vector2(0.7f, 1.35f),
            rate: 130f,
            coneAngle: 14f,
            stretched: true,
            gravity: 0.15f,
            startColor: new Color(1f, 0.92f, 0.45f, 1f),
            hotColor: new Color(1f, 0.95f, 0.7f),
            coolColor: new Color(1f, 0.28f, 0.04f));

        embers = CreateSystem(
            "Embers",
            material,
            duration: 1f,
            lifetime: new Vector2(0.6f, 1.0f),
            speed: new Vector2(10f, 16f),
            size: new Vector2(0.35f, 0.75f),
            rate: 50f,
            coneAngle: 20f,
            stretched: false,
            gravity: 0.45f,
            startColor: new Color(1f, 0.45f, 0.08f, 1f),
            hotColor: new Color(1f, 0.55f, 0.12f),
            coolColor: new Color(0.35f, 0.05f, 0.01f));

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 1.6f);
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.48f, 0.12f);
        glow.range = 11f;
        glow.intensity = 3.4f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    private ParticleSystem CreateSystem(
        string childName,
        Material material,
        float duration,
        Vector2 lifetime,
        Vector2 speed,
        Vector2 size,
        float rate,
        float coneAngle,
        bool stretched,
        float gravity,
        Color startColor,
        Color hotColor,
        Color coolColor)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        ParticleSystem system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = duration;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = startColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 350;
        main.gravityModifier = gravity;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.12f;
        shape.length = 0.25f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(hotColor, 0f),
                new GradientColorKey(startColor, 0.35f),
                new GradientColorKey(coolColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.2f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = stretched
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        if (stretched)
        {
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 1.8f;
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return system;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (core != null)
        {
            ParticleSystemRenderer coreRenderer = core.GetComponent<ParticleSystemRenderer>();
            if (coreRenderer != null)
                coreRenderer.enabled = enabled;
        }

        if (embers != null)
        {
            ParticleSystemRenderer emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
            if (emberRenderer != null)
                emberRenderer.enabled = enabled;
        }
    }

    private static Material GetAdditiveMaterial()
    {
        if (additiveMaterial != null)
            return additiveMaterial;

        Shader shader = Shader.Find("Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        additiveMaterial = new Material(shader)
        {
            name = "DragonFlameAdditive",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = GetSoftParticleTexture()
        };

        if (additiveMaterial.HasProperty("_Color"))
            additiveMaterial.SetColor("_Color", Color.white);
        if (additiveMaterial.HasProperty("_TintColor"))
            additiveMaterial.SetColor("_TintColor", new Color(1f, 0.7f, 0.2f, 0.5f));

        return additiveMaterial;
    }

    private static Texture2D GetSoftParticleTexture()
    {
        if (softParticleTexture != null)
            return softParticleTexture;

        const int size = 64;
        softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DragonFlameSoftParticle",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                alpha *= alpha;
                softParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softParticleTexture.Apply(false, true);
        return softParticleTexture;
    }
}
