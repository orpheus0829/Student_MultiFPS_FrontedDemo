using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class HitBoxSceneDrawer
{
    private const float LineThickOffset = 0.025f;

    static HitBoxSceneDrawer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        TimelineClip clip = TimelineEditor.selectedClip;
        if (clip == null)
        {
            return;
        }
        HitBoxClip asset = clip.asset as HitBoxClip;
        if (asset == null)
        {
            return;
        }
        PlayableDirector director = TimelineEditor.inspectedDirector;
        if (director == null)
        {
            return;
        }
        TrackAsset track = clip.GetParentTrack();
        if (track == null)
        {
            return;
        }
        Transform rootTrans = director.GetGenericBinding(track) as Transform;
        if (rootTrans == null)
        {
            return;
        }

        Vector3 localOffset = asset.boxOffset;
        Vector3 worldCenter = rootTrans.TransformPoint(localOffset);
        bool canRotate = asset.hitBoxShape == HitBoxShape.Sector || asset.hitBoxShape == HitBoxShape.Box;
        bool rotateMode = Tools.current == Tool.Rotate && canRotate;

        Matrix4x4 localMatrix = Matrix4x4.TRS(rootTrans.position, rootTrans.rotation, rootTrans.lossyScale);
        using (new Handles.DrawingScope(localMatrix))
        {
            Vector3 localCenter = asset.boxOffset;
            if (asset.hitBoxShape == HitBoxShape.Sphere)
            {
                Handles.color = new Color(1f, 0.25f, 0.25f, 0.35f);
                Handles.SphereHandleCap(0, localCenter, Quaternion.identity, asset.boxRadius, EventType.Repaint);
                Handles.color = TimelineSceneStyle.Hit;
                Handles.SphereHandleCap(0, localCenter, Quaternion.identity, asset.boxRadius + LineThickOffset, EventType.Repaint);
                Vector3 labelPos = rootTrans.TransformPoint(localCenter + Vector3.up * (asset.boxRadius + 0.3f));
                TimelineSceneStyle.Tag($"攻击球 半径:{asset.boxRadius:F2}", labelPos, TimelineSceneStyle.Hit);
            }
            else if (asset.hitBoxShape == HitBoxShape.Sector)
            {
                DrawSectorShape(asset, rootTrans, localCenter);
            }
            else
            {
                DrawBoxShape(asset, rootTrans, localCenter);
            }
        }

        if (rotateMode)
        {
            Quaternion rot = rootTrans.rotation * Quaternion.Euler(asset.boxEuler);
            Quaternion newWorldRot = Handles.RotationHandle(rot, worldCenter);
            Quaternion newLocalRot = Quaternion.Inverse(rootTrans.rotation) * newWorldRot;
            if (Quaternion.Angle(Quaternion.Euler(asset.boxEuler), newLocalRot) > 0.01f)
            {
                Undo.RecordObject(asset, "修改判定旋转");
                asset.boxEuler = newLocalRot.eulerAngles;
                EditorUtility.SetDirty(asset);
            }
            TimelineSceneStyle.Tag($"判定旋转(本地欧拉) X:{asset.boxEuler.x:F0} Y:{asset.boxEuler.y:F0} Z:{asset.boxEuler.z:F0}", worldCenter + Vector3.up * 1.6f, TimelineSceneStyle.Hit);
            return;
        }

        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, rootTrans.rotation);
        Vector3 newLocalOffset = rootTrans.InverseTransformPoint(newWorldCenter);
        if (Vector3.Distance(localOffset, newLocalOffset) > 0.0001f)
        {
            Undo.RecordObject(asset, "修改碰撞盒偏移");
            asset.boxOffset = newLocalOffset;
            EditorUtility.SetDirty(asset);
        }

        if (asset.hitBoxShape == HitBoxShape.Sphere)
        {
            float oldRadius = asset.boxRadius;
            float newRadius = Handles.RadiusHandle(Quaternion.identity, worldCenter, oldRadius);
            if (!Mathf.Approximately(oldRadius, newRadius))
            {
                Undo.RecordObject(asset, "修改球体半径");
                asset.boxRadius = Mathf.Max(0.001f, newRadius);
                EditorUtility.SetDirty(asset);
            }
        }
        else if (asset.hitBoxShape == HitBoxShape.Sector)
        {
            DrawSectorHandles(asset, rootTrans, worldCenter, localOffset);
        }
        else
        {
            Quaternion boxRot = rootTrans.rotation * Quaternion.Euler(asset.boxEuler);
            Vector3 oldSize = asset.hitBoxSize;
            Vector3 newSize = Handles.ScaleHandle(oldSize, worldCenter, boxRot, 1f);
            if (Vector3.Distance(oldSize, newSize) > 0.0001f)
            {
                Undo.RecordObject(asset, "修改盒尺寸");
                newSize.x = Mathf.Max(0.001f, newSize.x);
                newSize.y = Mathf.Max(0.001f, newSize.y);
                newSize.z = Mathf.Max(0.001f, newSize.z);
                asset.hitBoxSize = newSize;
                EditorUtility.SetDirty(asset);
            }
        }
    }

    private static void DrawSectorShape(HitBoxClip asset, Transform rootTrans, Vector3 localCenter)
    {
        float radius = Mathf.Max(0.001f, asset.boxRadius);
        float inner = Mathf.Clamp(asset.sectorInnerRadius, 0f, Mathf.Max(0f, radius - 0.01f));
        float halfAngle = asset.sectorAngle * 0.5f;
        bool hasHeight = asset.sectorHeight > 0.03f;
        float halfH = Mathf.Max(0f, asset.sectorHeight) * 0.5f;
        Quaternion rot = Quaternion.Euler(asset.boxEuler);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 up = rot * Vector3.up;
        Vector3 leftDir = Quaternion.AngleAxis(-halfAngle, up) * fwd;
        Vector3 rightDir = Quaternion.AngleAxis(halfAngle, up) * fwd;
        Vector3 lowCenter = hasHeight ? localCenter - up * halfH : localCenter;
        Vector3 highCenter = hasHeight ? localCenter + up * halfH : localCenter;

        Handles.color = new Color(1f, 0.45f, 0.25f, 0.2f);
        if (!hasHeight && inner <= 0.01f)
        {
            Handles.DrawSolidArc(localCenter, up, leftDir, asset.sectorAngle, radius);
        }
        Handles.color = TimelineSceneStyle.Hit;
        Handles.DrawWireArc(lowCenter, up, leftDir, asset.sectorAngle, radius);
        if (inner > 0.01f)
        {
            Handles.DrawWireArc(lowCenter, up, leftDir, asset.sectorAngle, inner);
        }
        if (hasHeight)
        {
            Handles.DrawWireArc(highCenter, up, leftDir, asset.sectorAngle, radius);
            if (inner > 0.01f)
            {
                Handles.DrawWireArc(highCenter, up, leftDir, asset.sectorAngle, inner);
            }
        }
        Vector3 lowLeftInner = lowCenter + leftDir * inner;
        Vector3 lowRightInner = lowCenter + rightDir * inner;
        Vector3 lowLeftOuter = lowCenter + leftDir * radius;
        Vector3 lowRightOuter = lowCenter + rightDir * radius;
        Handles.DrawLine(lowLeftInner, lowLeftOuter, TimelineSceneStyle.ThinLineWidth);
        Handles.DrawLine(lowRightInner, lowRightOuter, TimelineSceneStyle.ThinLineWidth);
        if (hasHeight)
        {
            Vector3 highLeftOuter = highCenter + leftDir * radius;
            Vector3 highRightOuter = highCenter + rightDir * radius;
            Vector3 highLeftInner = highCenter + leftDir * inner;
            Vector3 highRightInner = highCenter + rightDir * inner;
            Handles.DrawLine(highLeftInner, highLeftOuter, TimelineSceneStyle.ThinLineWidth);
            Handles.DrawLine(highRightInner, highRightOuter, TimelineSceneStyle.ThinLineWidth);
            Handles.DrawLine(lowLeftOuter, highLeftOuter, TimelineSceneStyle.ThinLineWidth);
            Handles.DrawLine(lowRightOuter, highRightOuter, TimelineSceneStyle.ThinLineWidth);
            if (inner > 0.01f)
            {
                Handles.DrawLine(lowLeftInner, highLeftInner, TimelineSceneStyle.ThinLineWidth);
                Handles.DrawLine(lowRightInner, highRightInner, TimelineSceneStyle.ThinLineWidth);
            }
        }
        Vector3 labelPos = rootTrans.TransformPoint(localCenter + up * (halfH + 0.6f));
        string shapeText = inner > 0.01f ? $"内径:{inner:F2}" : "实心";
        TimelineSceneStyle.Tag($"攻击扇柱 {shapeText} 半径:{radius:F2} 角:{asset.sectorAngle:F0}° 高:{asset.sectorHeight:F2}", labelPos, TimelineSceneStyle.Hit);
    }

    private static void DrawBoxShape(HitBoxClip asset, Transform rootTrans, Vector3 localCenter)
    {
        Handles.color = TimelineSceneStyle.HitBox;
        Vector3 half = asset.hitBoxSize * 0.5f;
        Quaternion rot = Quaternion.Euler(asset.boxEuler);
        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3((i & 1) == 0 ? -half.x : half.x, ((i >> 1) & 1) == 0 ? -half.y : half.y, ((i >> 2) & 1) == 0 ? -half.z : half.z);
            corners[i] = localCenter + rot * p;
        }
        for (int i = 0; i < 8; i++)
        {
            for (int b = 0; b < 3; b++)
            {
                int j = i ^ (1 << b);
                if (j > i)
                {
                    Handles.DrawLine(corners[i], corners[j]);
                }
            }
        }
        Vector3 labelPos = rootTrans.TransformPoint(localCenter + rot * Vector3.up * (half.y + 0.3f));
        TimelineSceneStyle.Tag($"攻击盒 尺寸:{asset.hitBoxSize:F2}", labelPos, TimelineSceneStyle.HitBox);
    }

    private static void DrawSectorHandles(HitBoxClip asset, Transform rootTrans, Vector3 worldCenter, Vector3 localOffset)
    {
        float radius = Mathf.Max(0.001f, asset.boxRadius);
        float halfH = Mathf.Max(0f, asset.sectorHeight) * 0.5f;
        Quaternion rot = Quaternion.Euler(asset.boxEuler);
        Vector3 fwdL = rot * Vector3.forward;
        Vector3 upL = rot * Vector3.up;
        Vector3 worldFwd = rootTrans.rotation * fwdL;
        Vector3 worldUp = rootTrans.rotation * upL;

        Vector3 tipWorld = worldCenter + worldFwd * radius;
        Vector3 tipDrag = Handles.PositionHandle(tipWorld, rootTrans.rotation);
        Vector3 alongLocal = rootTrans.InverseTransformPoint(tipDrag) - localOffset;
        float along = Vector3.Dot(alongLocal, fwdL);
        if (along > 0.05f && !Mathf.Approximately(along, radius))
        {
            Undo.RecordObject(asset, "修改扇形外半径");
            asset.boxRadius = along;
            EditorUtility.SetDirty(asset);
        }

        if (asset.sectorInnerRadius > 0.05f)
        {
            Vector3 innerWorld = worldCenter + worldFwd * asset.sectorInnerRadius;
            Vector3 innerDrag = Handles.PositionHandle(innerWorld, rootTrans.rotation);
            Vector3 innerLocal = rootTrans.InverseTransformPoint(innerDrag) - localOffset;
            float alongInner = Vector3.Dot(innerLocal, fwdL);
            float maxInner = Mathf.Max(0f, asset.boxRadius - 0.05f);
            if (alongInner > 0.01f && !Mathf.Approximately(alongInner, asset.sectorInnerRadius))
            {
                Undo.RecordObject(asset, "修改扇形内径");
                asset.sectorInnerRadius = Mathf.Min(alongInner, maxInner);
                EditorUtility.SetDirty(asset);
            }
        }

        float halfAngle = asset.sectorAngle * 0.5f;
        Vector3 rightDirL = Quaternion.AngleAxis(halfAngle, upL) * fwdL;
        Vector3 edgeWorld = rootTrans.TransformPoint(localOffset + rightDirL * radius);
        Vector3 edgeDrag = Handles.PositionHandle(edgeWorld, rootTrans.rotation);
        Vector3 dirLocal = rootTrans.InverseTransformPoint(edgeDrag) - localOffset;
        if (dirLocal.sqrMagnitude > 0.0025f)
        {
            float rel = Mathf.Abs(Vector3.SignedAngle(fwdL, dirLocal, upL));
            float sweep = Mathf.Clamp(rel * 2f, 5f, 340f);
            if (!Mathf.Approximately(sweep, asset.sectorAngle))
            {
                Undo.RecordObject(asset, "修改扇形角度");
                asset.sectorAngle = sweep;
                EditorUtility.SetDirty(asset);
            }
        }

        if (asset.sectorHeight > 0.03f)
        {
            Vector3 topWorld = worldCenter + worldUp * halfH;
            Vector3 topDrag = Handles.PositionHandle(topWorld, rootTrans.rotation);
            Vector3 heightLocal = rootTrans.InverseTransformPoint(topDrag) - localOffset;
            float newHalfH = Mathf.Max(0.05f, Vector3.Dot(heightLocal, upL));
            if (!Mathf.Approximately(newHalfH, halfH))
            {
                Undo.RecordObject(asset, "修改扇形柱高度");
                asset.sectorHeight = newHalfH * 2f;
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
