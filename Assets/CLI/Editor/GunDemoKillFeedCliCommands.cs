using Unity.Pipeline.Commands;

public static class GunDemoKillFeedCliCommands
{
    private const string Group = "gundemo";

    [CliGroupCommand(
        Group,
        "重新收集正式武器剪影和击杀图标，生成 TMP 击杀播报图集",
        Name = "rebuild_killfeed_icons",
        Tags = new[] { "gundemo/ui", "gundemo/resources" })]
    public static KillFeedSpriteBuildReport RebuildKillFeedIcons()
    {
        return KillFeedSpriteAssetBuilder.Rebuild();
    }
}
