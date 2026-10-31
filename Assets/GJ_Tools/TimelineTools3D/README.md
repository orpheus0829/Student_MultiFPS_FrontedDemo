# TimelineTools3D · Timeline 套件 使用手册

## 一、这是什么

用 Timeline 编排「打击 / 位移 / 运镜 / 音效特效」的一套小工具。

| 需求 | 对应工具 |
|---|---|
| 时间轴上划判定窗口、打人 | **HitBoxTrack** + **HitBoxClip** + **TimelineActorHost** |
| 冲刺 / 击飞 / 绕圈等位移 | **TransformTrack** + **TransformTimelineClip** |
| 运镜（平滑 / 瞬切 / 归位 / 环绕） | **CameraTimelineTrack** + **CameraTimelineClip** + **TimelineCamRig** |
| 按时间点播声音、出特效 | **EffectAudioTrack** + **EffectAudioClip** |
| 在屏幕下方显示排队文字 | **WordNoteTrack** + **WordNoteClip** |
| 血量、受击反馈 | **DamageableHealth** + **HitFlash** |
| 玩家鼠标环绕视角 | **OrbitCameraControl** |

---

## 二、快速开始（做一次攻击）

1. 给角色挂 **`TimelineActorHost`**（它会自动带上 `PlayableDirector`）
2. 选中角色 → `Window → Sequencing → Timeline` → 新建 / 指定一个 TimelineAsset
3. 在 Timeline 里右键加轨：`HitBoxTrack`、`TransformTrack`、`EffectAudioTrack`、`CameraTimelineTrack`
4. 划片段、在片段 Inspector 里配参数；选中片段后可在 **Scene 视图直接拖**判定盒 / 扇形柱 / 位移终点 / 相机机位
5. Play：时间轴走到判定窗口 → `TimelineActorHost` 扫描目标 → `DamageableHealth` 扣血 → `HitFlash` 闪色

---

## 三、目录结构

```
AboutTimeline/                 自定义轨道与片段
├── Track/     HitBoxTrack / TransformTrack / CameraTimelineTrack / EffectAudioTrack / WordNoteTrack
├── Clip/      HitBoxClip / TransformTimelineClip / CameraTimelineClip / EffectAudioClip / WordNoteClip
├── Behavior/  片段对应的运行时行为
├── AboutTimelineHosts.cs       接口定义（ITimelineHitHost / ITimelineEffectHost / IDamageable）
└── Editor/    片段 Inspector、Scene 可视化、样式

TimelineKit/                   与 Timeline 配合的运行时组件
├── Actor/     TimelineActorHost.cs（打击判定 / 音效特效的执行者）
├── Combat/    DamageableHealth.cs（血量）/ HitFlash.cs（受击闪色）
├── Camera/    TimelineCamRig.cs（机位数据）/ OrbitCameraControl.cs（玩家环绕）
└── Editor/    上述组件的 Inspector + 本套件自带的说明栏
```

---

## 四、五条轨道

在 Timeline 里右键 → 添加轨道 就能看到它们：

| 轨道 | 片段 | 作用 |
|---|---|---|
| **HitBoxTrack** | `HitBoxClip` | 打击判定：划一段 = 这段时间有判定（球 / 盒 / 扇形柱） |
| **TransformTrack** | `TransformTimelineClip` | 位移：瞬移穿墙 / 匀速直线 / 变速直线 / 绕圈 |
| **CameraTimelineTrack** | `CameraTimelineClip` | 运镜：平滑 / 瞬切 / 归位（可环绕、可手动接管） |
| **EffectAudioTrack** | `EffectAudioClip` | 在时间轴上触发音效与特效 |
| **WordNoteTrack** | `WordNoteClip` | 在片段持续期间申请一条固定于屏幕下方、统一上浮出现和上浮消失的文字消息 |

**打击判定的完整链路**：

```
Timeline 上的 HitBoxTrack（划判定窗口）
        ↓ 绑定物体
TimelineActorHost（真正去扫描目标、结算伤害、放音效特效）
        ↓ 打到
DamageableHealth（扣血）→ HitFlash（闪色）→ onDeath（死亡事件）
```

### HitBoxClip 关键参数

- **形状**：球（`boxRadius`）/ 盒（`hitBoxSize`）/ 扇形柱（`sectorAngle` 张角、`sectorInnerRadius` 内径、`sectorHeight` 柱高）
  - 内径为 0 = 实心扇形柱；大于 0 = 空心圆弧刃（贴身内圈不判定）
- **位置朝向**：`boxOffset`（本地偏移）、`boxEuler`（本地旋转，三轴自由；扇形中轴沿本地 +Z）
- **结算**：`damage` 伤害、`HitForce` 击退冲量
- **窗口**：`startTime` / `endTime`（相对片段起点，秒）
- **重复扫描**：`useRepeatScan` + `scanInterval`（同一目标只结算一次，用于补漏新进入的目标）

### Scene 视图里的操作

- **W** 移动判定中心 / 位移起点终点 · **E** 拖三向旋转环（盒与扇形）· **R** 缩放
- 端点手柄可以改：扇形外半径 / 内径 / 张角 / 柱高
- 选中效果片段可以拖动**特效预览本体**（移动 / 旋转 / 缩放）

