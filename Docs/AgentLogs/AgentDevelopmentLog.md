# Agent 开发日志

这份日志只记录值得长期保留的 Agent 协作成果，并按用户明确授权纳入版本库。编译、测试、Console、Git 差异和效果截图等原始证据属于本地临时产物，不纳入版本库。

## 2026-11-05｜战斗反馈与资源寻址基础

### 本次目标

- 建立统一伤害入口，使枪械和未来其他伤害来源能共用命中、击杀、计分与播报流程。
- 完成右上角击杀播报和屏幕得分反馈的第一版可视效果。
- 用资源 ID 取代业务脚本中的直接资源引用，并把现有枪械展示资源纳入 Addressables。
- 补充可由 Agent 调用的项目检查、验证和报告能力。

### 已完成

#### 战斗与玩家信息

- 新增 `CombatDamage` 统一伤害入口，以及 `DamageRequest`、`DamageResult`、`DamageOutcome` 数据结构。
- 新增 `CharacterHealth`、`BodyHitbox` 与 `BodyPart`，由命中部位提供伤害和分数倍率，再交给生命组件结算。
- 新增 `CombatScore`，支持单次命中得分和击杀得分累计。
- 新增 `PlayerInfo` 玩家身份组件；玩家名变化时可同步 GameObject 名称，为击杀播报和后续玩家生成保留统一名字来源。
- 伤害发起者允许为空，便于环境伤害等非玩家来源后续接入。

#### 战斗反馈 UI

- 新增 `CombatFeedbackCtrl`，负责把伤害结算结果分发给得分反馈和击杀播报。
- 新增 `CombatScoreFeedbackUI`：累计分数采用插值变化；单次加分单独显示；持续收到新分数时不重播整套入场；停止得分后统一淡出并归零。
- 新增 `KillFeedUI`：右上角队列显示，最新击杀从顶部插入，支持普通击杀与爆头击杀。
- 扩展 WordNote 为多区域、可堆叠消息结构，并补充消息条目和区域组件。
- 更新击杀骷髅图标、中文字体和击杀播报图文资源；普通击杀使用白色骷髅，爆头击杀使用红色骷髅。
- 调整击杀播报的对齐、图文间距和图标比例，并调整大分数、单次加分、骷髅和玩家名的视觉比例。

#### 场景演示对象

- 在 `example` 场景新增“战斗测试角色”分组，放置 4 个不带输入和相机的简单方块角色。
- 训练兵_阿橙、侦察兵_小林、铁卫_老周、重装兵_大熊的生命上限分别为 80、60、150、250。
- 每个测试角色均配置 `PlayerInfo`、`CharacterHealth`、`BodyHitbox` 和碰撞体；整个方块暂按上半身命中区处理，伤害与得分倍率均为 1。

#### Addressables 资源体系

- 接入 Addressables 包和项目配置。
- 新增 `ResourceMgr`、`ResourceIdAttribute` 及编辑器资源 ID 选择器，让业务字段可以保存资源 ID，而不是直接保存资源引用。
- 枪械数据与目录改为记录展示资源 ID；现有枪械剪影等可用资源已整理进 Addressables 配置。
- 新增 Addressables 配置脚本和 CLI 命令，供后续新增资源时复用。

#### Agent 工作流

- 扩展项目 CLI 的场景、玩家、动画、UI、数据、预制体、资源和综合验证报告。
- 新增 Addressables 与击杀播报相关 CLI 命令。
- 工作流支持 `Quick`、`Normal`、`Full` 三档，并把 Git 状态、差异、编译、测试、Console、验证结果和 Bug 报告写入 `Temp/AgentReports/<时间戳>-<档位>/`。
- 新增 Addressables EditMode 测试、战斗核心 EditMode 测试和战斗反馈 PlayMode 测试。

### 本次整理

