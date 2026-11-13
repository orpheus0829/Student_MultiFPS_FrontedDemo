# AudioMgr

项目通用的分层音量与播放入口。

- `Master` 控制全部声音。
- `Music` 独立控制音乐。
- `Sfx` 控制全部音效，其下再分角色语音、战斗音效和反馈音效。
- `SetVolume` 可直接供设置界面的 Slider 调用，数值会写入 `PlayerPrefs` 缓存；暂停或退出时自动落盘，也可在设置界面关闭时调用 `SaveVolumes` 显式保存。
- `Play`、`PlayOneShot`、`Stop` 统一负责 AudioSource 的音量层级。
- `PlayMusicAsync` 使用稳定 Addressable ID 异步播放循环或单次音乐；连续切歌与停止播放会让旧请求失效，并自动释放上一首音乐。
- `StopMusic` 停止并释放当前音乐。
- 业务数据仍由各自 ScriptableObject 保存稳定 Addressable ID，不把所有声音耦合进一个配置资产。

