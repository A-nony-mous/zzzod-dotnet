namespace ZzzOd.Gui.Services.RunIntent;

using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

public sealed class ZzzGuiRunIntentService
{
    private bool _startOneDragonRequested;

    public event EventHandler<string>? GlobalInputPressed;

    public void RequestStartOneDragon()
    {
        _startOneDragonRequested = true;
    }

    public bool ConsumeStartOneDragon()
    {
        if (!_startOneDragonRequested)
        {
            return false;
        }

        _startOneDragonRequested = false;
        return true;
    }

    internal void PublishGlobalInputPressed(string key)
    {
        ScreenshotHelperGlobalInputSource.Publish(key);
        GlobalInputPressed?.Invoke(this, key);
    }
}

