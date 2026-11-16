using Unity.Pipeline.Commands;

public static class GunDemoWeaponPresentationCliCommands
{
    private const string Group = "gundemo";

    [CliGroupCommand(
        Group,
        "按老枪模板校准新增枪第三人称握持、枪口和左轮换弹",
        Name = "configure_weapon_presentation",
        Tags = new[] { "gundemo/weapons", "gundemo/animation" })]
    public static WeaponPresentationConfigureReport ConfigureWeaponPresentation()
    {
        return WeaponPresentationConfigurator.Configure();
    }
}
