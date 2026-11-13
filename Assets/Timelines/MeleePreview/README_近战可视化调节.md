# 近战可视化调节

## 打开方式

1. 打开 `FP_MeleePreview.unity` 调第一人称。
2. 打开 `TP_MeleePreview.unity` 调第三人称。
3. 选中场景根节点上的 `PlayableDirector`，打开 Timeline 窗口拖动时间预览。
4. 调整完成后保存场景。不要点击重建菜单，否则会覆盖已调数据。

## 第一人称怎么调

- 第一人称手部与枪械已经全部隐藏，只保留屏幕前的刀。
- 只调整 `01_挥砍起点刀_调整位置旋转大小`。
- 只调整 `02_挥砍终点刀_调整位置旋转大小`。
- `03_弧线运动预览刀_不要手调` 是默认运动轨迹参考，不要直接修改。
- Timeline 中的 `伤害API响应点_拖动此片段` 表示运行时调用
  `PlayerMeleeCombatCtrl.ResolveImpactNow()` 的时间点，可以直接左右拖动。
- 起点与终点确认后，运行时会根据两端姿态、力度和过冲参数重新计算弧线，
  不会直接把两把参考刀同时显示给玩家。

## 第三人称怎么调

- Timeline 会以 0.5 倍速播放第三人称身体挥砍动作。
- 只调整 `棍子绑定调整点_调整位置旋转大小`。
- `第三人称棍子_资源实例_不要单独调整` 只是资源实例，不要直接改它。
- 调整节点挂在左手骨下，因此位置、旋转和大小全部是相对手部的本地值。

## 资源清单

- 第三人称动画：`Assets/Resource/Animations/ThirdPerson/Melee/Anby_Attack_Normal_02.fbx`
- RPG ATK2 目标：安比 Prefab 左手骨下的 `安比武器_L_2`（0.1 秒激活）
- 黑色棍状武器预制体：`Assets/Prefabs/Weapons/Melee/AnbyBlackStick.prefab`
- 黑色棍状武器材质：`Assets/Resource/Weapons/Melee/AnbyBlade/Materials/AnbyBlackStick.mat`
- 命中特效：`Assets/Prefabs/Effects/Melee/MeleeHitBurst.prefab`
- 第一人称 Timeline：`Assets/Timelines/MeleePreview/FP_MeleePreview.playable`
- 第三人称 Timeline：`Assets/Timelines/MeleePreview/TP_MeleePreview.playable`
- 第一人称预览场景：`Assets/Timelines/MeleePreview/FP_MeleePreview.unity`
- 第三人称预览场景：`Assets/Timelines/MeleePreview/TP_MeleePreview.unity`

预览文件由 `GunDemo/近战/生成或重建第一与第三人称 Timeline 预览` 菜单生成。
菜单会覆盖手动校准结果，仅在明确要重置时使用。
