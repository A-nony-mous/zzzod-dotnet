using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.Gui.Pages;

namespace ZzzOd.Gui.Shell;

public enum ZzzNavigationPlacement
{
    Primary,

    Footer,
}

public sealed record ZzzNavigationEntry(
    string Key,
    string Text,
    string IconGlyph,
    string SelectedIconGlyph,
    string AccessibleName,
    ZzzNavigationPlacement Placement,
    Func<IServiceProvider, Control> CreatePage);

public sealed class ZzzNavigationRegistry
{
    private readonly IReadOnlyList<ZzzNavigationEntry> _entries;

    public ZzzNavigationRegistry()
    {
        bool diagnosticsEnabled = string.Equals(Environment.GetEnvironmentVariable("ZZZOD_GUI_ENABLE_DIAGNOSTICS"), "1", StringComparison.Ordinal);

        List<ZzzNavigationEntry> entries =
        [
            new("home", "仪表盘", "\uE80F", "\uEA8A", "主页仪表盘", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateHomePage()),
            new("game-assistant", "游戏助手", "\uE7FC", "\uEA8A", "游戏助手", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateGameAssistantPage()),
            new("one-dragon", "一条龙", "\uE768", "\uEA8A", "一条龙", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateOneDragonPage()),
            new("standalone", "应用运行", "\uECAA", "\uEA8A", "独立应用", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateStandalonePage()),
            new("devtools", "开发工具", "\uEC7A", "\uEA8A", "开发工具", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateDevtoolsPage()),
            new("accounts", "账户管理", "\uE77B", "\uEA8A", "账户管理", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateAccountsPage()),
            new("settings", "设置", "\uE713", "\uEA8A", "设置选项", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateSettingsPage()),
        ];
        if (diagnosticsEnabled)
        {
            entries.Add(new("diagnostics", "诊断", "\uE9D9", "\uEA8A", "系统诊断", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateDiagnosticsPage()));
        }

        _entries =
            entries;
    }

    public IReadOnlyList<ZzzNavigationEntry> Entries => _entries;

    public ZzzNavigationEntry GetRequired(string key) =>
        _entries.First(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
}

