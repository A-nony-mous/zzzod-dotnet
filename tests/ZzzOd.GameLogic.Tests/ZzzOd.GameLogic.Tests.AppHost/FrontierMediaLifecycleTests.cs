using ZzzOd.Gui.Shell;
using Xunit;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class FrontierMediaLifecycleTests
{
    [Fact]
    public void RouteChanges_PlayOnlyOnHomeAndPauseOutsideHome()
    {
        ZzzFrontierMediaLifecycle lifecycle = new();

        lifecycle.OnRouteChanged("game-assistant");
        Assert.False(lifecycle.ShouldPlay);

        lifecycle.OnRouteChanged("home");
        Assert.True(lifecycle.ShouldPlay);

        lifecycle.OnRouteChanged("settings");
        Assert.False(lifecycle.ShouldPlay);
    }

    [Fact]
    public void Release_StopsPlaybackAndPreventsLaterRoutePlayback()
    {
        ZzzFrontierMediaLifecycle lifecycle = new();
        lifecycle.OnRouteChanged("home");

        lifecycle.Release();
        lifecycle.OnRouteChanged("home");

        Assert.True(lifecycle.IsReleased);
        Assert.False(lifecycle.ShouldPlay);
    }
}
