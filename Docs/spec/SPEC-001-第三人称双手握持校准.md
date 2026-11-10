# SPEC-001：四角色 × 七把可用枪的第三人称双手握持校准

> 状态：已批准，待实现
> 作者：蓝图（规格）/ 落盘：Chief
> 关联待办：交接第 1 项（第三人称左右手近景与明确抓握点）

## 0. 本次规格的由来

实拍证据（`Temp/AgentReports/LiveGrip/`）确认了三个问题，其中第 3 条是原待办未覆盖的新问题：

1. **安比左手**（AK、第三人称 Idle）：左手落在弹匣井前缘、护木下方，四指基本伸直侧向贴护木，拇指未绕枪身，未形成握合。
2. **Belle 左掌**：掌心朝上/朝外，拇指竖指向上，四指伸直指向前方，整只手在护木下方"托空气"，完全没形成抓握（倒掌）。
3. **右手也未握住枪**（Belle 与安比一致）：掌心朝上，四指伸开贴在弹匣井侧面，枪体悬在手掌上方/后方，手枪握把不在手里。**这是枪的挂点相对手掌偏前偏上，不是手指未握紧。**

远景下枪抱在胸前，问题被掩盖，因此旧测试长期通过。

---

## 一、范围与现状基线

- 可选角色仅限：`character.anbi`、`character.ellen`、`character.zhuyuan`、`character.belle`。
- 当前玩家枪栏仅限：AK、AS VAL、M1911、Kar98k、MPS5、KXG12、PDW90。
- 注意：工程内文件名是 **`MPS5.prefab`**，不是 `MP5.prefab`。
- 不恢复雅，不删除任何备份资源，不处理枪栏外的十二把枪。
- 沿用现有 `WeaponIkData.LeftHandIkPointPath`，不得另建一套左手抓握点字段。
- 不修改购买动画关键帧，不新增角色名分支，不再把第一人称手骨位置作为七把正式枪的权威抓握数据。
- 保留当前全部在途改动；禁止清理、重置、覆盖、暂存、提交或推送。

---

## 二、抓握点数据契约

文件：`Assets/Scripts/Weapons/WeaponPresentationData.cs`

1. 保留字段：

```csharp
[SerializeField] private string _leftHandIkPointPath;
public string LeftHandIkPointPath { get; }
```

2. `LeftHandIkPointPath` 的语义固定为：相对"第三人称武器实例根 Transform"的路径，不是相对角色、右手或 Renderer。
3. 七把正式枪统一填写：

```text
ThirdPersonGrip/LeftHand
```

4. 七个武器 Prefab 均新增层级：

```text
<WeaponRoot>
└── ThirdPersonGrip
    └── LeftHand
```

5. `LeftHand` 的位置是 `HumanBodyBones.LeftHand` 腕骨目标点；不得把指尖、掌心或 Renderer 最近点当目标。
6. 锚点应挂在随护木/握把运动的最近武器节点下；当前武器没有独立运动护木时挂在 `ThirdPersonGrip` 根下。
7. 路径为空：仅允许枪栏外旧枪使用现有第一人称 Idle 第 0 帧位置回退；回退只提供位置，`HasRotation=false`，腕朝向继续完全由动画负责。
8. 路径非空但找不到：视为配置错误，禁止静默回退；本次握持关闭并只记录一次包含枪名和路径的错误。
9. 七把玩家枪栏武器路径为空或解析失败，自动测试必须失败。

---

## 三、锚点坐标轴约定（权威定义）

`ThirdPersonGrip/LeftHand` 节点的局部坐标系，必须与现有 `GetHandFrame` 完全同构：

- **`+Z`（指尖/手掌朝前方向）**：

```text
+Z = Normalize(LeftMiddleProximal.position - LeftHand.position)
```

即腕骨指向中指近节的方向。

- **`+Y`（掌面法线）**：

```text
+Y = Normalize(Cross(+Z, LeftIndexProximal.position - LeftLittleProximal.position))
```

即现有 `GetHandFrame` 定义的掌面法线。

- **`+X`**：

```text
+X = Cross(+Y, +Z)
```

- 三轴必须是右手正交系，写入前做正交归一化。
- 该坐标系是 `ClampWristCorrection` 拆 twist/swing 的唯一依据：绕 `+Z` 的是 twist，其余是 swing。**约定一旦与实现不一致，整块返工。**

