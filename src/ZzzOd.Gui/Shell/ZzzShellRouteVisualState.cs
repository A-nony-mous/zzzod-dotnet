using Avalonia;

namespace ZzzOd.Gui.Shell;

internal readonly record struct ZzzShellRouteVisualState(bool IsHomeMode, Thickness ContentMargin)
{
    internal static ZzzShellRouteVisualState ForRoute(string routeKey)
    {
        bool isHomeMode = string.Equals(routeKey, "home", StringComparison.Ordinal);
        return new ZzzShellRouteVisualState(
            isHomeMode,
            isHomeMode ? new Thickness(0) : new Thickness(11, 32, 11, 0));
    }
}

