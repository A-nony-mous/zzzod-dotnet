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
            new("game-assistant", "游戏助手", "\uE7FC", "\uE7FC", "游戏助手", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateGameAssistantPage()),
            new("one-dragon", "一条龙", "\uE768", "\uE768", "一条龙", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateOneDragonPage()),
            new("standalone", "应用运行", "\uECAA", "\uECAA", "独立应用", ZzzNavigationPlacement.Primary, sp => sp.GetRequiredService<ZzzPageFactory>().CreateStandalonePage()),
            new("devtools", "开发工具", "\uEC7A", "\uEC7A", "开发工具", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateDevtoolsPage()),
            new("accounts", "账户管理", "\uE77B", "\uEA8C", "账户管理", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateAccountsPage()),
            new("settings", "设置", "\uE713", "\uE713", "设置选项", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateSettingsPage()),
        ];
        if (diagnosticsEnabled)
        {
            entries.Add(new("diagnostics", "诊断", "\uE9D9", "\uE9D9", "系统诊断", ZzzNavigationPlacement.Footer, sp => sp.GetRequiredService<ZzzPageFactory>().CreateDiagnosticsPage()));
        }

        _entries =
            entries;
    }

    internal ZzzNavigationRegistry(IEnumerable<ZzzNavigationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ZzzNavigationEntry[] materialized = entries.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("导航注册至少需要一个真实路由。", nameof(entries));
        }

        string[] duplicateKeys = materialized
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw new ArgumentException($"导航路由重复: {string.Join(", ", duplicateKeys)}。", nameof(entries));
        }

        _entries = materialized;
    }

    public IReadOnlyList<ZzzNavigationEntry> Entries => _entries;

    public ZzzNavigationEntry GetRequired(string key) =>
        _entries.First(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
}

