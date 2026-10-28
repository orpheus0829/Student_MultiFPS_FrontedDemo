using UnityEditor;
using UnityEngine;

public static class TimelineSceneStyle
{
    public static readonly Color Move = new Color(0.2f, 0.85f, 1f, 0.95f);
    public static readonly Color MoveEnd = new Color(1f, 1f, 1f, 1f);
    public static readonly Color Hit = new Color(1f, 0.3f, 0.3f, 0.85f);
    public static readonly Color HitBox = new Color(1f, 0.9f, 0.3f, 0.9f);
    public static readonly Color Effect = new Color(1f, 0.5f, 0.85f, 0.95f);
    public static readonly Color CamLine = new Color(0.35f, 0.6f, 1f, 0.95f);
    public static readonly Color CamCone = new Color(1f, 1f, 1f, 0.35f);
    public static readonly Color Warn = new Color(1f, 0.25f, 0.2f, 1f);

    public const float MainLineWidth = 4f;
    public const float ThinLineWidth = 2f;

    public static void Tag(string text, Vector3 pos)
    {
        Handles.Label(pos, text);
    }

    public static void Tag(string text, Vector3 pos, Color color)
    {
        Handles.color = color;
        Handles.Label(pos, text);
        Handles.color = Color.white;
    }
}
