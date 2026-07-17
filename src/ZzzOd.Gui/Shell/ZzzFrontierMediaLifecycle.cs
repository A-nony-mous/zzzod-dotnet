namespace ZzzOd.Gui.Shell;

internal sealed class ZzzFrontierMediaLifecycle
{
    internal bool ShouldPlay { get; private set; }

    internal bool IsReleased { get; private set; }

    internal void OnRouteChanged(string routeKey)
    {
        if (IsReleased)
        {
            return;
        }

        ShouldPlay = string.Equals(routeKey, "home", StringComparison.Ordinal);
    }

    internal void Release()
    {
        ShouldPlay = false;
        IsReleased = true;
    }
}
