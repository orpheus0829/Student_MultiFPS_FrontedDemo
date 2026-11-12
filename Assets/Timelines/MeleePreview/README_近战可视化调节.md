# 近战可视化调节

## 打开方式

1. 打开 `FP_MeleePreview.unity` 调第一人称。
2. 打开 `TP_MeleePreview.unity` 调第三人称。
3. 打开同名 `.playable`，在 Timeline 窗口拖动时间查看 0.5 倍速挥砍。
4. 只调整场景层级中的 `BLACK_BLADE_ADJUST_HERE` 和 `HIT_EFFECT_ADJUST_HERE`。
5. 调整完成后保存场景。不要点击重建菜单，否则会覆盖已调数据。

## 资源清单

- 第一人称动画：`Assets/Resource/Animations/FirstPerson/Melee/FP_Anby_Attack_Normal_02.anim`
- 第三人称动画：`Assets/Resource/Animations/ThirdPerson/Melee/Anby_Attack_Normal_02.fbx`
- RPG ATK2 目标：安比 Prefab 左手骨下的 `安比武器_L_2`（0.1 秒激活）
- 黑色棍状武器预制体：`Assets/Prefabs/Weapons/Melee/AnbyBlackStick.prefab`
- 黑色棍状武器材质：`Assets/Resource/Weapons/Melee/AnbyBlade/Materials/AnbyBlackStick.mat`
- 命中特效：`Assets/Prefabs/Effects/Melee/MeleeHitBurst.prefab`
- 第一人称 Timeline：`Assets/Timelines/MeleePreview/FP_MeleePreview.playable`
- 第三人称 Timeline：`Assets/Timelines/MeleePreview/TP_MeleePreview.playable`
- 第一人称预览场景：`Assets/Timelines/MeleePreview/FP_MeleePreview.unity`
- 第三人称预览场景：`Assets/Timelines/MeleePreview/TP_MeleePreview.unity`

预览文件由 `GunDemo/近战/生成或重建第一与第三人称 Timeline 预览` 菜单生成。首次编译后若文件不存在也会自动生成。
