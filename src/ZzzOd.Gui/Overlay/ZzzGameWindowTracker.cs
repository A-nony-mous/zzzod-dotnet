using Avalonia.Threading;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Overlay;

/// <summary>
/// 按配置间隔读取游戏客户区快照，并且只在窗口状态变化时通知 UI。
/// </summary>
internal sealed class ZzzGameWindowTracker
{
    private readonly IZzzAppBackend _backend;
    private readonly DispatcherTimer _timer;
    private bool _hasPublished;
    private bool _lastRequestSucceeded;
    private ZzzWindowStatusDto? _lastWindow;
    private ZzzBackendResult<ZzzWindowStatusDto>? _lastResult;

    public ZzzGameWindowTracker(IZzzAppBackend backend, int intervalMilliseconds)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(NormalizeInterval(intervalMilliseconds)),
        };
        _timer.Tick += (_, _) => Poll(force: false);
    }

    public event Action<ZzzBackendResult<ZzzWindowStatusDto>, bool>? WindowChanged;

    public void UpdateInterval(int intervalMilliseconds)
    {
        _timer.Interval = TimeSpan.FromMilliseconds(NormalizeInterval(intervalMilliseconds));
    }

    public void Start()
    {
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _hasPublished = false;
        _lastRequestSucceeded = false;
        _lastWindow = null;
        _lastResult = null;
    }

    /// <summary>
    /// 获取共享窗口快照。强制刷新时会先通知订阅方应用同一份快照。
    /// </summary>
    public ZzzBackendResult<ZzzWindowStatusDto> GetSnapshot(bool force)
    {
        return force || !_hasPublished
            ? Poll(force)
            : _lastResult ?? ZzzBackendResult<ZzzWindowStatusDto>.Fail(
                ZzzBackendErrorCode.NotReady,
                "游戏窗口快照尚未可用。");
    }

    private ZzzBackendResult<ZzzWindowStatusDto> Poll(bool force)
    {
        ZzzBackendResult<ZzzWindowStatusDto> result = _backend.GetWindow();
        bool succeeded = result.Success && result.Value is not null;
        ZzzWindowStatusDto? current = succeeded ? result.Value : null;
        if (!force && !HasStateChanged(succeeded, current))
        {
            return result;
        }

        _hasPublished = true;
        _lastRequestSucceeded = succeeded;
        _lastWindow = current;
        _lastResult = result;
        WindowChanged?.Invoke(result, force);
        return result;
    }

    private bool HasStateChanged(bool succeeded, ZzzWindowStatusDto? current)
    {
        if (!_hasPublished || succeeded != _lastRequestSucceeded)
        {
            return true;
        }

        return succeeded && current is not null && ZzzOverlayController.HasWindowStateChanged(_lastWindow, current);
    }

    private static int NormalizeInterval(int intervalMilliseconds) => Math.Max(30, intervalMilliseconds);
}
