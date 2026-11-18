using UnityEngine;

/// <summary>
/// Explosao leve da granada, montada por codigo: rajada de particulas + flash de luz
/// que apaga rapido. Destroi a si mesma.
/// </summary>
public class ExplosionEffect : MonoBehaviour
{
    static Material fallbackMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => fallbackMaterial = null;

    Light flash;
    float t;
    const float FlashTime = 0.3f;
    const float FlashIntensity = 12f;

    public static void Spawn(Vector3 position, Material particleMaterial)
    {
        var go = new GameObject("GrenadeExplosion");
        go.transform.position = position;

        // ---- particulas
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.25f, 0.05f));
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)70) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = particleMaterial != null ? particleMaterial : GetFallbackMaterial();

        ps.Play();

        // ---- flash
        var fx = go.AddComponent<ExplosionEffect>();
        fx.flash = go.AddComponent<Light>();
        fx.flash.type = LightType.Point;
        fx.flash.color = new Color(1f, 0.55f, 0.2f);
        fx.flash.range = 5f;
        fx.flash.intensity = FlashIntensity;
        fx.flash.shadows = LightShadows.None;

        Destroy(go, 1.5f);
    }

    static Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        fallbackMaterial = new Material(s);
        return fallbackMaterial;
    }

    void Update()
    {
        if (flash == null) return;
        t += Time.deltaTime;
        flash.intensity = Mathf.Lerp(FlashIntensity, 0f, t / FlashTime);
        if (t >= FlashTime) flash.enabled = false;
    }
}
