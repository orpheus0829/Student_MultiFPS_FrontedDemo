# BasicTools · 基础工具 使用手册

> ⚠️ **重要声明**：本手册、以及工程内所有组件的「组件说明」栏，**全部由 AI 编写**。
> 它们能覆盖大部分常见用法，但**不保证与最新代码完全一致**，也可能遗漏边界情况或描述有偏差。
> 请把本文当作**上手导读**，**遇到疑问一律以脚本源码为准**；发现不对的地方直接改文案即可。

---

## 一、这是什么

一套 GameJam 用的基础小工具，解决的问题是：

| 需求 | 对应工具 |
|---|---|
| 延迟 / 循环执行 | **SmartTimer** |
| 等条件成立再执行 | **SmartWait** |
| 模块之间传消息、解耦 | **EventMgr** + 事件生成器 |
| 反复生成子弹 / 特效 | **ObjectPool** |
| 写全局管理器 | **BaseMgr&lt;T&gt;** |

**设计基调**：尽量少写代码就能用起来；凡是需要写代码的地方，都尽量做到「一行接入」。

---

## 二、快速开始

1. **看组件说明**：选中任意组件，Inspector 最上方就是「组件说明」栏（默认收起，点开可读）
2. **加事件**：菜单 `Tools → GJ_Tools → 事件生成器` → 填事件名、勾参数类型 → 生成
3. **写计时**：`SmartTimer.instance.SetTimer(this, 3f, () => 做事());`
4. **等条件**：`SmartWait.instance.WaitUntil(this, () => hp <= 0, () => Die());`
5. **加对象池**：`ObjectPool.instance.Spawn(模板, 位置, 旋转);` 回收直接 `SetActive(false)`

---

## 三、目录结构

```
BasicTools/
├── BaseMgr.cs                  管理器单例基类（抽象，不能挂物体）
├── SmartTimer.cs               计时 / 循环管理器
├── SmartWait.cs                条件等待管理器
├── EventMgr.cs                 事件中心（partial，配套生成文件）
├── EventMgr.Generated.cs       事件生成器的输出（自动生成，别手改）
├── EventHook.cs                事件挂载点（自动添加）
├── ObjectPool.cs               对象池
├── TaskRegistry.cs             计时任务挂载点（自动添加）
├── WaitHook.cs                 等待任务挂载点（自动添加）
└── Editor/
    ├── EventGeneratorWindow.cs     事件生成器窗口
    ├── ComponentHelp.cs            组件说明栏的绘制 + 兜底 Inspector
    ├── ComponentHelpText.cs        说明文案注册表
    ├── HelpTextTools.cs            基础工具各组件的中文说明
    └── HelpTextExtra.cs            按类型名登记说明的落点
```

---

## 四、各组件详解

### 4.1 BaseMgr&lt;T&gt; · 管理器单例基类

```csharp
public class MyMgr : BaseMgr<MyMgr>
{
    protected override bool Ddol
    {
        get
        {
            return true;
        }
    }
}

MyMgr.instance.DoSomething();
```

- `instance` 是**按需创建**的：第一次访问时自动 new 一个物体
- `Ddol` 为 true 时切换场景不销毁（默认 false）

### 4.2 SmartTimer · 计时器

```csharp
SmartTimer.instance.SetTimer(this, 3f, () => Debug.Log("3 秒到了"));

SmartTimer.instance.SetLoop(this, 1f, () => 每秒干一次);
SmartTimer.instance.SetLoop(this, 1f, OnTick, maxLoop: 5);
```

| 要点 | 说明 |
|---|---|
| `owner` | 传 `this`。物体销毁时，它身上的计时会被一并清理 |
| `Mode` | `Update`（默认）/ `FixedUpdate` / `Realtime`（不受 `timeScale` 影响） |
| 返回值 | `TimerTask`，可 `Pause / Resume / Stop / Restart`，读 `Progress / Remaining / GetInfo()` |
| 全局操作 | `PauseAll / ResumeAll / StopAll / RestartAll / GetAllTasks()` |

### 4.3 SmartWait · 条件等待

```csharp
SmartWait.instance.WaitUntil(this, () => hp <= 0, () => Die());

SmartWait.instance.WaitUntilAll(this, 条件A, 条件B)
    .CheckEvery(0.2f)
    .Then(() => 做事());

var w = SmartWait.instance.WaitUntil(this, 条件, 回调);
w.Cancel();
```

