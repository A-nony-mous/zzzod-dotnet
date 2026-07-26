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
    ZzzNavigationPlacement Placement);

public sealed class ZzzNavigationRegistry
{
    private readonly IReadOnlyList<ZzzNavigationEntry> _entries;

    public ZzzNavigationRegistry()
    {
        _entries =
        [
            new("home", "仪表盘", "\uE80F", "\uEA8A", "主页仪表盘", ZzzNavigationPlacement.Primary),
            new("game-assistant", "游戏助手", "\uE7FC", "\uE7FC", "游戏助手", ZzzNavigationPlacement.Primary),
            new("one-dragon", "一条龙", "\uE768", "\uE768", "一条龙", ZzzNavigationPlacement.Primary),
            new("standalone", "应用运行", "\uECAA", "\uECAA", "独立应用", ZzzNavigationPlacement.Primary),
            new("devtools", "开发工具", "\uEC7A", "\uEC7A", "开发工具", ZzzNavigationPlacement.Footer),
            new("accounts", "账户管理", "\uE77B", "\uEA8C", "账户管理", ZzzNavigationPlacement.Footer),
            new("settings", "设置", "\uE713", "\uE713", "设置选项", ZzzNavigationPlacement.Footer),
        ];
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
