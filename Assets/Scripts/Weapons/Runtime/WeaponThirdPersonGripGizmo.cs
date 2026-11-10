using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponThirdPersonGripGizmo : MonoBehaviour
{
    private const float WristRadius = 0.015f;
    private const float AxisLength = 0.08f;
    private const float PalmHalfWidth = 0.045f;
    private const float PalmHalfLength = 0.06f;

    private void OnDrawGizmos()
    {
        DrawWrist();
    }

    private void OnDrawGizmosSelected()
    {
        DrawWrist();
    }

    private void DrawWrist()
    {
        Transform anchor = transform;
        if (anchor.parent && anchor.parent.name != "ThirdPersonGrip")
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(anchor.position, WristRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(anchor.position, anchor.TransformPoint(Vector3.right * AxisLength));
        Gizmos.color = Color.green;
        Gizmos.DrawLine(anchor.position, anchor.TransformPoint(Vector3.up * AxisLength));
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(anchor.position, anchor.TransformPoint(Vector3.forward * AxisLength));

        Vector3 corner00 = anchor.TransformPoint(new Vector3(-PalmHalfWidth, 0f, -PalmHalfLength));
        Vector3 corner10 = anchor.TransformPoint(new Vector3(PalmHalfWidth, 0f, -PalmHalfLength));
        Vector3 corner11 = anchor.TransformPoint(new Vector3(PalmHalfWidth, 0f, PalmHalfLength));
        Vector3 corner01 = anchor.TransformPoint(new Vector3(-PalmHalfWidth, 0f, PalmHalfLength));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(corner00, corner10);
        Gizmos.DrawLine(corner10, corner11);
        Gizmos.DrawLine(corner11, corner01);
        Gizmos.DrawLine(corner01, corner00);
        Gizmos.color = previousColor;
    }
}