- 条件委托抛异常 → 该等待自动取消，异常打印到 Console
- `PauseWhenInactive()`：物体失活时暂停检测而不是取消

### 4.4 ObjectPool · 对象池

```csharp
var go = ObjectPool.instance.Spawn(prefab);
var go2 = ObjectPool.instance.Spawn(prefab, 位置, 旋转, 父节点);
var comp = ObjectPool.instance.Spawn<MyComponent>(prefab);
ObjectPool.instance.Despawn(go);
ObjectPool.instance.Warmup(prefab, 20);
```

**自动回收**：池中物体带 `PooledItem` 标记，业务里直接 `SetActive(false)` 即可，下一帧自动收回。

### 4.5 EventMgr · 事件中心

菜单 **`Tools → GJ_Tools → 事件生成器`**：填事件名（写 `OnPlayerDamaged` 会自动去掉 `On`）→ 勾参数 → 生成。
生成器会往 `EventMgr.Generated.cs` 追加三个成员：

```csharp
public static Action<float> OnPlayerDamaged;
public static void BroadcastPlayerDamaged(float a) { ... }
public static void BindPlayerDamaged(MonoBehaviour owner, params Action<float>[] handlers) { ... }
```

用法：

```csharp
EventMgr.BindPlayerDamaged(this, OnPlayerDamaged);
EventMgr.BroadcastPlayerDamaged(12.5f);
```

**为什么都要传 `this`**：绑定时会自动在 owner 上挂 `EventHook` —— 物体失活自动解绑、激活自动重绑、销毁自动清理，并拦截重复绑定。

生成文件的位置**不写死**：每次刷新会全工程搜索 `EventMgr.Generated.cs`；找不到才在 `BasicTools/EventMgr.Generated.cs` 新建，窗口顶部会显示当前操作的路径。若遇到「事件不见了」，先看窗口顶上的路径、再搜 `#region Event:` 确认事件写在哪。

### 4.6 EventHook / TaskRegistry / WaitHook

框架自动添加的挂载点，平时不用管：

| 组件 | 谁添加 | 作用 |
|---|---|---|
| `EventHook` | `EventMgr.BindXxx(this, ...)` 时 | 让事件绑定跟着物体的激活/失活/销毁走 |
| `TaskRegistry` | `SmartTimer.SetTimer(this, ...)` 时 | 集中管理该物体上的计时任务 |
| `WaitHook` | `SmartWait.WaitUntil(this, ...)` 时 | 同上，管理等待任务 |

---

## 五、编辑器工具

| 菜单位置 | 作用 |
|---|---|
| `Tools → GJ_Tools → 事件生成器` | 可视化增删事件，自动写进生成文件 |

### 组件说明栏

选中**任意**组件，Inspector 最上方会有一个：

```
▶ 组件说明 · <组件名> · <一句话定位>
```

- **默认收起**，点标题展开；内容是**只读**的，不影响字段编辑
- 文案集中在 `Editor/HelpTextTools.cs` 里；新增文案也可以写在 `Editor/HelpTextExtra.cs`：

```csharp
ComponentHelpText.Register(map, "类型名", "标题", "正文");
```

- 查找顺序：**精确类型 → 按名字 → 泛型定义 → 沿基类向上**，所以派生类会自动继承祖先的说明
- 实现：`ComponentHelp.cs` 里一个覆盖所有 MonoBehaviour 的兜底 Inspector（没有自定义面板的组件自动带上说明栏）

---

## 六、常见问题与坑

1. **计时 / 等待 / 事件绑定，`owner` 一律传 `this`** —— 不传就不会跟着物体清理，容易出「回调还在、物体没了」的问题
2. **事件是静态的**：进入播放时会自动清空，避免编辑器残留上一次运行的委托；这是预期行为
3. **改脚本请保持 UTF-8 无 BOM**：用脚本批量改 `.cs` 时务必显式按 UTF-8 读写，否则中文会变乱码
4. **`AssetDatabase` 的导入时序**：一次并行写入多个新脚本时，偶发某些文件没进编译列表（表现为「类型不存在 / 菜单不出现」）。遇到时**换个文件名重写一次**即可

---

> 再次提醒：**本文与工程内所有说明文字均由 AI 生成**，仅供参考，**请以源码为准**。
