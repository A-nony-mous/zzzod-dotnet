using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayController
{
    private readonly IZzzOverlayService _overlayService;
    private readonly IZzzAppBackend _backend;
    private readonly DispatcherTimer _refreshTimer;
    private ZzzOverlayTechnicalWindow? _window;

    public ZzzOverlayController(IZzzOverlayService overlayService, IZzzAppBackend backend)
    {
        _overlayService = overlayService;
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = backend.GetConfigScope("overlay");
        if (!result.Success || result.Value is null)
        {
            throw new InvalidOperationException(result.Error ?? "Overlay 设置读取失败。");
        }

        Settings = ZzzOverlaySettingsMapper.Create(result.Value.Values);
        _overlayService.SetEnabled(Settings.Enabled);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.StatePollIntervalMs) };
        _refreshTimer.Tick += (_, _) => Refresh(null);
    }

    public ZzzOverlayGuiSettings Settings { get; private set; }

    public ZzzOverlayStatusDto Status => _overlayService.GetStatus();

    public void ReloadConfiguration(ZzzOverlayGuiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        bool wasEnabled = Settings.Enabled;
        Settings = settings;
        _overlayService.SetEnabled(settings.Enabled);
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(settings.StatePollIntervalMs);
        if (!settings.Enabled)
        {
            if (_window is null)
            {
                _refreshTimer.Stop();
            }
            else
            {
                Hide();
            }

            return;
        }

        if (!wasEnabled && settings.ShowByDefault)
        {
            Show(null);
            return;
        }

        ApplyToWindow();
    }

    public void Start()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Settings.Enabled && Settings.ShowByDefault && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Show(null);
        }
    }

    public void TryToggleFromHotkey(string key)
    {
        if (!Settings.Enabled
            || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)
            || !string.Equals(key, Settings.Hotkey, StringComparison.OrdinalIgnoreCase)
            || !IsKeyDown(0x11)
            || !IsKeyDown(0x12))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_window?.IsVisible == true)
            {
                Hide();
            }
            else
            {
                Show(null);
            }
        });
    }

    public ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfiguration(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(
            new ZzzSaveConfigScopeRequest("overlay", values));
        if (result.Success && result.Value is not null)
        {
            ReloadConfiguration(ZzzOverlaySettingsMapper.Create(result.Value.Values));
        }

        return result;
    }

    public ZzzBackendResult<ZzzConfigScopeValuesDto> ResetPanelGeometry() =>
        SaveConfiguration(new Dictionary<string, object?>
        {
            ["panel_geometry"] = ZzzOverlaySettingsMapper.DefaultPanelGeometry(),
        });

    public void Show(string? windowTitle)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Settings.Enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return;
        }

        _window ??= CreateWindow();
        _window.ApplySettings(Settings);
        _window.FollowGameWindow(windowTitle ?? ResolveGameWindowTitle());
        _window.Render(_overlayService.GetStatus(), _overlayService.GetLastFrame(), _overlayService.GetPerformanceSamples());
        _window.Show();
        _refreshTimer.Start();
    }

    public void Hide()
    {
        Dispatcher.UIThread.VerifyAccess();
        _refreshTimer.Stop();
        _window?.Hide();
    }

    public void Refresh(string? windowTitle)
    {
        long startedAt = Stopwatch.GetTimestamp();
        if (_window?.IsVisible != true)
        {
            return;
        }

        _window.FollowGameWindow(windowTitle ?? ResolveGameWindowTitle());
        _window.Render(_overlayService.GetStatus(), _overlayService.GetLastFrame(), _overlayService.GetPerformanceSamples());
        _overlayService.SubmitPerformanceSample(new ZzzOverlayPerformanceSampleDto(
            "overlay_refresh_ms",
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            "ms",
            DateTimeOffset.UtcNow));
    }

    private ZzzOverlayTechnicalWindow CreateWindow()
    {
        ZzzOverlayTechnicalWindow window = new();
        window.Closed += (_, _) => _window = null;
        return window;
    }

    private string? ResolveGameWindowTitle()
    {
        ZzzBackendResult<ZzzWindowStatusDto> result = _backend.GetWindow();
        return result.Success ? result.Value?.WinTitle : null;
    }

    private static bool IsKeyDown(int virtualKey) =>
        OperatingSystem.IsWindows() && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private void ApplyToWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.ApplySettings(Settings);
        _window.Render(_overlayService.GetStatus(), _overlayService.GetLastFrame(), _overlayService.GetPerformanceSamples());
    }
}
