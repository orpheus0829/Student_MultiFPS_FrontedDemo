using Unity.Pipeline.Commands;

public static class GunDemoWeaponActionCliCommands
{
    private const string Group = "gundemo";

    [CliGroupCommand(
        Group,
        "创建或更新全部枪械动作集，并迁移现有动作时间与补弹节点",
        Name = "configure_weapon_actions",
        Tags = new[] { "gundemo/weapons", "gundemo/data" })]
    public static WeaponActionConfigureReport ConfigureWeaponActions()
    {
        return WeaponActionSetConfigurator.Configure();
    }
}
