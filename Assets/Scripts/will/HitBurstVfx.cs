using UnityEngine;

public static class HitBurstVfx
{
    public static void Spawn(Vector2 position, ColorType colorType)
    {
        GameObject burst = new("Hit Burst VFX");
        burst.transform.position = position;

        ParticleSystem particles = burst.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = 0.12f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 5.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = GetColor(colorType);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.16f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 30;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Default");
        if (shader != null)
        {
            renderer.material = new Material(shader);
        }

        particles.Emit(16);
        Object.Destroy(burst, 0.5f);
    }

    private static Color GetColor(ColorType colorType) => colorType switch
    {
        ColorType.Blue => new Color(0.35f, 0.82f, 1f, 1f),
        ColorType.Yellow => new Color(1f, 0.86f, 0.22f, 1f),
        _ => new Color(1f, 0.25f, 0.18f, 1f),
    };
}
