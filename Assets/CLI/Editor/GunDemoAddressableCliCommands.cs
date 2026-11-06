using Unity.Pipeline.Commands;

public static class GunDemoAddressableCliCommands
{
    private const string Group = "gundemo";

    [CliGroupCommand(
        Group,
        "初始化本地 Addressables 分组并同步正式武器资源 ID",
        Name = "configure_addressables",
        Tags = new[] { "gundemo/addressables" })]
    public static GunDemoAddressableConfigureReport ConfigureAddressables()
    {
        return GunDemoAddressableConfigurator.Configure();
    }
}
