using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum MoveMode
{
    [InspectorName("瞬移(穿墙)")]
    FixedEndPos,
    [InspectorName("匀速直线")]
    SpeedAndDistance,
    [InspectorName("变速直线")]
    VariableSpeed,
    [InspectorName("绕圈")]
    CircleRotate,
}

public class TransformTimelineClip : PlayableAsset, ITimelineClipAsset
{
    [Header("位移模式")]
    [Tooltip("位移模式，下方参数面板会随模式切换")]
    public MoveMode moveMode;

    [Header("通用参数")]
    [Tooltip("直线移动方向(本地坐标)。自动去掉垂直分量；全零时改用角色面朝方向")]
    public Vector3 direction = Vector3.forward;
    [Tooltip("直线移动撞墙时是否贴墙滑行")]
    public bool useCollision = true;
    [Tooltip("撞墙检测球半径，越大越“胖”，越早碰墙")]
    public float castRadius = 0.5f;

    [Header("固定终点(瞬移,可穿墙)")]
    [Tooltip("进入片段瞬间传送到的终点(本地偏移)，不检测碰撞、可穿墙")]
    public Vector3 endPos;
    [Tooltip("传送完成后锁定的朝向(本地欧拉)")]
    public Vector3 endEuler;

    [Header("速度+距离(匀速直线)")]
    [Tooltip("匀速移动速度(米/秒)")]
    public float moveSpeed;
    [Tooltip("直线总距离，走满即停；0 = 不限制，走到片段结束为止")]
    public float totalDistance;

    [Header("变速直线")]
    [Tooltip("起始速度(0=从静止起步)")]
    public float startSpeed;
    [Tooltip("结束速度，播放中从起始速度线性过渡到此值")]
    public float endSpeed;

    [Header("绕圈旋转")]
    [Tooltip("绕圈圆心(本地偏移，相对进入片段时的位置)")]
    public Vector3 circleCenterLocal;
    [Tooltip("绕圈半径")]
    public float circleRadius;
    [Tooltip("绕圈总角度(度)，360 = 完整一圈")]
    public float circleTotalAngle;
    [Tooltip("旋转方向：勾选 = 俯视顺时针")]
    public bool circleClockwise = true;
    [Tooltip("绕圈是否使用角速度变速插值")]
    public bool circleVariableSpeed;
    [Tooltip("变速绕圈起始角速度(度/秒)")]
    public float circleStartAngSpeed;
    [Tooltip("变速绕圈结束角速度(度/秒)")]
    public float circleEndAngSpeed;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var behaviour = new TransformBehaviour();
        behaviour.clip = this;
        return ScriptPlayable<TransformBehaviour>.Create(graph, behaviour);
    }
}
