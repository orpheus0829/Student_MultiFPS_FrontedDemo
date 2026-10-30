using UnityEngine;

public enum PlayerInputPhase
{
    Started = 0,
    Performed = 1,
    Canceled = 2
}

public readonly struct PlayerInputCommand
{
    public GJInputAction Action { get; }
    public PlayerInputPhase Phase { get; }
    public Vector2 Value { get; }

    public PlayerInputCommand(GJInputAction action, PlayerInputPhase phase, Vector2 value = default)
    {
        Action = action;
        Phase = phase;
        Value = value;
    }
}
