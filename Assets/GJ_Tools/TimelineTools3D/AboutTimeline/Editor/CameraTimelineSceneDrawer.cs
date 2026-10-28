using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CameraTimelineSceneDrawer
{
    static CameraTimelineSceneDrawer()
    {
        SceneView.duringSceneGui += OnSceneDraw;
    }

    private static void OnSceneDraw(SceneView sceneView)
    {
        TimelineClip clip = TimelineEditor.selectedClip;
        if (clip == null)
        {
            return;
        }
        CameraTimelineClip camClip = clip.asset as CameraTimelineClip;
        if (camClip == null)
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
        TimelineCamRig rig = director.GetGenericBinding(track) as TimelineCamRig;
        if (rig == null)
        {
            rig = Object.FindFirstObjectByType<TimelineCamRig>();
        }
        if (rig == null)
        {
            return;
        }
        Transform anchor = rig.target != null ? rig.target : rig.transform;

        Vector3 originWorldPos = rig.OrbitPosition(anchor);
        Vector3 targetWorldPos = anchor.TransformPoint(camClip.cameraTargetLocalPos);
        Quaternion targetWorldRot = anchor.rotation * Quaternion.Euler(camClip.cameraTargetEuler);

        Handles.color = TimelineSceneStyle.CamLine;
        Handles.DrawLine(originWorldPos, targetWorldPos, TimelineSceneStyle.MainLineWidth);

        float coneLength = 3f;
        float coneHalfAngle = 30f;
        Handles.color = TimelineSceneStyle.CamCone;
        using (new Handles.DrawingScope(Matrix4x4.TRS(targetWorldPos, targetWorldRot, Vector3.one)))
        {
            Vector3 forwardTip = Vector3.forward * coneLength;
            int segmentCount = 16;
            float radStep = Mathf.PI * 2f / segmentCount;
            float radius = Mathf.Tan(Mathf.Deg2Rad * coneHalfAngle) * coneLength;
            Vector3 lastPoint = new Vector3(Mathf.Cos(0) * radius, Mathf.Sin(0) * radius, coneLength);
            for (int i = 1; i <= segmentCount; i++)
            {
                float rad = radStep * i;
                Vector3 curPoint = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, coneLength);
                Handles.DrawLine(lastPoint, curPoint, TimelineSceneStyle.ThinLineWidth);
                lastPoint = curPoint;
            }
            Handles.DrawLine(Vector3.zero, forwardTip, 3f);
            int boneCount = 8;
            float boneStep = Mathf.PI * 2f / boneCount;
            for (int i = 0; i < boneCount; i++)
            {
                float rad = boneStep * i;
                Vector3 ringPoint = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, coneLength);
                Handles.DrawLine(Vector3.zero, ringPoint, 1.5f);
            }
        }

        Vector3 newTargetWorld = Handles.PositionHandle(targetWorldPos, anchor.rotation);
        Vector3 newLocalOffset = anchor.InverseTransformPoint(newTargetWorld);
        if (Vector3.Distance(camClip.cameraTargetLocalPos, newLocalOffset) > 0.0001f)
        {
            Undo.RecordObject(camClip, "修改相机目标机位坐标");
            camClip.cameraTargetLocalPos = newLocalOffset;
            EditorUtility.SetDirty(camClip);
        }

        Quaternion newTargetRot = Handles.RotationHandle(targetWorldRot, targetWorldPos);
        Quaternion localRot = Quaternion.Inverse(anchor.rotation) * newTargetRot;
        if (Quaternion.Angle(targetWorldRot, newTargetRot) > 0.01f)
        {
            Undo.RecordObject(camClip, "修改相机目标机位旋转");
            camClip.cameraTargetEuler = localRot.eulerAngles;
            EditorUtility.SetDirty(camClip);
        }

        string tip = camClip.useSurroundMode ? "圆弧终点预览(拖拽不改变圆弧半径)" : "直线终点机位(可拖位置+旋转)";
        TimelineSceneStyle.Tag(tip, targetWorldPos + Vector3.up * 0.3f, TimelineSceneStyle.CamLine);
        TimelineSceneStyle.Tag("相机基准机位(圆弧起点方位)", originWorldPos + Vector3.up * 0.3f, TimelineSceneStyle.CamLine);

        sceneView.Repaint();
    }
}
