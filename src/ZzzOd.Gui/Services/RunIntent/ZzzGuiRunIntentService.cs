namespace ZzzOd.Gui.Services.RunIntent;

using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

public sealed class ZzzGuiRunIntentService
{
    private bool _startOneDragonRequested;
    private object? _runTargetOwner;
    private ZzzGuiRunTarget? _runTarget;

    public event EventHandler<string>? GlobalInputPressed;

    public ZzzGuiRunTarget? CurrentRunTarget => _runTarget;

    public void RegisterRunTarget(object owner, string appId, string? groupId = null, int? instanceIndex = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(appId))
        {
            ClearRunTarget(owner);
            return;
        }

        _runTargetOwner = owner;
        _runTarget = new ZzzGuiRunTarget(appId, groupId, instanceIndex);
    }

    public void ClearRunTarget(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!ReferenceEquals(_runTargetOwner, owner))
        {
            return;
        }

        _runTargetOwner = null;
        _runTarget = null;
    }

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

public sealed record ZzzGuiRunTarget(string AppId, string? GroupId, int? InstanceIndex);