---

## 四、解析与回退 API

新增文件：`Assets/Scripts/Weapons/Runtime/ThirdPersonGripTargetResolver.cs`

```csharp
public enum ThirdPersonGripTargetSource
{
    None = 0,
    ExplicitAnchor = 1,
    LegacyFirstPersonIdle = 2
}

public readonly struct ThirdPersonGripTarget
{
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public bool HasRotation { get; }
    public ThirdPersonGripTargetSource Source { get; }

    public ThirdPersonGripTarget(
        Vector3 position,
        Quaternion rotation,
        bool hasRotation,
        ThirdPersonGripTargetSource source)
    {
        Position = position;
        Rotation = rotation;
        HasRotation = hasRotation;
        Source = source;
    }
}

public static class ThirdPersonGripTargetResolver
{
    public static bool TryResolve(
        WeaponIkData ikData,
        Transform sourceWeapon,
        Transform targetWeapon,
        Transform sourceHand,
        out ThirdPersonGripTarget target);
}
```

分支规则：

- `ikData`、`targetWeapon` 始终必需，任一为空：返回 `false`，`target=default`。
- `sourceWeapon`、`sourceHand` **只在空路径回退分支必需**；显式锚点分支不要求它们，不得因第一人称对象缺失而阻断显式锚点。
- `LeftHandIkPointPath` 非空：
  - 只调用一次 `targetWeapon.Find(path)`，不得逐帧查找。
  - 找到后返回锚点世界位置、世界旋转、`HasRotation=true`、`ExplicitAnchor`。
  - 找不到返回 `false`，不得进入旧回退。
- 路径为空：
  - 若 `sourceWeapon` 或 `sourceHand` 为空，返回 `false`，`target=default`。
  - `localPosition = sourceWeapon.InverseTransformPoint(sourceHand.position)`。
  - `Position = targetWeapon.TransformPoint(localPosition)`。
  - `Rotation=Quaternion.identity`、`HasRotation=false`、`LegacyFirstPersonIdle`。
- `PlayerThirdPersonGripCtrl` 在换枪或第三人称武器实例变化时重新解析并缓存。

---

## 五、左腕 IK 朝向

文件：`Assets/Scripts/Player/Animation/PlayerThirdPersonGripCtrl.cs`

1. 空路径回退保持现状：`rotWeight=0f`，仅修正位置。
2. 显式锚点不得直接使用 `rotWeight=1` 把腕骨硬掰到目标。
3. 显式锚点每帧先保存 Animator 输出的 `animatedHandRotation`，再计算：

```csharp
targetHandRotation =
    gripTarget.Rotation * Quaternion.Inverse(_handFrame);
correction =
    targetHandRotation * Quaternion.Inverse(animatedHandRotation);
```

4. 新增：

```csharp
private const float MaxWristSwingDegrees = 35f;
private const float MaxWristTwistDegrees = 25f;

private static Quaternion ClampWristCorrection(
    Quaternion correction,
    Vector3 twistAxisWorld);
```

5. `ClampWristCorrection` 将四元数拆成：
   - twist：绕动画掌框 `+Z`。
   - swing：其余摆动。
   - twist 限制为 `25°`，swing 限制为 `35°`，再按 `swing * twist` 合成。
6. KTwoBoneIK：
   - `posWeight = _gripWeight`。
   - 显式锚点 `rotWeight = _gripWeight`，目标旋转使用经过限制后的结果。
   - 旧回退 `rotWeight = 0f`。
7. 不新增安比、Belle 或其他角色判断。
8. 下列状态由程序握持：
   - `CurrentAction == null`，包括待机与疾跑。
   - `CurrentAction.ActionType == Fire`。
9. 下列状态完全由原动画接管，左腕 IK 和程序化指骨均不写入：
   - `Reload`、`EmptyReload`、`PerRoundStart`、`PerRoundInsert`、`PerRoundEnd`
   - `Equip`、`Holster`、`Inspect`
   - 死亡、Animator 禁用、武器缺失、锚点配置错误。
10. 切枪完成后最多两个 `WaitForEndOfFrame` 内必须重新解析新枪锚点；不得沿用上一把枪缓存。

---

## 六、手指姿势最小实现

本轮明确实现程序化指骨，不留"视情况而定"。

