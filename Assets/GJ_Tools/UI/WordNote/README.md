# WordNote 多区域文字通知

`WordNote` 是轻量的多区域文字通知工具。目前保留原有屏幕底部 FIFO 队列，并增加右上角堆叠区域。底部消息逐条上浮显示；右上角消息可以同时存在，最新消息插在最上方，每条消息独立计时，并以左侧滑入、右侧滑出的方式渐显和渐隐。

## 预制体

预制体位于 `Assets/Prefabs/UI/WordNote/WordNote_UI.prefab`。预制体默认跨场景保留，并通过 `CanvasScaler` 与 `Screen.safeArea` 适配分辨率、宽高比和异形屏安全区。

右上角区域需要在预制体中配置 `WordNoteStackRegionUI`：消息容器负责纵向排列，消息预制体使用 `WordNoteItemUI`。消息预制体的外层节点交给布局组件控制，`滑动节点` 应指向内部子节点，避免布局定位与左右滑动动画互相覆盖。

## 静态 API

```csharp
WordNoteHandle handle = WordNote.Show("默认样式文字");
WordNote.Show("显示三秒", 3f);

WordNoteStyle style = new WordNoteStyle(
    font,
    48f,
    Color.yellow,
    true,
    Color.black);

WordNoteHandle styledHandle = WordNote.Show(
    "自定义样式",
    style,
    2f);

styledHandle.Close();
WordNote.Clear();

WordNoteHandle topRightHandle = WordNote.Show(
    WordNoteRegion.TopRight,
    "玩家A <sprite name=\"weapon.ak\"> 玩家B",
    4f);

WordNote.Clear(WordNoteRegion.TopRight);
```

`duration` 大于零时，从该消息完成上浮出现后开始计时；等于零时持续显示，直到关闭句柄或清空队列。关闭当前消息会播放上浮消失，关闭尚未显示的排队消息会直接将其移出队列。

原有 `Show(string)`、`Show(string, float)` 和 `Show(string, WordNoteStyle, float)` 全部继续指向底部队列，因此旧代码与 Timeline 片段不需要改动。带 `WordNoteRegion` 的新重载用于选择区域。

## TMP 图标

`WordNoteStyle` 可以携带 `TMP_SpriteAsset`。使用 `<sprite name="图标名称">` 前，需要先把对应 Sprite Asset 配到默认样式或自定义样式中。业务层应传入可信的稳定图标名称，不要把玩家输入直接拼入 Sprite 标签。

## Timeline

在 Timeline 中添加 `WordNoteTrack`，再添加 `WordNoteClip`。片段长度就是文字显示区间，片段 Inspector 可设置内容、字体文件、字号、字体颜色、是否居中和描边颜色。Timeline 消息与静态 API 消息共用同一条 FIFO 队列；片段结束只关闭自己的句柄，不影响其他消息。
