using System.Collections.Generic;

public static class TimelineHelpTextActors
{
    public static void Fill(Dictionary<System.Type, string[]> map)
    {
        TimelineHelpText.Register(map, typeof(DamageableHealth), "DamageableHealth · 可受伤血量",
            "【这是什么】\n" +
            "最基础的血量组件。挂在敌人 / 可破坏物上，负责保存血量、处理扣血与回血、广播死亡。" +
            "它实现了 IDamageable 接口，所以 Timeline 的打击判定轨道可以直接打到它。\n\n" +
            "【字段】\n" +
            "· maxHp：生命上限，同时也是 Heal / ResetHp 的封顶值。\n\n" +
            "【常用 API】\n" +
            "· TakeDamage(数值)：扣血，扣到 0 触发 onDeath；\n" +
            "· Heal(数值)：回血，不会超过 maxHp；\n" +
            "· Kill()：直接致死；\n" +
            "· ResetHp()：恢复满血；\n" +
            "· Hp / IsDead：只读状态查询。\n\n" +
            "【事件】\n" +
            "· onDamaged(本次伤害, 剩余血量)：受伤和回血都会触发（回血时伤害值为负数）；\n" +
            "· onDeath：血量降到 0 时触发一次。\n\n" +
            "【注意】\n" +
            "组件在 Awake 时若发现血量 ≤ 0，会用 maxHp 重置一次，" +
            "所以只在 Inspector 里改 maxHp、留空 _hp 也是安全的。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(HitFlash), "HitFlash · 受击闪色",
            "【这是什么】\n" +
            "挨打时让模型整体闪一下颜色的反馈组件。它会自动订阅同一物体上的 DamageableHealth，" +
            "不需要你写任何代码。\n\n" +
            "【前置条件】\n" +
            "同一物体上必须有 DamageableHealth，否则本组件不会生效（Start 时若取不到就直接返回）。\n\n" +
            "【字段】\n" +
            "· duration：闪色持续时长（秒），受击瞬间切到闪色，到点恢复原色；\n" +
            "· flashColor：命中瞬间的闪色，默认偏红。\n\n" +
            "【实现细节】\n" +
            "· 会收集所有子物体的 Renderer（含未激活的），逐个改材质颜色；\n" +
            "· 自动识别材质使用的是 _BaseColor（URP）还是 _Color（内置管线）；\n" +
            "· 闪色期间重复受击不会打断，等本次闪完再响应下一次。\n\n" +
            "【注意】\n" +
            "运行时改的是 material 实例（不是 sharedMaterial），所以每个物体都会产生材质副本，" +
            "数量多时请注意内存。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(TimelineActorHost), "TimelineActorHost · 时间轴执行者",
            "【这是什么】\n" +
            "Timeline 打击判定的「执行者」。HitBox 轨道只负责在时间轴上划判定窗口，" +
            "真正去扫描目标、扣血、放音效和特效的，是这个组件。\n\n" +
            "【前置条件】\n" +
            "同一物体上必须有 PlayableDirector（组件上带 RequireComponent，会自动添加）。\n" +
            "Timeline 里的 HitBox 轨道要绑定到这个物体的 Transform 上。\n\n" +
            "【字段】\n" +
            "· attackPoint：判定发射锚点，命中盒从这里往外扫；留空则用角色自身位置；\n" +
            "· targetMask：可命中的物理层，默认全部层 —— 强烈建议改为只勾敌人层，避免误伤；\n" +
            "· ignoreTags：忽略的标签列表（例如友军、自身所属标签）；\n" +
            "· useHitForce：命中后是否沿水平方向击退目标刚体；\n" +
            "· autoExcludeSelf：自动排除自身及所有子物体上的碰撞体，避免自伤（建议保持勾选）；\n" +
            "· randomizePitch / pitchRange：播放音效时随机微调音调，避免重复触发太刺耳；\n" +
            "· sfxSource：放音效用的 AudioSource，留空会自动查找或创建。\n\n" +
            "【可监听的事件】\n" +
            "· onHitWindowChanged(bool)：判定窗口开启 / 关闭；\n" +
            "· onHitTarget(HitInfo)：命中了某个目标；\n" +
            "· onHitDataChanged(TimelineHitData)：判定参数被 Timeline 刷新。\n\n" +
            "【接口】\n" +
            "实现 ITimelineHitHost（SetHitBox / ClearHitBox / DoHitScan）与 " +
            "ITimelineEffectHost（PlaySound / SpawnEffect / RecycleEffect），" +
            "这两个接口是给自定义轨道行为调用的，业务代码一般不用直接碰。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        TimelineHelpText.Register(map, typeof(TimelineCamRig), "TimelineCamRig · 相机机位承载",
            "【这是什么】\n" +
            "相机的「机位数据载体」。Timeline 的相机轨道通过它来驱动相机，" +
            "玩家的手动环绕（OrbitCameraControl）也建立在它之上。\n\n" +
            "【怎么配合使用】\n" +
            "1) 把本组件挂在相机物体上；\n" +
            "2) 在 Timeline 里添加 CameraTimelineTrack，绑定这台相机；\n" +
            "3) 想让玩家自己转视角，再挂一个 OrbitCameraControl（它会自动要求本组件存在）。\n\n" +
            "【它会做什么】\n" +
            "· 保存目标点、距离、水平/垂直角度、观察高度等机位参数；\n" +
            "· 根据这些参数计算相机应该待在的位置（OrbitPosition）与朝向；\n" +
            "· 提供把当前相机状态反算回参数的接口，便于从手动操作平滑切回运镜。\n\n" +
            "【注意】\n" +
            "运镜和玩家手动操作会争用同一套参数，代码里用 userDriving 之类的标记做区分，" +
            "改这块逻辑时请一并考虑两种来源。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

#if ENABLE_INPUT_SYSTEM
        TimelineHelpText.Register(map, typeof(OrbitCameraControl), "OrbitCameraControl · 玩家环绕视角",
            "【这是什么】\n" +
            "让玩家用鼠标环绕观察目标的相机控制。它作用在同一个物体上的 TimelineCamRig 参数上，" +
            "因此运镜和手动操作可以自然衔接。\n\n" +
            "【前置条件】\n" +
            "同一物体上必须有 TimelineCamRig（组件带 RequireComponent，会自动添加）。\n" +
            "输入使用新 Input System（Mouse.current）。\n\n" +
            "【字段】\n" +
            "· requireMouseButton：勾选后需要按住鼠标右键才旋转，不勾则鼠标移动即转；\n" +
            "· lookSpeedX / lookSpeedY：水平 / 垂直旋转灵敏度；\n" +
            "· invertY：垂直方向反转；\n" +
            "· scrollSpeed：滚轮缩放速度；\n" +
            "· rotXClamp：俯仰角限制（度）；\n" +
            "· distanceRange：相机与目标的距离范围。\n\n" +
            "【工作方式】\n" +
            "在 Update 里读取鼠标位移与滚轮，累加到 TimelineCamRig 的角度和距离上；" +
            "若当前正在播放机位动画，则把标记切到「玩家接管」，由本组件直接摆放相机位置与朝向。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");
#endif
    }
}