文件：`Assets/Scripts/Weapons/WeaponPresentationData.cs`

新增数据类型（符号级冻结，不得自行改名）：

```csharp
[Serializable]
public struct HumanoidFingerData
{
    [SerializeField, Range(-1f, 1f)] private float _proximal;
    [SerializeField, Range(-1f, 1f)] private float _intermediate;
    [SerializeField, Range(-1f, 1f)] private float _distal;
    [SerializeField, Range(-1f, 1f)] private float _spread;

    public float Proximal { get { return _proximal; } }
    public float Intermediate { get { return _intermediate; } }
    public float Distal { get { return _distal; } }
    public float Spread { get { return _spread; } }
}

[Serializable]
public class HumanoidFingerPoseData
{
    [SerializeField] private HumanoidFingerData _thumb;
    [SerializeField] private HumanoidFingerData _index;
    [SerializeField] private HumanoidFingerData _middle;
    [SerializeField] private HumanoidFingerData _ring;
    [SerializeField] private HumanoidFingerData _little;

    public HumanoidFingerData Thumb { get { return _thumb; } }
    public HumanoidFingerData Index { get { return _index; } }
    public HumanoidFingerData Middle { get { return _middle; } }
    public HumanoidFingerData Ring { get { return _ring; } }
    public HumanoidFingerData Little { get { return _little; } }
}

[Serializable]
public class ThirdPersonFingerPoseData
{
    [SerializeField] private bool _useLeftGripPose;
    [SerializeField] private HumanoidFingerPoseData _leftGripPose =
        new HumanoidFingerPoseData();
    [SerializeField] private bool _useRightGripPose;
    [SerializeField] private HumanoidFingerPoseData _rightIdlePose =
        new HumanoidFingerPoseData();
    [SerializeField] private HumanoidFingerPoseData _rightFirePose =
        new HumanoidFingerPoseData();

    public bool UseLeftGripPose { get { return _useLeftGripPose; } }
    public HumanoidFingerPoseData LeftGripPose { get { return _leftGripPose; } }
    public bool UseRightGripPose { get { return _useRightGripPose; } }
    public HumanoidFingerPoseData RightIdlePose { get { return _rightIdlePose; } }
    public HumanoidFingerPoseData RightFirePose { get { return _rightFirePose; } }
}
```

`WeaponIkData` 新增（符号级冻结）：

```csharp
[SerializeField] private ThirdPersonFingerPoseData _thirdPersonFingerPose =
    new ThirdPersonFingerPoseData();

public ThirdPersonFingerPoseData ThirdPersonFingerPose
{
    get
    {
        return _thirdPersonFingerPose;
    }
}
```

实现规则：

- 使用 Humanoid muscle，不使用各 FBX 不统一的指骨局部欧拉轴。
- 目标 muscle 范围固定为 `[-1, 1]`；Inspector 明示负值趋向握合、正值趋向伸展。
- 支持左右手拇指、食指、中指、无名指、小指的三段弯曲与 Spread。
- `CacheBody(Animator animator)` 时创建并缓存 `HumanPoseHandler`；角色切换时销毁旧 Handler 并重建。
- 用 `HumanTrait.MuscleName` 一次性解析左右手指骨 muscle 索引（每手二十个，左右手共四十个），不得在 `LateUpdate` 按字符串搜索。
- `LateUpdate` 顺序固定：Animator 已求值 → 写入本帧指骨 muscle → 解左臂 IK。
- 待机、疾跑使用 `_leftGripPose` 与 `_rightIdlePose`。
- Fire 使用 `_leftGripPose` 与 `_rightFirePose`。
- Reload、Inspect、Equip、Holster、逐发装填、死亡全部不写 muscle，由动画接管。
- 任一可选角色缺少所需 Humanoid 指骨映射时，禁用该手程序姿势、记录一次错误，并使验证失败。
- 左手必须形成护木/前握把握合；右手中指、无名指、小指包住握把，拇指保持自然，食指待机不得穿过扳机护圈，Fire 姿势允许向扳机收紧。
- 不修改任何动画资产或 Avatar 映射。

**针对实拍发现的"右手枪体悬在手掌上方/后方"**：右手抓握偏移（`useThirdPersonGrip/Position/Rotation/Scale` 与 `PlayerWeaponViewCtrl` 的 hand-frame 矫正）必须逐角色复核，使握把落入掌心；这不是指骨问题，禁止只靠调手指掩盖。

