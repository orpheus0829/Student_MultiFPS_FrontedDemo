using UnityEditor;
using Unity.Pipeline.Commands;
using UnityEngine;

public static class GunDemoMatchCliCommands
{
    [CliGroupCommand(
        "gundemo",
        "生成或同步内置对局模式、炸弹与包点正式资源",
        Name = "configure_match_content",
        Tags = new[] { "gundemo/match" })]
    public static MatchContentConfigureReport ConfigureMatchContent()
    {
        return MatchContentConfigurator.Configure();
    }

    [CliGroupCommand(
        "gundemo",
        "在 Play Mode 中以当前场景角色启动爆破模式预览",
        Name = "start_bomb_preview",
        Tags = new[] { "gundemo/match", "gundemo/preview" })]
    public static bool StartBombPreview()
    {
        ModeDefinitionSO mode = AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(
            MatchContentConfigurator.BombModePath);
        return MatchDebugWindow.StartConfiguredMatch(mode);
    }

    [CliGroupCommand(
        "gundemo",
        "在爆破预览购买期打开本地玩家商城",
        Name = "open_bomb_shop_preview",
        Tags = new[] { "gundemo/match", "gundemo/preview" })]
    public static bool OpenBombShopPreview()
    {
        BombEconomyPlayerCtrl[] controllers = Object.FindObjectsByType<BombEconomyPlayerCtrl>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].TryOpenShop())
            {
                return true;
            }
        }

        return false;
    }
}
