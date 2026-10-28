using System.Collections.Generic;

public static class HelpTextTools
{
    public static void Fill(Dictionary<System.Type, string[]> map)
    {
        ComponentHelpText.Register(map, typeof(SmartTimer), "SmartTimer · 计时器管理器",
            "【这是什么】\n" +
            "全局计时 / 循环管理器。做「延迟 3 秒执行」「每隔 1 秒触发一次」这类需求时用它，" +
            "不需要自己写协程或维护计数字段。全局单例，首次使用自动创建，切换场景不销毁。\n\n" +
            "【怎么用】\n" +
            "· 延时：SmartTimer.instance.SetTimer(this, 3f, () => 做事());\n" +
            "· 循环：SmartTimer.instance.SetLoop(this, 1f, () => 每个循环干啥);\n" +
            "· 全参数版本还支持 onStart、onTick（带进度 0~1）、maxLoop、pauseOnDisable。\n\n" +
            "【返回值 TimerTask】\n" +
            "可随时 Pause() / Resume() / Stop() / Restart()；" +
            "Progress（0~1）、Remaining（剩余秒数）、GetInfo() 可用于显示进度条或调试。\n\n" +
            "【时间模式 Mode】\n" +
            "· Update：跟随帧时间，最常用；\n" +
            "· FixedUpdate：按固定步长累加，适合和物理同步；\n" +
            "· Realtime：不受 Time.timeScale 影响，暂停菜单里的倒计时用这个。\n\n" +
            "【全局控制】\n" +
            "PauseAll / ResumeAll / StopAll / RestartAll / GetAllTasks。\n\n" +
            "【注意】\n" +
            "owner 参数请传 this —— 物体销毁时它身上的计时会被一并清理，" +
            "否则回调可能在物体已经不存在之后仍被触发。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(SmartWait), "SmartWait · 条件等待管理器",
            "【这是什么】\n" +
            "「等某个条件成立，再去执行一件事」的集中管理器。" +
            "把散落在各处的「Update 里 if 判断 + 布尔标记」收敛到一处，避免状态碎片。\n\n" +
            "【怎么用】\n" +
            "· 单条件：SmartWait.instance.WaitUntil(this, () => hp <= 0, () => Die());\n" +
            "· 多条件：SmartWait.instance.WaitUntilAll(this, 条件A, 条件B).Then(() => 做事());\n\n" +
            "【返回的 WaitTask 还能继续配置】\n" +
            "· Then(回调)：追加完成回调；\n" +
            "· CheckEvery(秒)：降低检测频率，省性能；\n" +
            "· PauseWhenInactive()：物体失活时暂停检测，而不是直接取消；\n" +
            "· Pause() / Resume() / Cancel()：手动控制。\n\n" +
            "【注意】\n" +
            "· owner 请传 this，物体销毁时等待会被清理；\n" +
            "· 条件委托里抛异常时，该等待会被自动取消，异常打印到控制台；\n" +
            "· 若 owner 创建时就是失活状态，未声明 PauseWhenInactive 的等待会被直接取消并给出警告。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(ObjectPool), "ObjectPool · 对象池",
            "【这是什么】\n" +
            "按模板分类的对象池。子弹、特效、飘字这类会反复生成的物体用它，" +
            "避免频繁 Instantiate / Destroy 带来的 GC 抖动。全局单例，自动创建且跨场景保留。\n\n" +
            "【怎么用】\n" +
            "· 取出：ObjectPool.instance.Spawn(模板, 位置, 旋转, 父节点) —— 后三个参数可省略；\n" +
            "  也支持泛型：Spawn<MyComponent>(模板) 直接拿到组件；\n" +
            "· 归还：ObjectPool.instance.Despawn(对象)；\n" +
            "· 预热：ObjectPool.instance.Warmup(模板, 20) —— 开局先建好一批，避免第一次生成卡顿。\n\n" +
            "【自动回收】\n" +
            "池里的物体都带 PooledItem 标记。业务里直接 SetActive(false) 即可，" +
            "池子会在下一帧把它收回并挂回池子节点下，无需手动 Despawn。\n\n" +
            "【结构】\n" +
            "闲置物体会被放在本组件下的 Pools 子节点里（默认隐藏），方便在 Hierarchy 观察。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(EventMgr), "EventMgr · 事件中心",
            "【这是什么】\n" +
            "全局事件中心，配套菜单「Tools → GJ_Tools → 事件生成器」使用。" +
            "它把「谁监听了谁」和「物体销毁时忘记解绑」这两类问题交给框架处理。\n\n" +
            "【怎么加事件】\n" +
            "打开 Tools → GJ_Tools → 事件生成器，填事件名、勾参数类型（也可填自定义类型），" +
            "点生成，它会往 Assets/GJ_Tools/BasicTools/EventMgr.Generated.cs 写好三个成员：\n" +
            "· On事件名（事件本体）\n" +
            "· Broadcast事件名(参数)（广播）\n" +
            "· Bind事件名(owner, 处理函数...)（绑定，支持一次绑多个）\n\n" +
            "【怎么用】\n" +
            "EventMgr.BindRoy(this, OnRoy);   // 听\n" +
            "EventMgr.BroadcastRoy(10);       // 喊\n\n" +
            "【为什么都要传 this】\n" +
            "绑定时会自动在 owner 上挂 EventHook：物体失活自动解绑、激活自动重绑、销毁自动清理，" +
            "并且会拦截重复绑定。\n\n" +
            "【注意】\n" +
            "事件是静态的，进入播放时会自动清空，避免编辑器里残留上一次运行的委托。" +
            "也可以自己写 partial class EventMgr 手动补充事件，效果相同。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(EventHook), "EventHook · 事件挂载点（自动添加）",
            "【这是什么】\n" +
            "由 EventMgr 自动添加到 owner 物体上的辅助组件，" +
            "让「这个物体身上的事件绑定」跟着它的激活 / 失活 / 销毁走。\n\n" +
            "【什么时候会出现】\n" +
            "调用 EventMgr.BindXxx(this, ...) 时自动挂到 this 所在物体上，通常不需要手动添加。\n\n" +
            "【它做了什么】\n" +
            "· 物体失活：把绑定的监听全部摘掉；\n" +
            "· 物体激活：重新挂上；\n" +
            "· 物体销毁：彻底解绑；\n" +
            "· 身上没有绑定时：下一帧自动销毁自己，不留空组件。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(TaskRegistry), "TaskRegistry · 计时任务挂载点（自动添加）",
            "【这是什么】\n" +
            "由 SmartTimer 自动添加到 owner 物体上的辅助组件，" +
            "用来记录「这个物体身上挂着哪些计时任务」。\n\n" +
            "【它做了什么】\n" +
            "· 集中保存该物体上创建的计时任务；\n" +
            "· 物体销毁时统一清理，避免出现「回调还在、物体已经没了」的情况；\n" +
            "· 配合 SmartTimer 的 pauseOnDisable 参数使用。\n\n" +
            "【你需要做什么】\n" +
            "通常什么都不用做：调用 SmartTimer.instance.SetTimer(this, ...) 时它会自动挂上。" +
            "不要手动删除它，否则该物体的计时任务会失去生命周期管理。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");

        ComponentHelpText.Register(map, typeof(WaitHook), "WaitHook · 等待任务挂载点（自动添加）",
            "【这是什么】\n" +
            "由 SmartWait 自动添加到 owner 物体上的辅助组件，" +
            "用来承载这个物体上「等待条件」任务的生命周期。\n\n" +
            "【它做了什么】\n" +
            "· 记录该物体上登记的等待任务；\n" +
            "· 物体失活 / 销毁时配合 SmartWait 把等待暂停或取消；\n" +
            "· 没有任务时自动销毁自己。\n\n" +
            "【你需要做什么】\n" +
            "通常什么都不用做：调用 SmartWait.instance.WaitUntil(this, ...) 时它会自动挂上。\n\n" +
            "（本说明由 AI 编写，可能与最新代码存在出入，请以脚本源码为准。）");
    }
}