- 删除项目内临时效果截图、临时生图脚本和无用捕获产物。
- 清理 TMP 动态字形缓存膨胀，保留正式字体源文件、字体资产和图集。
- 保留正式骷髅图标、击杀播报资源与 Addressables 配置。

### 验证状态

- 后续已启动 Unity 完成导入、场景、Prefab、EditMode、PlayMode 和画面验证。
- Full 工作流最终通过：编译通过，EditMode 17/17，PlayMode 5/5，项目检查、Console 和 Game View 截图步骤均通过。
- 原始报告仅保存在 `Temp/AgentReports`，不纳入提交。

### 人工判断

- 击杀播报、得分反馈和命中准心仍由用户判断最终观感；用户已明确同意本日志纳入提交。

### 对应提交

- 已同意纳入本次提交；提交哈希待提交后补充。

## 2026-11-06｜玩家 HUD 与全量脚本检查

### 已完成

- 新增正式 `PlayerHUD` 预制体、基础十字准心和普通命中/爆头命中反馈，并接入 `CombatFeedbackCtrl`。
- 普通命中使用较短白叉，线宽最终调整为 `2.5`；爆头使用更长红叉，线宽最终调整为 `3.5`。两者均快速伸展、原地淡出，并受统一触发间隔限制。
- 扩展 `gundemo inspect_ui`，检查场景中 HUD 数量、命中反馈组件数量和四条反馈线完整性。
- 对 `Assets/Scripts`、`Assets/GJ_Tools`、`Assets/CLI`、`Assets/Editor`、`Assets/Tests` 共 121 个自有 C# 脚本进行静态检查。
- 修复 `CombatScore`、`CombatFeedbackCtrl`、`PlayerActionCtrl`、`FirstPersonCameraCtrl` 的组件级订阅启停对称性。
- 修复旧 `HitFlash` 的事件泄漏，并改用 `SmartTimer`；修复 `SmartTimer`、`SmartWait` 每帧 `ToArray` 产生临时数组的问题。
- 根据用户明确授权，将本日志整理到正式 `Docs` 目录并纳入本次提交范围；项目 Skill 与临时 Agent 文件仍保持本机排除。

### 检查结论

- 自有运行时代码不存在 `UnityEditor` 依赖、空生命周期方法、逐帧场景查找、直接 Addressables 旁路或正式资源依赖 `source_unselected`。
- 场景和 Prefab 均无缺失脚本、缺失材质或候选资源引用；UI 专属检查无问题。
- 旧 `TimelineKit` 中的 `DamageableHealth` 与 `TimelineActorHost` 仍保留独立 `IDamageable` 伤害路径，不符合 GunDemo 正式玩法的唯一 `CombatDamage` 规则；当前没有任何场景、Prefab 或资产引用这些组件，因此暂不删除或反向耦合业务层，正式对象禁止使用。
- 旧 Timeline 工具仍有公开序列化字段、表达式体等历史风格差异。它们不构成当前运行错误，为避免序列化风险和无意义大 diff，本次不做机械重写。

### 验证状态

- Full 工作流通过：编译通过，EditMode 17/17，PlayMode 5/5，项目检查、Console 和 Game View 截图步骤均通过。
- 证据目录：`Temp/AgentReports/20261106-153918-full/`，不纳入提交。
- 最终粗细调整后又通过一次 Quick 工作流；首次运行因 Unity 刷新期间 Pipeline 瞬时返回 400，等待后同档回归通过。
- 准心预览已经移到项目外的会话可视化目录，只在会话中展示，不纳入项目或提交。
- 项目检查仍报告 3 条非阻塞动画警告：玩家根、第一人称表现、第三人称表现尚未配置 Animator Controller；这是此前明确暂不填写实际动画逻辑的预留状态。

### 提交范围

- 本日志已获用户同意纳入本次提交。
- `Temp/AgentReports`、效果截图、项目 Skill 和其他本机 Agent 辅助文件仍不纳入提交。
- 提交哈希待提交后补充。
