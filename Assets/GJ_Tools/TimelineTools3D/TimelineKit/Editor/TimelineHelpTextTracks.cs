using System.Collections.Generic;
using UnityEngine.Timeline;

public static class TimelineHelpTextTracks
{
    public static void Fill(Dictionary<System.Type, string[]> map)
    {

        TimelineHelpText.Register(map, typeof(HitBoxTrack), "HitBoxTrack · 打击判定轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上划一段，就代表「这段时间里存在打击判定」。\n" +
            "和传统做法（代码里定时开关碰撞体）相比，判定窗口跟着时间轴走，改起来更直观。\n\n" +
            "【怎么用】\n" +
            "1) Timeline 里右键 → 添加 HitBoxTrack；\n" +
            "2) 把轨道绑定到一个挂了 TimelineActorHost 的物体（界面上的轨道绑定槽）；\n" +
            "3) 在轨道上创建片段，拖动两端决定判定起止时间；\n" +
            "4) 选中片段，在 Inspector 里配形状、偏移、伤害、击退等参数。\n\n" +
            "【绑定类型】\n" +
            "Transform —— 判定位置以这个物体的朝向为基准换算。\n\n" +
            "【注意】\n" +
            "绑定物体上必须有 TimelineActorHost，否则不会生效，控制台会给出警告。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(HitBoxClip), "HitBoxClip · 打击判定片段",
            "【这是什么】\n" +
            "扣在 HitBoxTrack 上的判定片段。片段本身只描述「判定长什么样」和「哪段时间有效」，" +
            "真正扫描目标的是物体上的 TimelineActorHost。\n\n" +
            "【判定形状 HitBoxShape】\n" +
            "· Sphere 球：用 boxRadius 当半径；\n" +
            "· Box 盒：用 hitBoxSize 当长宽高；\n" +
            "· Sector 扇形：朝角色前方展开，用 sectorAngle 定张角、" +
            "sectorInnerRadius 定内径（>0 就是空心圆弧，贴身处不判定）、sectorHeight 定竖直高度。\n\n" +
            "【位置与朝向】\n" +
            "· boxOffset：判定中心相对角色的本地偏移，随角色位置与朝向换算；\n" +
            "· boxEuler：判定整体相对角色的本地旋转（欧拉角，三轴自由）。扇形中轴沿本地 +Z。\n\n" +
            "【结算参数】\n" +
            "· damage：命中伤害；\n" +
            "· HitForce：命中击退冲量，0 表示不击退。\n\n" +
            "【判定窗口】\n" +
            "· startTime / endTime：相对片段起点的时间窗（秒），只有落在这个区间内才开启判定；\n" +
            "· useRepeatScan：勾上后在窗口内按 scanInterval 反复扫描，" +
            "同一目标只结算一次，只用来补漏新进入范围的目标。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");


        TimelineHelpText.Register(map, typeof(TransformTrack), "TransformTrack · 位移轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上编排物体位移。适合做冲刺、击飞、绕圈这类带演出的移动。\n\n" +
            "【怎么用】\n" +
            "1) 添加 TransformTrack；\n" +
            "2) 绑定要移动的物体（Transform）；\n" +
            "3) 创建片段，在 Inspector 里选位移模式并填参数。\n\n" +
            "【绑定类型】\n" +
            "Transform。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(TransformTimelineClip), "TransformTimelineClip · 位移片段",
            "【这是什么】\n" +
            "扣在 TransformTrack 上的位移片段，支持四种位移模式：\n" +
            "· 瞬移（穿墙）：进入片段瞬间直接传送到目标点，不检测碰撞；\n" +
            "· 匀速直线：按固定速度朝某方向走一段距离；\n" +
            "· 变速直线：速度在片段内从起始值线性过渡到结束值；\n" +
            "· 绕圈：绕一个圆心走一段角度，可做顺时针/逆时针、可变速。\n\n" +
            "【通用参数】\n" +
            "· direction：直线移动方向（本地坐标），会自动去掉垂直分量，留空则用角色面朝方向；\n" +
            "· useCollision：撞墙时是否贴墙滑动（勾上会做碰撞扫掠）；\n" +
            "· castRadius：扫掠检测球半径，越大越「胖」，越早碰到墙。\n\n" +
            "【瞬移模式参数】\n" +
            "· endPos：传送到的终点（本地偏移）；\n" +
            "· endEuler：传送完成后锁定的朝向（本地欧拉角）。\n\n" +
            "【匀速直线参数】\n" +
            "· moveSpeed：速度（米/秒）；\n" +
            "· totalDistance：总距离，走满即停；0 表示不限制，走到片段结束为止。\n\n" +
            "【变速直线参数】\n" +
            "· startSpeed / endSpeed：起始与结束速度，片段内线性过渡。\n\n" +
            "【绕圈参数】\n" +
            "· circleCenterLocal：圆心（相对进入片段时的位置）；\n" +
            "· circleRadius：半径；\n" +
            "· circleTotalAngle：总角度（360 = 完整一圈）；\n" +
            "· circleClockwise：俯视顺时针；\n" +
            "· circleVariableSpeed 与 circleStartAngSpeed / circleEndAngSpeed：角速度是否变速。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");


        TimelineHelpText.Register(map, typeof(CameraTimelineTrack), "CameraTimelineTrack · 相机运镜轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上编排相机运镜。\n\n" +
            "【怎么用】\n" +
            "1) 先把相机准备成「挂了 TimelineCamRig 的相机」；\n" +
            "2) 添加 CameraTimelineTrack 并绑定这台相机；\n" +
            "3) 创建片段，在 Inspector 里选运镜模式、设置目标与时长。\n\n" +
            "【和手动操作的关系】\n" +
            "运镜与 OrbitCameraControl 共用 TimelineCamRig 的参数；" +
            "玩家手动转视角时会把控制权切到玩家一侧，运镜片段结束可再切回。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(CameraTimelineClip), "CameraTimelineClip · 运镜片段",
            "【这是什么】\n" +
            "扣在 CameraTimelineTrack 上的运镜片段。\n\n" +
            "【运镜模式】\n" +
            "· 平滑运镜：从当前机位平滑过渡到目标机位；\n" +
            "· 瞬移运镜：立刻切到目标机位，常用于硬切镜头；\n" +
            "· 归位：把相机恢复到默认（原点）机位。\n\n" +
            "【归位子模式】\n" +
            "归位也分「瞬移」和「平滑」两种过渡方式，逻辑与上面对应。\n\n" +
            "【提示】\n" +
            "片段上还有环绕/变速相关的进阶参数（环绕圆心、速度插值等），" +
            "选中片段后 Inspector 里会全部列出，每个字段都有中文 Tooltip 说明。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");


        TimelineHelpText.Register(map, typeof(EffectAudioTrack), "EffectAudioTrack · 音效特效轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上触发音效和特效。" +
            "用它可以把「打击音、命中火花」这类表现和时间轴对齐，不用在代码里掐时间。\n\n" +
            "【怎么用】\n" +
            "1) 添加 EffectAudioTrack 并绑定到挂了 TimelineActorHost 的物体；\n" +
            "2) 创建片段，在 Inspector 里指定音效、特效模板等参数。\n\n" +
            "【音效与特效从哪来】\n" +
            "由 TimelineActorHost 负责实际播放与回收：音效走它身上的 AudioSource，" +
            "特效走它实现的特效生成 / 回收接口（可接对象池）。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(EffectAudioClip), "EffectAudioClip · 音效特效片段",
            "【这是什么】\n" +
            "扣在 EffectAudioTrack 上的片段，描述「在这个时间点播放什么音效 / 特效」。\n\n" +
            "【常用能力】\n" +
            "· 指定要播放的音效（AudioClip）；\n" +
            "· 指定要生成的特效模板；\n" +
            "· 支持在片段持续时间内重复触发（配合触发间隔参数），" +
            "适合做持续喷射、连击火花这类表现；\n" +
            "· 音调可随机微调（在 TimelineActorHost 上配置范围），避免重复音太机械。\n\n" +
            "【提示】\n" +
            "选中片段后 Inspector 会列出全部字段（含中文 Tooltip）；" +
            "需要更细的表现时，优先在这里调，而不是改代码。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");
    }
}