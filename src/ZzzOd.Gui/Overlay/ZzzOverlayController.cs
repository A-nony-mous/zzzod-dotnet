using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayController : IDisposable
{
    private static readonly TimeSpan HotkeyToggleDebounce = TimeSpan.FromMilliseconds(350);

    private readonly IZzzOverlayService _overlayService;
    private readonly IZzzAppBackend _backend;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ZzzGameWindowTracker _windowTracker;
    private readonly DispatcherTimer _inputTimer;
    private readonly Dictionary<string, ZzzOverlayInfoPanelWindow> _panelWindows = new(StringComparer.Ordinal);
    private ZzzOverlayTechnicalWindow? _window;
    private ZzzWindowStatusDto? _currentGameWindow;
    private bool _visibilityRequested;
    private bool _hotkeyPressed;
    private bool _legacyLayoutMigrated;
    private bool _savingPanelLayout;
    private bool _disposed;
    private DateTimeOffset _lastHotkeyToggleAt;

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
        _visibilityRequested = Settings.ShowByDefault;
        _overlayService.SetEnabled(Settings.Enabled);
        ConfigureDisplay(Settings);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.StatePollIntervalMs) };
        _refreshTimer.Tick += (_, _) => Refresh(null);
        _windowTracker = new ZzzGameWindowTracker(backend, Settings.FollowIntervalMs);
        _windowTracker.WindowChanged += ApplyTrackedWindow;
        _inputTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.InputPollIntervalMs) };
        _inputTimer.Tick += (_, _) => PollInput();
    }

    public ZzzOverlayGuiSettings Settings { get; private set; }

    public ZzzOverlayStatusDto Status => _overlayService.GetStatus();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(Dispose).GetAwaiter().GetResult();
            return;
        }

        _visibilityRequested = false;
        HideWindows();
        _disposed = true;
        _refreshTimer.Stop();
        _inputTimer.Stop();
        _windowTracker.WindowChanged -= ApplyTrackedWindow;
        _windowTracker.Stop();

        ZzzOverlayTechnicalWindow? visionWindow = _window;
        _window = null;
        visionWindow?.Close();
        foreach (ZzzOverlayInfoPanelWindow panel in _panelWindows.Values.ToArray())
        {
            panel.Close();
        }

        _panelWindows.Clear();
    }

    public void ReloadConfiguration(ZzzOverlayGuiSettings settings)
    {
        if (_disposed)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(settings);
        bool wasEnabled = Settings.Enabled;
        bool visibilityChanged = Settings.ShowByDefault != settings.ShowByDefault;
        Settings = settings;
        _overlayService.SetEnabled(settings.Enabled);
        ConfigureDisplay(settings);
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(settings.StatePollIntervalMs);
        _windowTracker.UpdateInterval(settings.FollowIntervalMs);
        _inputTimer.Interval = TimeSpan.FromMilliseconds(settings.InputPollIntervalMs);
        _legacyLayoutMigrated = false;

        if (!settings.Enabled)
        {
            _visibilityRequested = false;
            _refreshTimer.Stop();
            _windowTracker.Stop();
            _inputTimer.Stop();
            HideWindows();
            return;
        }

        _inputTimer.Start();
        if (!wasEnabled || visibilityChanged)
        {
            _visibilityRequested = settings.ShowByDefault;
        }

        if (_visibilityRequested)
        {
            _windowTracker.Start();
            FollowWindow(force: true);
        }
        else
        {
            _windowTracker.Stop();
            HideWindows();
        }
    }

    public void Start()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || !Settings.Enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return;
        }

        _inputTimer.Start();
        if (!Settings.ShowByDefault)
        {
            return;
        }

        _visibilityRequested = true;
        _windowTracker.Start();
        FollowWindow(force: true);
    }

    public void TryToggleFromHotkey(string key)
    {
        if (_disposed ||
            !Settings.Enabled ||
            !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ||
            !string.Equals(key, Settings.Hotkey, StringComparison.OrdinalIgnoreCase) ||
            !IsKeyDown(0x11) ||
            !IsKeyDown(0x12))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_hotkeyPressed)
            {
                return;
            }

            _hotkeyPressed = true;
            ToggleRequestedVisibility();
        });
    }

    public ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfiguration(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Dictionary<string, object?> requested = new(values, StringComparer.Ordinal);
        if (requested.TryGetValue("panel_lock_to_game_window", out object? rawLock))
        {
            bool lockToGameWindow = Convert.ToBoolean(rawLock, System.Globalization.CultureInfo.InvariantCulture);
            ApplyGlobalPanelModeChange(requested, lockToGameWindow);
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(
            new ZzzSaveConfigScopeRequest("overlay", requested));
        if (result.Success && result.Value is not null)
        {
            ReloadConfiguration(ZzzOverlaySettingsMapper.Create(result.Value.Values));
        }

        return result;
    }

    public ZzzBackendResult<ZzzConfigScopeValuesDto> ResetPanelGeometry()
    {
        _legacyLayoutMigrated = false;
        return SaveConfiguration(new Dictionary<string, object?>
        {
            ["panel_geometry"] = ZzzOverlaySettingsMapper.DefaultPanelGeometry(),
        });
    }

    public void Show(string? _ = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || !Settings.Enabled)
        {
            return;
        }

        _visibilityRequested = true;
        _windowTracker.Start();
        _inputTimer.Start();
        FollowWindow(force: true);
    }

    public void Hide()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed)
        {
            return;
        }

        _visibilityRequested = false;
        _windowTracker.Stop();
        HideWindows();
    }

    public void Refresh(string? _)
    {
        if (_disposed)
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        if (_window?.IsVisible != true || _currentGameWindow is null)
        {
            return;
        }

        RenderSnapshot(_currentGameWindow, geometryChanged: false);
        _overlayService.SubmitPerformanceSample(new ZzzOverlayPerformanceSampleDto(
            "overlay_refresh_ms",
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            "ms",
            DateTimeOffset.UtcNow));
    }

    internal IReadOnlyList<ZzzOverlayCaptureTarget> GetCaptureTargets()
    {
        Dispatcher.UIThread.VerifyAccess();
        List<ZzzOverlayCaptureTarget> targets = [];
        if (_window?.IsVisible == true)
        {
            targets.Add(CreateCaptureTarget("vision", _window));
        }

        foreach ((string panelId, ZzzOverlayInfoPanelWindow panelWindow) in _panelWindows)
        {
            if (panelWindow.IsVisible)
            {
                targets.Add(CreateCaptureTarget(panelId, panelWindow));
            }
        }

        return targets;
    }

    /// <summary>
    /// 返回控制视觉窗口跟随的缓存游戏客户区快照。
    /// </summary>
    internal ZzzBackendResult<ZzzWindowStatusDto> GetGameWindowSnapshotForCapture()
    {
        Dispatcher.UIThread.VerifyAccess();
        return _windowTracker.GetSnapshot(force: false);
    }

    internal void FollowWindowForTesting(bool force = false) => FollowWindow(force);

    internal ZzzOverlayTechnicalWindow? VisionWindowForTesting => _window;

    private void FollowWindow(bool force = false)
    {
        Dispatcher.UIThread.VerifyAccess();
        _ = _windowTracker.GetSnapshot(force);
    }

    private void ApplyTrackedWindow(ZzzBackendResult<ZzzWindowStatusDto> windowResult, bool force)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Settings.Enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) || !_visibilityRequested)
        {
            HideWindows();
            return;
        }

        if (!windowResult.Success || windowResult.Value is null || !CanShowForWindow(windowResult.Value))
        {
            HideWindows();
            return;
        }

        ZzzWindowStatusDto gameWindow = windowResult.Value;
        bool windowCreated = _window is null;
        bool geometryChanged = ShouldApplyGeometry(force, windowCreated, _currentGameWindow, gameWindow);
        _currentGameWindow = gameWindow;
        _window ??= CreateWindow();
        if (geometryChanged)
        {
            _window.ApplySettings(Settings);
            _window.FollowGameWindow(gameWindow);
        }

        if (!_window.IsVisible)
        {
            _window.Show();
            geometryChanged = true;
        }

        if (TryCompleteDeferredPanelModeChanges(gameWindow))
        {
            return;
        }

        if (!_legacyLayoutMigrated && TryMigrateLegacyPanelLayouts(gameWindow))
        {
            return;
        }

        if (geometryChanged)
        {
            RenderSnapshot(gameWindow, geometryChanged: true);
        }

        _refreshTimer.Start();
    }

    private void RenderSnapshot(ZzzWindowStatusDto gameWindow, bool geometryChanged)
    {
        if (_window is null)
        {
            return;
        }

        ZzzOverlaySnapshotDto snapshot = _overlayService.GetSnapshot();
        _window.Render(snapshot.VisionFrame);
        foreach (ZzzOverlayPanelSettings panel in Settings.Panels)
        {
            if (!panel.Enabled)
            {
                if (_panelWindows.TryGetValue(panel.Id, out ZzzOverlayInfoPanelWindow? existing))
                {
                    existing.HidePanel();
                }

                continue;
            }

            ZzzOverlayInfoPanelWindow panelWindow = GetOrCreatePanelWindow(panel.Id);
            panelWindow.ApplyConfiguration(panel, Settings, gameWindow, geometryChanged);
            panelWindow.UpdateContent(ZzzOverlayPanelTextFormatter.Format(panel.Id, snapshot, Settings));
            if (!panelWindow.IsVisible)
            {
                panelWindow.Show();
            }
        }
    }

    private void HideWindows()
    {
        _refreshTimer.Stop();
        _window?.Hide();
        foreach (ZzzOverlayInfoPanelWindow panel in _panelWindows.Values)
        {
            panel.HidePanel();
        }

        _currentGameWindow = null;
    }

    private void PollInput()
    {
        if (!Settings.Enabled || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return;
        }

        bool pressed = IsConfiguredHotkeyPressed();
        if (pressed && !_hotkeyPressed)
        {
            ToggleRequestedVisibility();
        }

        _hotkeyPressed = pressed;
    }

    private bool IsConfiguredHotkeyPressed()
    {
        string hotkey = Settings.Hotkey.Trim();
        if (hotkey.Length != 1 || !IsKeyDown(0x11) || !IsKeyDown(0x12))
        {
            return false;
        }

        char key = char.ToUpperInvariant(hotkey[0]);
        return key is >= 'A' and <= 'Z' or >= '0' and <= '9' && IsKeyDown(key);
    }

    private void ToggleRequestedVisibility()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || !Settings.Enabled || !IsGameWindowActiveForHotkey())
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastHotkeyToggleAt < HotkeyToggleDebounce)
        {
            return;
        }

        _lastHotkeyToggleAt = now;
        SaveConfiguration(new Dictionary<string, object?>
        {
            ["visible"] = !Settings.ShowByDefault,
        });
    }

    private bool IsGameWindowActiveForHotkey()
    {
        ZzzBackendResult<ZzzWindowStatusDto> result = _backend.GetWindow();
        return result.Success && result.Value is
        {
            IsWinValid: true,
            IsWinActive: true,
            IsWinMinimized: false,
        };
    }

    private bool TryMigrateLegacyPanelLayouts(ZzzWindowStatusDto gameWindow)
    {
        _legacyLayoutMigrated = true;
        List<ZzzOverlayPanelSettings> legacy = Settings.Panels.Where(ZzzOverlayPanelLayout.NeedsLayoutMigration).ToList();
        if (legacy.Count == 0)
        {
            return false;
        }

        double scaling = ResolveCurrentDesktopScaling(gameWindow);
        foreach (ZzzOverlayPanelSettings panel in legacy)
        {
            if (panel.IsFreeMode)
            {
                ZzzOverlayPhysicalRect physicalBounds = ZzzOverlayPanelLayout.ResolveLegacyFreeForMigration(panel, scaling);
                ZzzOverlayDisplayArea displayArea = ResolveDisplayAreaForPanel(
                    panel,
                    physicalBounds,
                    scaling,
                    preferStoredFreeAnchor: true);
                ZzzOverlayPanelLayout.StoreFree(panel, physicalBounds, displayArea, ResolveDisplayDpi(displayArea));
            }
            else
            {
                ZzzOverlayPanelLayout.StoreLocked(panel, ZzzOverlayPanelLayout.ResolveLocked(panel, gameWindow), gameWindow);
            }
        }

        _savingPanelLayout = true;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = SaveConfiguration(new Dictionary<string, object?>
            {
                ["panel_geometry"] = ZzzOverlaySettingsMapper.CreatePanelGeometry(Settings.Panels),
                ["panel_free_mode_map"] = CreatePanelFreeModeMap(),
            });
            return result.Success;
        }
        finally
        {
            _savingPanelLayout = false;
        }
    }

    private void ApplyGlobalPanelModeChange(
        IDictionary<string, object?> requested,
        bool lockToGameWindow)
    {
        bool targetIsFreeMode = !lockToGameWindow;
        List<ZzzOverlayPanelSettings> panels = Settings.Panels
            .Select(ClonePanelSettings)
            .ToList();
        bool hasGameWindow = TryGetUsableLayoutGameWindow(out ZzzWindowStatusDto? gameWindow);

        foreach (ZzzOverlayPanelSettings panel in panels)
        {
            bool sourceIsFreeMode = panel.PendingSourceIsFreeMode ?? panel.IsFreeMode;
            if (sourceIsFreeMode == targetIsFreeMode)
            {
                panel.IsFreeMode = targetIsFreeMode;
                panel.PendingSourceIsFreeMode = null;
                continue;
            }

            if (!hasGameWindow || gameWindow is null)
            {
                panel.IsFreeMode = targetIsFreeMode;
                panel.PendingSourceIsFreeMode = sourceIsFreeMode;
                continue;
            }

            ConvertPanelMode(panel, sourceIsFreeMode, targetIsFreeMode, gameWindow, useCurrentWindowBounds: true);
        }

        requested["panel_free_mode_map"] = CreatePanelFreeModeMap(panels);
        requested["panel_geometry"] = ZzzOverlaySettingsMapper.CreatePanelGeometry(panels);
    }

    private bool TryCompleteDeferredPanelModeChanges(ZzzWindowStatusDto gameWindow)
    {
        List<ZzzOverlayPanelSettings> pendingPanels = Settings.Panels
            .Where(panel => panel.PendingSourceIsFreeMode.HasValue)
            .ToList();
        if (pendingPanels.Count == 0)
        {
            return false;
        }

        foreach (ZzzOverlayPanelSettings panel in pendingPanels)
        {
            bool sourceIsFreeMode = panel.PendingSourceIsFreeMode!.Value;
            bool targetIsFreeMode = panel.IsFreeMode;
            if (sourceIsFreeMode == targetIsFreeMode)
            {
                panel.PendingSourceIsFreeMode = null;
                continue;
            }

            ConvertPanelMode(panel, sourceIsFreeMode, targetIsFreeMode, gameWindow, useCurrentWindowBounds: false);
        }

        _savingPanelLayout = true;
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = SaveConfiguration(new Dictionary<string, object?>
            {
                ["panel_geometry"] = ZzzOverlaySettingsMapper.CreatePanelGeometry(Settings.Panels),
                ["panel_free_mode_map"] = CreatePanelFreeModeMap(),
            });
            return result.Success;
        }
        finally
        {
            _savingPanelLayout = false;
        }
    }

    private void ConvertPanelMode(
        ZzzOverlayPanelSettings panel,
        bool sourceIsFreeMode,
        bool targetIsFreeMode,
        ZzzWindowStatusDto gameWindow,
        bool useCurrentWindowBounds)
    {
        if (useCurrentWindowBounds &&
            _panelWindows.TryGetValue(panel.Id, out ZzzOverlayInfoPanelWindow? panelWindow) &&
            panelWindow.TryGetCurrentPhysicalBounds(out ZzzOverlayPhysicalRect currentBounds, out ZzzOverlayDisplayArea displayArea, out uint displayDpi))
        {
            ZzzOverlayPanelLayout.StoreMode(
                panel,
                targetIsFreeMode,
                currentBounds,
                gameWindow,
                displayArea,
                displayDpi);
            return;
        }

        double scaling = ResolveCurrentDesktopScaling(gameWindow);
        ZzzOverlayPhysicalRect physicalBounds;
        if (sourceIsFreeMode)
        {
            ZzzOverlayPhysicalRect provisionalBounds = ZzzOverlayPanelLayout.ResolveFree(panel, scaling);
            ZzzOverlayDisplayArea sourceDisplayArea = ResolveDisplayAreaForPanel(
                panel,
                provisionalBounds,
                scaling,
                preferStoredFreeAnchor: true);
            physicalBounds = ZzzOverlayPanelLayout.ResolveFree(panel, sourceDisplayArea);
        }
        else
        {
            physicalBounds = ZzzOverlayPanelLayout.ResolveLocked(panel, gameWindow);
        }

        ZzzOverlayDisplayArea fallbackDisplayArea = ResolveDisplayAreaForPanel(
            panel,
            physicalBounds,
            scaling,
            preferStoredFreeAnchor: sourceIsFreeMode);
        ZzzOverlayPanelLayout.StoreMode(panel, targetIsFreeMode, physicalBounds, gameWindow, fallbackDisplayArea, ResolveDisplayDpi(fallbackDisplayArea));
    }

    private bool TryGetUsableLayoutGameWindow(out ZzzWindowStatusDto? gameWindow)
    {
        if (_currentGameWindow is not null && ZzzOverlayPanelLayout.HasUsableGameBounds(_currentGameWindow))
        {
            gameWindow = _currentGameWindow;
            return true;
        }

        gameWindow = null;
        return false;
    }

    private double ResolveCurrentDesktopScaling(ZzzWindowStatusDto gameWindow) =>
        _window is { IsVisible: true }
            ? Math.Max(0.5d, _window.DesktopScaling)
            : gameWindow.Dpi > 0
                ? Math.Max(0.5d, gameWindow.Dpi / 96d)
                : 1d;

    private static uint ResolveDisplayDpi(ZzzOverlayDisplayArea displayArea) =>
        (uint)Math.Max(1d, Math.Round(displayArea.EffectiveScaling * 96d));

    private ZzzOverlayDisplayArea ResolveDisplayAreaForPanel(
        ZzzOverlayPanelSettings panel,
        ZzzOverlayPhysicalRect bounds,
        double fallbackScaling,
        bool preferStoredFreeAnchor)
    {
        ZzzOverlayTechnicalWindow? window = _window;
        Screen? screen = null;
        if (window is not null && preferStoredFreeAnchor)
        {
            if (!string.IsNullOrWhiteSpace(panel.FreeDisplayName))
            {
                screen = window.Screens.All.FirstOrDefault(candidate =>
                    string.Equals(candidate.DisplayName, panel.FreeDisplayName, StringComparison.Ordinal));
            }

            screen ??= window.Screens.All.FirstOrDefault(candidate =>
            {
                PixelRect workArea = candidate.WorkingArea;
                return ZzzOverlayPanelLayout.MatchesFreeWorkAreaAnchor(
                    panel,
                    new ZzzOverlayPhysicalRect(workArea.X, workArea.Y, workArea.Width, workArea.Height));
            });

            if (screen is null &&
                ZzzOverlayPanelLayout.TryGetFreeWorkAreaAnchor(panel, out ZzzOverlayPhysicalRect savedWorkArea))
            {
                PixelPoint anchor = new(
                    (int)Math.Round(savedWorkArea.X + savedWorkArea.Width / 2d),
                    (int)Math.Round(savedWorkArea.Y + savedWorkArea.Height / 2d));
                screen = window.Screens.ScreenFromPoint(anchor);
            }
        }

        if (window is not null)
        {
            PixelPoint origin = new((int)Math.Round(bounds.X), (int)Math.Round(bounds.Y));
            screen ??= window.Screens.ScreenFromPoint(origin) ??
                window.Screens.ScreenFromWindow(window) ??
                window.Screens.Primary;
        }

        if (screen is not null)
        {
            PixelRect area = screen.WorkingArea;
            return new ZzzOverlayDisplayArea(
                screen.DisplayName,
                new ZzzOverlayPhysicalRect(area.X, area.Y, area.Width, area.Height),
                screen.Scaling);
        }

        return new ZzzOverlayDisplayArea(
            null,
            new ZzzOverlayPhysicalRect(bounds.X, bounds.Y, Math.Max(1d, bounds.Width), Math.Max(1d, bounds.Height)),
            fallbackScaling);
    }

    private static ZzzOverlayPanelSettings ClonePanelSettings(ZzzOverlayPanelSettings panel) => new(
        panel.Id,
        panel.Title,
        panel.Enabled,
        panel.X,
        panel.Y,
        panel.Width,
        panel.Height)
    {
        IsFreeMode = panel.IsFreeMode,
        LayoutVersion = panel.LayoutVersion,
        LockedX = panel.LockedX,
        LockedY = panel.LockedY,
        LockedWidth = panel.LockedWidth,
        LockedHeight = panel.LockedHeight,
        FreeX = panel.FreeX,
        FreeY = panel.FreeY,
        FreeWidth = panel.FreeWidth,
        FreeHeight = panel.FreeHeight,
        FreeDpi = panel.FreeDpi,
        FreeDisplayName = panel.FreeDisplayName,
        FreeWorkAreaX = panel.FreeWorkAreaX,
        FreeWorkAreaY = panel.FreeWorkAreaY,
        FreeWorkAreaWidth = panel.FreeWorkAreaWidth,
        FreeWorkAreaHeight = panel.FreeWorkAreaHeight,
        PendingSourceIsFreeMode = panel.PendingSourceIsFreeMode,
        FontSize = panel.FontSize,
        Opacity = panel.Opacity,
    };

    private ZzzOverlayTechnicalWindow CreateWindow()
    {
        ZzzOverlayTechnicalWindow window = new();
        window.Closed += (_, _) => _window = null;
        return window;
    }

    private static ZzzOverlayCaptureTarget CreateCaptureTarget(string id, Window window)
    {
        double scaling = Math.Max(0.5d, window.DesktopScaling);
        return new ZzzOverlayCaptureTarget(
            id,
            window,
            window.Position,
            new PixelSize(
                Math.Max(1, (int)Math.Round(window.Width * scaling)),
                Math.Max(1, (int)Math.Round(window.Height * scaling))));
    }

    private ZzzOverlayInfoPanelWindow GetOrCreatePanelWindow(string panelId)
    {
        if (_panelWindows.TryGetValue(panelId, out ZzzOverlayInfoPanelWindow? panelWindow))
        {
            return panelWindow;
        }

        panelWindow = new ZzzOverlayInfoPanelWindow();
        panelWindow.GeometryCommitted += OnPanelGeometryCommitted;
        panelWindow.FreeModeChanged += OnPanelFreeModeChanged;
        panelWindow.AppearanceChanged += OnPanelAppearanceChanged;
        panelWindow.EditModeExitRequested += OnPanelEditModeExitRequested;
        panelWindow.Closed += (_, _) => _panelWindows.Remove(panelId);
        _panelWindows.Add(panelId, panelWindow);
        return panelWindow;
    }

    private void OnPanelGeometryCommitted(ZzzOverlayInfoPanelWindow panelWindow) => PersistPanelLayout();

    private void OnPanelFreeModeChanged(ZzzOverlayInfoPanelWindow panelWindow)
    {
        if (panelWindow.Panel is not null)
        {
            Settings.PanelFreeModeMap[$"{panelWindow.Panel.Id}_panel"] = panelWindow.Panel.IsFreeMode;
        }

        PersistPanelLayout();
    }

    private void OnPanelAppearanceChanged(ZzzOverlayInfoPanelWindow panelWindow)
    {
        if (_savingPanelLayout)
        {
            return;
        }

        _savingPanelLayout = true;
        try
        {
            SaveConfiguration(new Dictionary<string, object?>
            {
                ["panel_appearance"] = ZzzOverlaySettingsMapper.CreatePanelAppearance(Settings.Panels),
            });
        }
        finally
        {
            _savingPanelLayout = false;
        }
    }

    private void OnPanelEditModeExitRequested(ZzzOverlayInfoPanelWindow panelWindow)
    {
        if (!Settings.LayoutEditMode || _savingPanelLayout)
        {
            return;
        }

        SaveConfiguration(new Dictionary<string, object?>
        {
            ["panel_edit_mode"] = false,
        });
    }

    private void PersistPanelLayout()
    {
        if (_savingPanelLayout)
        {
            return;
        }

        _savingPanelLayout = true;
        try
        {
            SaveConfiguration(new Dictionary<string, object?>
            {
                ["panel_geometry"] = ZzzOverlaySettingsMapper.CreatePanelGeometry(Settings.Panels),
                ["panel_free_mode_map"] = CreatePanelFreeModeMap(),
            });
        }
        finally
        {
            _savingPanelLayout = false;
        }
    }

    private Dictionary<string, bool> CreatePanelFreeModeMap() => CreatePanelFreeModeMap(Settings.Panels);

    private static Dictionary<string, bool> CreatePanelFreeModeMap(IReadOnlyList<ZzzOverlayPanelSettings> panels) => panels.ToDictionary(
        panel => $"{panel.Id}_panel",
        panel => panel.IsFreeMode,
        StringComparer.Ordinal);

    private void ConfigureDisplay(ZzzOverlayGuiSettings settings)
    {
        _overlayService.ConfigureDisplay(new ZzzOverlayDisplayOptionsDto
        {
            VisionLayerEnabled = settings.VisionLayerEnabled,
            ShowYolo = settings.Visual.ShowYolo,
            YoloDedupIouThreshold = settings.Visual.YoloDedupIouThreshold,
            ShowOcr = settings.Visual.ShowOcr,
            ShowTemplate = settings.Visual.ShowTemplate,
            ShowCv = settings.Visual.ShowCv,
            PanelEnabledMap = settings.Panels.ToDictionary(panel => panel.Id, panel => panel.Enabled, StringComparer.Ordinal),
            PerformanceMetricEnabledMap = new Dictionary<string, bool>(settings.PerformanceMetrics, StringComparer.Ordinal),
            LogMaxLines = settings.LogMaxLines,
            LogFadeSeconds = settings.LogFadeSeconds,
        });
    }

    private bool CanShowForWindow(ZzzWindowStatusDto status) => CanShowForWindow(status, Settings.LayoutEditMode);

    internal static bool CanShowForWindow(ZzzWindowStatusDto status, bool layoutEditMode) =>
        status.IsWinValid &&
        !status.IsWinMinimized &&
        status.X.HasValue &&
        status.Y.HasValue &&
        status.Width is > 0 &&
        status.Height is > 0 &&
        (status.IsWinActive || layoutEditMode);

    internal static bool HasWindowStateChanged(ZzzWindowStatusDto? previous, ZzzWindowStatusDto current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return previous is null || WindowSignature.From(previous) != WindowSignature.From(current);
    }

    internal static bool ShouldApplyGeometry(
        bool force,
        bool windowCreated,
        ZzzWindowStatusDto? previous,
        ZzzWindowStatusDto current) =>
        force || windowCreated || HasWindowStateChanged(previous, current);

    private static bool IsKeyDown(int virtualKey) =>
        OperatingSystem.IsWindows() && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private readonly record struct WindowSignature(
        bool IsValid,
        bool IsActive,
        bool IsMinimized,
        int? X,
        int? Y,
        int? Width,
        int? Height,
        uint Dpi)
    {
        public static WindowSignature From(ZzzWindowStatusDto window) => new(
            window.IsWinValid,
            window.IsWinActive,
            window.IsWinMinimized,
            window.X,
            window.Y,
            window.Width,
            window.Height,
            window.Dpi);
    }
}
