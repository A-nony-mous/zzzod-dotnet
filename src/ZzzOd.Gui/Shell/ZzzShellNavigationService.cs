namespace ZzzOd.Gui.Shell;

public sealed class ZzzShellNavigationService
{
    public event EventHandler<string>? NavigationRequested;

    public void RequestNavigate(string key)
    {
        NavigationRequested?.Invoke(this, key);
    }

    public ZzzShellNavigationTarget Resolve(string key) => key switch
    {
        "settings-resource-download" => new ZzzShellNavigationTarget("settings", "资源下载"),
        _ => new ZzzShellNavigationTarget(key, null),
    };
}

public sealed record ZzzShellNavigationTarget(string RootKey, string? PivotHeader);