---

## 七、Gizmo

新增文件：`Assets/Scripts/Weapons/Runtime/WeaponThirdPersonGripGizmo.cs`

- 组件仅挂在七个 `ThirdPersonGrip/LeftHand` 上。
- `OnDrawGizmos` 绘制（与第三节坐标系严格同构）：
  - 腕点球：半径 `0.015m`。
  - XYZ 轴：各长 `0.08m`，X 红、Y 绿、Z 蓝。
  - 掌面矩形：以锚点为中心，位于局部 **XZ** 平面，宽 `0.09m`（沿 X）、长 `0.12m`（沿 Z），法线严格为 `+Y`；四角用 `TransformPoint(±0.045m, 0, ±0.06m)` 连接。
- 不引用 `UnityEditor`，不在运行时查找任何对象。
- Gizmo 只用于编辑校准，不参与运行时求解。

---

## 八、必须手调与允许回退

必须逐枪手调 Prefab 锚点、SO 路径、左右手指姿势：

- `Assets/Prefabs/Weapons/AK.prefab`
- `Assets/Prefabs/Weapons/ASVal.prefab`
- `Assets/Prefabs/Weapons/M1911.prefab`
- `Assets/Prefabs/Weapons/Kar98k.prefab`
- `Assets/Prefabs/Weapons/MPS5.prefab`
- `Assets/Prefabs/Weapons/KXG12.prefab`
- `Assets/Prefabs/Weapons/PDW90.prefab`
- 对应七个 `Assets/SO/Weapons/Data/*.asset`

AK 必须重新摆放显式锚点，不能把"已贴枪"当作合格；重点解决左掌位于机匣下方和握合不自然。

枪栏外其余 WeaponCatalog 枪保持 `LeftHandIkPointPath` 为空，使用旧位置回退且不启用程序化指骨。本规格不要求调它们；日后加入 `Player.prefab` 枪栏前必须补齐显式锚点与手型。

---

## 九、射击抽搐问题（诊断结论，本轮不实施）

**状态：仅记录诊断，禁止据此实施。修法另立 SPEC-002。**

第三条实测与诊断（鲸码，只读）：

- 第三人称 `UpperBodyWeapon.Fire` 挂 `Rifle_ShootOnce`，实长 0.8s / 30fps / 非循环，状态 speed=1。
- AK Fire 动作实长 0.09677s（620 RPM），每发重入同一 Fire 状态，按 CrossFade 语义从 0 重播，实际只播到片段 12.1%。
- 右手腕 t=0 与 t=0.0968 差 7.4°，重播叠加 0.05s 混合约 87–148°/s；片段自身前 0.0968s 腕速峰值 141.7°/s，与实采 204°/s 同量级。
- 枪的 `A_W_AKX_Fire` 是 0.100s，基本匹配射速；枪刚性挂右手，手抽则枪抽。
- 右手无任何代码写入，Rigidbody/Collider 可排除。

**结论**：每发把 0.8s 的单发片段从头重播，是抽搐主因；层权重每发开关是次要可疑项。

**待 SPEC-002 冻结的约束（先记下，本轮不动手）**：

- 必须保持 620 RPM，**不许靠降射速或延长片段遮掩**。
- 不许修改动画资产本身（COD_Feel 硬约束）。
- 只能在播放层修正，具体方案由 SPEC-002 冻结后再实施。

**本轮明确不做**：不在本规格内实现抽搐修复，鲸码不得自行选方案。

---

## 九之二、右手握持对齐（本轮明确裁决）

**裁决：不改 `PlayerWeaponViewCtrl` 的 hand-frame 算法，不做角色特例。**

现有每枪一套 `_thirdPersonGripPosition/_thirdPersonGripRotation/_thirdPersonGripScale` 与 hand-frame 矫正保留。本轮只做数据校准：

- 鲸码负责：确认 `WeaponIkData` 中上述三组右手参数可被 Inspector 正常编辑与保存（不改算法、不加角色分支、不加新字段）。
- 浪码负责：在实例上逐枪调整这三组右手参数的实际数值，使握把落入掌心；七把枪各一套，**四名角色共用同一套**（不按角色区分）。
- 验收：以画面为准，四角色 × 七枪待机姿态下，握把位于右掌掌心范围内，枪不浮在掌上方或后方。
- 若逐枪调参后仍无法让四角色同时成立（骨架差异导致），停手报 Chief 另立规格，**不得擅自加角色特例**。

