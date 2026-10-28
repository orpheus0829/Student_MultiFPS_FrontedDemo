using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;

public class HitBoxClip : PlayableAsset
{
    [Header("攻击盒参数")]
    [Tooltip("判定形状：球(半径)/盒(长宽高)/扇形(朝角色前方展开)")]
    public HitBoxShape hitBoxShape;
    [Tooltip("判定中心相对角色的本地偏移(随角色位置与朝向换算)")]
    public Vector3 boxOffset;
    [Tooltip("球半径 或 扇形半径")]
    public float boxRadius;
    [Tooltip("盒形判定的尺寸(长宽高)")]
    public Vector3 hitBoxSize;
    [FormerlySerializedAs("boxYaw")]
    [Tooltip("判定整体相对角色的本地旋转(欧拉角)，三轴自由。扇形中轴沿本地+Z")]
    public Vector3 boxEuler;
    [Tooltip("扇形张角(度)，以中轴为基准左右展开")]
    public float sectorAngle = 90f;
    [Tooltip("扇形内径(米)。0=实心扇形柱；>0=空心圆弧刃(贴身内圈不判定)")]
    public float sectorInnerRadius = 0f;
    [Tooltip("扇形柱竖直总高(米)，以判定中心为中间上下均分")]
    public float sectorHeight = 2f;
    [Tooltip("命中伤害值")]
    public float damage;
    [Tooltip("命中击退冲量(0 = 不击退)")]
    public float HitForce;
    [Tooltip("判定窗口开始时间(相对片段起点，秒)")]
    public float startTime;
    [Tooltip("判定窗口结束时间(相对片段起点，秒)")]
    public float endTime;

    [Header("重复判定设置")]
    [Tooltip("开启后判定窗口内按间隔反复扫描(同一目标只结算一次，仅补漏新目标)")]
    public bool useRepeatScan;
    [Tooltip("反复扫描的间隔(秒)")]
    public float scanInterval = 0.1f;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var b = new HitBoxBehaviour();
        b.clip = this;
        return ScriptPlayable<HitBoxBehaviour>.Create(graph, b);
    }
}
