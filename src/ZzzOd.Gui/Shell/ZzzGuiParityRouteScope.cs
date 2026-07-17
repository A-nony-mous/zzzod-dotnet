namespace ZzzOd.Gui.Shell;

public static class ZzzGuiParityRouteScope
{
    public static IReadOnlyList<string> ProductPrimaryNavigationKeys { get; } =
    [
        "home",
        "game-assistant",
        "one-dragon",
        "standalone",
    ];

    public static IReadOnlyList<string> ProductFooterNavigationKeys { get; } =
    [
        "devtools",
        "accounts",
        "settings",
    ];

    public static IReadOnlyList<string> ProductNavigationKeys { get; } =
        ProductPrimaryNavigationKeys.Concat(ProductFooterNavigationKeys).ToArray();

    public static IReadOnlyList<string> ApprovedParityRouteKeys { get; } =
    [
        "home",
        "game-assistant-battle",
        "game-assistant-commission",
        "one-dragon-run",
        "one-dragon-charge-plan",
        "one-dragon-predefined-team",
        "one-dragon-sensitivity",
        "standalone-run",
        "accounts",
        "settings-game",
        "settings-overlay",
        "settings-resource-download",
        "settings-env",
        "settings-push",
        "settings-custom",
        "devtools-image-analysis",
        "devtools-template-helper",
        "devtools-screen-manage",
        "devtools-agent-template",
        "devtools-screenshot-helper",
        "devtools-operation-debug",
    ];

    public static IReadOnlyList<string> ApprovedSettingsTabs { get; } =
    [
        "游戏设置",
        "Overlay",
        "资源下载",
        "脚本环境",
        "通知设置",
        "自定义设置",
    ];

    public static IReadOnlyList<string> ExcludedParityRouteKeys { get; } =
    [
        "like",
        "code-sync",
        "pip",
        "settings-api",
        "settings-app-config",
        "diagnostics",
    ];
}