---

## 十、量化验收

新增：`Assets/Tests/PlayMode/PlayerThirdPersonGripPlayModeTests.cs`

矩阵：四角色 × 七枪 × 待机/疾跑/开火/切枪。

稳定握持帧必须同时满足：

- 左腕位置误差：

```text
Distance(LeftHand.position, anchor.position) <= 0.015m
```

- 掌框前向误差：

```text
Angle(actualPalmForward, anchor.forward) <= 12°
```

- 绕锚点前向的腕扭转：

```text
abs(SignedAngle(
    ProjectOnPlane(actualPalmUp, anchor.forward),
    anchor.up,
    anchor.forward)) <= 15°
```

- 翻掌保护：

```text
Dot(actualPalmUp, anchor.up) >= 0.90
```

- 相对 Animator 原腕姿的程序修正：
  - swing 不得超过 `35° + 0.5°`。
  - twist 不得超过 `25° + 0.5°`。
- 程序指骨启用且权重为 1 时，每个目标 finger muscle 与实际值误差 `<=0.05`。
- Reload/Inspect/Holster/Equip 等动画接管帧，调用控制器前后：
  - 左腕位置变化 `<=0.001m`。
  - 左腕旋转变化 `<=1°`。
  - 指骨 muscle 变化 `<=0.01`。
- 切枪结束两个渲染帧内：
  - TargetSource 必须为 `ExplicitAnchor`。
  - 锚点必须属于新枪实例。
  - 重新满足全部位置、朝向与指骨阈值。

现有 `PlayerCharacterModelPlayModeTests.cs` 中 `0.18m/0.20m` Renderer 距离断言不得继续作为握持合格判据；可保留为粗略防脱枪检查，但正式裁决必须使用上述锚点、掌框和 twist 指标。

---

## 十一、画面验收

- 每个角色、每把枪分别保留待机、疾跑、开火、切枪的左右手近景。
- 文件名包含角色 ID、枪 ID、状态、左右手。
- 安比重点看左手与 AK；Belle 重点看左掌和右手食指/后三指。
- 自动测试通过只代表几何与 muscle 指标合格；没有逐项近景人工验收前，不得宣称第三人称握枪已完全修好。
- 实现属于跨角色、动画、Prefab、SO 联动，必须跑 Full；报告证据放 `Temp/AgentReports/<时间戳>-full/`。

---

## 十二、分工（Chief 已裁决）

### 鲸码（核心逻辑）

- `Assets/Scripts/Weapons/WeaponPresentationData.cs`（只结构，不含握持数值）
- 新增 `Assets/Scripts/Weapons/Runtime/ThirdPersonGripTargetResolver.cs`
- 新增 `Assets/Tests/PlayMode/PlayerThirdPersonGripPlayModeTests.cs`
- `Assets/Tests/PlayMode/PlayerCharacterModelPlayModeTests.cs` 中旧判据替换
- 确认 `WeaponIkData` 右手三组参数可正常编辑保存（**不改 `PlayerWeaponViewCtrl` 算法、不加角色分支**）
- 负责数据结构、路径解析、回退规则、量化验收数值。
- **本轮不做**：射击抽搐修复（见第九节，留给 SPEC-002）。

### 浪码（表现外围）

- `Assets/Scripts/Player/Animation/PlayerThirdPersonGripCtrl.cs`
- 新增 `Assets/Scripts/Weapons/Runtime/WeaponThirdPersonGripGizmo.cs`
- 七个武器 Prefab 的锚点实际摆放。
- `Assets/Scripts/Weapons/Editor/WeaponDefinitionSOEditor.cs`（展示路径与手型字段）
- 七份 `Assets/SO/Weapons/Data/*.asset` 独占填写（含右手三组参数的实际数值）
- 腕朝向限制、IK 权重、HumanPoseHandler 指骨实现、近景证据。
- 不改 `PlayerWeaponViewCtrl`。

### 共享文件

- `Assets/Prefabs/Characters/Player.prefab`：本规格原则上无需修改；若实现确需新增序列化参数，必须先报 Chief 指定唯一所有者。
