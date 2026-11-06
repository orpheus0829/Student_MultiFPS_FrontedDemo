# Addressables 资源入口

这组工具为业务代码提供统一的字符串资源 ID 调用入口，业务层不需要持有 Addressables Handle。

## 运行时 API

```csharp
Sprite icon = await ResourceMgr.instance.LoadAsync<Sprite>("weapon.ak.silhouette");
ResourceMgr.instance.Release<Sprite>("weapon.ak.silhouette");

GameObject weapon = await ResourceMgr.instance.InstantiateAsync(
    "weapon.ak.prefab.first_person",
    weaponRoot);
ResourceMgr.instance.ReleaseInstance(weapon);
```

`ResourceMgr` 还提供：

- `InitializeAsync`：显式初始化。
- `PreloadLabelAsync` / `ReleaseLabel`：按 Label 预载和释放。
- `LoadSceneAsync` / `UnloadSceneAsync`：加载和卸载场景。
- `TryGetLoaded`：查询已经完成加载的资源。

相同 ID 与相同类型的并发加载共用一次 Addressables 请求。每次成功调用 `LoadAsync` 都应对应一次 `Release`；实例必须使用 `ReleaseInstance`，不能按普通资源释放。

## 编辑器选择

字符串字段添加 `[ResourceId(typeof(资源类型))]` 后，Inspector 会显示中文资源选择器。选择器支持搜索和类型过滤，字段最终只保存字符串 ID，不产生新的 Unity 对象引用。

GunDemo 的具体分组、登记和旧数据迁移由项目 Editor 服务与 `gundemo configure_addressables` CLI 命令负责，不属于这组通用运行时工具。
