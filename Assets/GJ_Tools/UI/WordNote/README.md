# WordNote 底部文字队列

`WordNote` 是固定显示在屏幕下方的轻量文字队列。它只保留 FIFO 排队、单条关闭、清空队列、Timeline 片段和文字样式配置。文字统一使用上浮淡入和继续上浮淡出，不提供其他动效、位置切换、打字机或堆叠显示。

## 预制体

预制体位于 `Assets/Prefabs/UI/WordNote/WordNote_UI.prefab`。`SampleScene` 已放入一个实例；预制体默认跨场景保留，并通过 `CanvasScaler` 与 `Screen.safeArea` 适配分辨率、宽高比和异形屏安全区。

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
```

`duration` 大于零时，从该消息完成上浮出现后开始计时；等于零时持续显示，直到关闭句柄或清空队列。关闭当前消息会播放上浮消失，关闭尚未显示的排队消息会直接将其移出队列。

## Timeline

在 Timeline 中添加 `WordNoteTrack`，再添加 `WordNoteClip`。片段长度就是文字显示区间，片段 Inspector 可设置内容、字体文件、字号、字体颜色、是否居中和描边颜色。Timeline 消息与静态 API 消息共用同一条 FIFO 队列；片段结束只关闭自己的句柄，不影响其他消息。
