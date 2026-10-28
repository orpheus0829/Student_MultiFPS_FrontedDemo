using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TransformTimelineSceneDrawer
{
    static TransformTimelineSceneDrawer()
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
        TransformTimelineClip transClip = clip.asset as TransformTimelineClip;
        if (transClip == null)
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
        Transform bindTrans = director.GetGenericBinding(track) as Transform;
        if (bindTrans == null)
        {
            return;
        }

        MoveMode curMode = transClip.moveMode;
        float clipDuration = (float)clip.duration;
        Vector3 worldStart = bindTrans.position;

        bool speedZeroWarn = false;
        if (curMode == MoveMode.SpeedAndDistance && Mathf.Approximately(transClip.moveSpeed, 0f))
        {
            speedZeroWarn = true;
        }
        if (curMode == MoveMode.VariableSpeed)
        {
            if (Mathf.Approximately(transClip.startSpeed, 0f) && Mathf.Approximately(transClip.endSpeed, 0f))
            {
                speedZeroWarn = true;
            }
        }
        if (speedZeroWarn)
        {
            TimelineSceneStyle.Tag("速度全为0，位移不生效", worldStart + Vector3.up * 1.2f, TimelineSceneStyle.Warn);
        }

        Handles.color = TimelineSceneStyle.Move;

        if (curMode == MoveMode.FixedEndPos)
        {
            Vector3 worldEnd = bindTrans.TransformPoint(transClip.endPos);
            Quaternion endRot = bindTrans.rotation * Quaternion.Euler(transClip.endEuler);
            Handles.DrawLine(worldStart, worldEnd, TimelineSceneStyle.MainLineWidth);
            Handles.SphereHandleCap(0, worldStart, Quaternion.identity, 0.15f, EventType.Repaint);
            TimelineSceneStyle.Tag("位移起点", worldStart + Vector3.up * 0.3f);

            Vector3 newWorldEnd = Handles.PositionHandle(worldEnd, bindTrans.rotation);
            Vector3 newLocalEnd = bindTrans.InverseTransformPoint(newWorldEnd);
            if (Vector3.Distance(transClip.endPos, newLocalEnd) > 0.0001f)
            {
                Undo.RecordObject(transClip, "修改瞬移终点");
                transClip.endPos = newLocalEnd;
                EditorUtility.SetDirty(transClip);
            }

            Handles.SphereHandleCap(0, worldEnd, endRot, 0.15f, EventType.Repaint);
            Handles.color = TimelineSceneStyle.MoveEnd;
            Handles.DrawLine(worldEnd, worldEnd + endRot * Vector3.forward * 0.6f, TimelineSceneStyle.ThinLineWidth);
            TimelineSceneStyle.Tag("瞬移终点(可穿墙,本地偏移)", worldEnd + Vector3.up * 0.3f);
        }
        else if (curMode == MoveMode.SpeedAndDistance || curMode == MoveMode.VariableSpeed)
        {
            Vector3 rawLocalDir = transClip.direction;
            rawLocalDir.Normalize();
            Vector3 worldDir = bindTrans.TransformDirection(rawLocalDir);
            Vector3 worldEnd = worldStart + worldDir * transClip.totalDistance;

            Handles.DrawLine(worldStart, worldEnd, TimelineSceneStyle.MainLineWidth);
            Handles.SphereHandleCap(0, worldStart, Quaternion.identity, 0.15f, EventType.Repaint);
            TimelineSceneStyle.Tag("位移起点", worldStart + Vector3.up * 0.3f);

            Vector3 newWorldEnd = Handles.PositionHandle(worldEnd, bindTrans.rotation);
            Vector3 deltaWorld = newWorldEnd - worldStart;
            float dragDist = Mathf.Max(0.01f, deltaWorld.magnitude);
            Vector3 newLocalDir = bindTrans.InverseTransformDirection(deltaWorld);
            newLocalDir.Normalize();

            if (Vector3.Distance(worldEnd, newWorldEnd) > 0.0001f && clipDuration > 0.0001f)
            {
                Undo.RecordObject(transClip, "拖拽调整路线，计算速度");
                transClip.direction = newLocalDir;
                transClip.totalDistance = dragDist;
                if (curMode == MoveMode.SpeedAndDistance)
                {
                    transClip.moveSpeed = dragDist / clipDuration;
                }
                else
                {
                    float v0 = transClip.startSpeed;
                    transClip.endSpeed = (2f * dragDist / clipDuration) - v0;
                }
                EditorUtility.SetDirty(transClip);
            }

            Handles.SphereHandleCap(0, worldEnd, Quaternion.identity, 0.15f, EventType.Repaint);
        }
        else if (curMode == MoveMode.CircleRotate)
        {
            Vector3 worldStartPos = bindTrans.position;
            Vector3 originalWorldCenter = bindTrans.TransformPoint(transClip.circleCenterLocal);
            Vector3 dragCenter = Handles.DoPositionHandle(originalWorldCenter, Quaternion.identity);
            bool centerChanged = Vector3.Distance(dragCenter, originalWorldCenter) > 0.0001f;
            if (centerChanged)
            {
                Undo.RecordObject(transClip, "修改绕圈中心点");
                transClip.circleCenterLocal = bindTrans.InverseTransformPoint(dragCenter);
                EditorUtility.SetDirty(transClip);
            }

            Vector3 realWorldCenter = bindTrans.TransformPoint(transClip.circleCenterLocal);
            Vector3 radiusOrigin = realWorldCenter + (worldStartPos - realWorldCenter).normalized * transClip.circleRadius;
            Vector3 dragRadiusPoint = Handles.DoPositionHandle(radiusOrigin, Quaternion.identity);
            float rawRadius = Vector3.Distance(dragRadiusPoint, realWorldCenter);
            bool radiusChanged = !Mathf.Approximately(rawRadius, transClip.circleRadius);
            if (radiusChanged)
            {
                Undo.RecordObject(transClip, "修改转圈半径");
                transClip.circleRadius = rawRadius;
                EditorUtility.SetDirty(transClip);
            }

            Handles.color = TimelineSceneStyle.Move;
            Handles.DrawWireDisc(realWorldCenter, Vector3.up, transClip.circleRadius);
            TimelineSceneStyle.Tag("绕圈中心点(可拖拽)", realWorldCenter + Vector3.up * 2.2f);
            TimelineSceneStyle.Tag($"绕圈半径:{transClip.circleRadius:F2} 总角度:{transClip.circleTotalAngle}°", worldStartPos + Vector3.up * 1.2f);
            if (centerChanged || radiusChanged)
            {
                sceneView.Repaint();
            }
        }

        sceneView.Repaint();
    }
}