---

## 五、运行时组件

### 5.1 TimelineActorHost · 时间轴执行者

实现 `ITimelineHitHost`（`SetHitBox` / `ClearHitBox` / `DoHitScan`）与 `ITimelineEffectHost`（`PlaySound` / `SpawnEffect` / `RecycleEffect`）。

| 字段 | 说明 |
|---|---|
| `attackPoint` | 判定发射锚点（留空用自身位置） |
| `targetMask` | 可命中层（默认所有层，一般不用碰；见下一行） |
| `ignoreTags` | 忽略的标签 |
| `useHitForce` | 命中后是否给目标刚体击退 |
| `autoExcludeSelf` | 自动排除自身及子物体碰撞体，避免自伤 |
| `ignoreNonDamageable` | **默认开**：自动跳过墙壁、地面等没有伤害能力的碰撞体，所以不用挑命中层 |
| `sfxSource` | 音效播放用的 AudioSource（留空自动查找/创建） |
| `randomizePitch` / `pitchRange` | 每次播放随机微调音调，避免重复音太机械 |
| 特效池 | **全自动**：工程里有 BasicTools 的 `ObjectPool` 就自动用它；没有就每次 `Instantiate/Destroy`，无需配置 |
| 事件 | `onHitTarget` / `onHitWindowChanged` / `onHitDataChanged` |

### 5.2 DamageableHealth · 血量

```csharp
var hp = GetComponent<DamageableHealth>();
hp.TakeDamage(10f);
hp.Heal(5f);
hp.Kill();
hp.ResetHp();

hp.onDamaged += (伤害, 剩余) => { };
hp.onDeath   += () => { };
```

实现了 `IDamageable`，所以 Timeline 的打击判定轨道可以直接打到它。

### 5.3 HitFlash · 受击闪色

- 挂在**同一物体**：组件带 `RequireComponent`，会**自动带上 `DamageableHealth`**，自动接住受击事件，无需写代码
- `duration` 闪多久、`flashColor` 闪什么颜色
- 自动识别材质用的是 `_BaseColor`（URP）还是 `_Color`（内置管线）
- 注意：运行时改的是 `material` 实例，物体多时会有材质副本开销

### 5.4 相机

- **TimelineCamRig**：挂在相机上的「机位数据载体」，被相机轨道驱动；玩家的手动环绕也基于它
- **OrbitCameraControl**：玩家用鼠标环绕观察（同物体上需要 `TimelineCamRig`，组件带 `RequireComponent` 会自动添加）
  - `requireMouseButton` 勾上则需按住右键才转；`lookSpeedX/Y`、`invertY`、`scrollSpeed`、`rotXClamp`、`distanceRange`
  - ⚠️ 它使用 Unity 的**新 Input System**；工程未启用时该组件不参与编译，其余功能不受影响
- 运镜片段的两个开关：「结束后恢复原机位」决定演完是跳回还是停在结束机位；「运镜中允许手动拖动」让玩家拖动时临时接管、松手后继续

---

## 六、编辑器增强

- **片段 Inspector 全中文**，并按模式条件显示参数（球只显示半径、扇形显示半径/内径/张角/柱高/旋转……）
- **Scene 可视化**：判定球 / 盒 / 扇形柱、位移终点与绕圈、相机机位、特效预览，都能直接拖
- **组件说明栏**：选中组件 / 轨道 / 片段时，Inspector 顶部有可折叠的中文说明

说明文案的位置：

| 文件 | 放什么 |
|---|---|
| `TimelineKit/Editor/TimelineHelpTextTracks.cs` | 五条轨道与五个片段 |
| `TimelineKit/Editor/TimelineHelpTextActors.cs` | ActorHost / 血量 / 闪色 / 机位 / 环绕 |
| `TimelineKit/Editor/TimelineHelpTextExtra.cs` | 想给任意类型（含第三方组件）加说明时写这里 |

```csharp
TimelineHelpText.Register(map, "类型名", "标题", "正文");
```

---

## 七、常见问题与坑

1. **打不到人**：先确认轨道绑定的物体上有 `TimelineActorHost`（否则控制台会给警告）；再看 `ignoreTags` 是否忽略了它；目标本身若没有 `DamageableHealth`／`IDamageable`，默认会被跳过（可关掉 `ignoreNonDamageable`）
2. **特效池**：全自动 —— 工程里有 BasicTools 的 `ObjectPool` 就用池，没有就 `Instantiate/Destroy`，**不需要任何配置**
3. **运镜交接**：片段里的「结束后恢复原机位」不勾选 = 停在结束机位；「以上一帧位置为起点」用于多段运镜衔接
4. **Input System**：`OrbitCameraControl` 依赖新输入系统；工程未启用时它自动不参与编译
5. **改脚本请保持 UTF-8 无 BOM**：否则中文会变乱码
6. **`AssetDatabase` 的导入时序**：一次并行写入多个新脚本时，偶发某些文件没进编译列表（表现为「类型不存在 / 菜单不出现」）。遇到时**换个文件名重写一次**即可
