using System;
using UnityEngine;

public readonly struct AINoise
{
    public Vector3 Position { get; }
    public float Radius { get; }
    public float Uncertainty { get; }
    public Transform Source { get; }

    public AINoise(Vector3 position, float radius, float uncertainty, Transform source)
    {
        Position = position;
        Radius = radius;
        Uncertainty = uncertainty;
        Source = source;
    }
}

public static class AINoiseBus
{
    public static event Action<AINoise> NoiseEmitted;

    public static void Emit(
        Vector3 position,
        float radius,
        float uncertainty = 1.5f,
        Transform source = null)
    {
        NoiseEmitted?.Invoke(new AINoise(position, radius, uncertainty, source));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        NoiseEmitted = null;
    }
}
