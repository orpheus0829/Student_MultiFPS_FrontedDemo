using UnityEngine;

public enum WeaponActionType
{
    [InspectorName("无")] None = 0,
    [InspectorName("开火")] Fire = 1,
    [InspectorName("普通换弹")] Reload = 2,
    [InspectorName("空仓换弹")] EmptyReload = 3,
    [InspectorName("逐发装填开始")] PerRoundStart = 4,
    [InspectorName("装入一发")] PerRoundInsert = 5,
    [InspectorName("逐发装填结束")] PerRoundEnd = 6,
    [InspectorName("抽出枪械")] Equip = 7,
    [InspectorName("收起枪械")] Holster = 8,
    [InspectorName("检视枪械")] Inspect = 9
}

public enum WeaponActionMarkerType
{
    [InspectorName("实际击发")] FireShot = 0,
    [InspectorName("弹匣补弹生效")] AmmoCommit = 1,
    [InspectorName("装入一发")] InsertRound = 2,
    [InspectorName("机械循环完成")] CycleComplete = 3
}

public enum WeaponActionCancelReason
{
    [InspectorName("主动取消")] Manual = 0,
    [InspectorName("进入翻越")] Vault = 1,
    [InspectorName("切换枪械")] WeaponChanged = 2,
    [InspectorName("组件停用")] Disabled = 3
}

public static class WeaponActionStateContract
{
    public const string Fire = "Fire";
    public const string Reload = "Reload_Tac";
    public const string EmptyReload = "Reload_Empty";
    public const string PerRoundStart = "Reload_Start";
    public const string PerRoundInsert = "Reload_Loop";
    public const string PerRoundEnd = "Reload_End";
    public const string Equip = "Equip_Override";
    public const string Holster = "Holster";
    public const string Inspect = "Inspect";

    public static string GetStateName(WeaponActionType actionType)
    {
        switch (actionType)
        {
            case WeaponActionType.Fire:
                return Fire;
            case WeaponActionType.Reload:
                return Reload;
            case WeaponActionType.EmptyReload:
                return EmptyReload;
            case WeaponActionType.PerRoundStart:
                return PerRoundStart;
            case WeaponActionType.PerRoundInsert:
                return PerRoundInsert;
            case WeaponActionType.PerRoundEnd:
                return PerRoundEnd;
            case WeaponActionType.Equip:
                return Equip;
            case WeaponActionType.Holster:
                return Holster;
            case WeaponActionType.Inspect:
                return Inspect;
            default:
                return string.Empty;
        }
    }
}
