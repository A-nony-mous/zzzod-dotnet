using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Overlay;

internal sealed class ZzzOverlayInfoPanelWindow : Window
{
    private const double HeaderHeight = 28d;
    private const double ResizeMargin = 6d;

    private readonly Border _panelBorder;
    private readonly Grid _header;
    private readonly TextBlock _title;
    private readonly TextBlock _content;
    private readonly ScrollViewer _contentScrollViewer;
    private readonly Button _fontDecreaseButton;
    private readonly Button _fontIncreaseButton;
    private readonly Button _opacityDecreaseButton;
    private readonly Button _opacityIncreaseButton;
    private readonly Button _modeButton;
    private readonly Button _closeEditButton;
    private ZzzOverlayPanelSettings? _panel;
    private ZzzOverlayGuiSettings? _settings;
    private ZzzWindowStatusDto? _gameWindow;
    private bool _applyingGeometry;
    private bool _userManipulating;
    private bool _geometryApplied;
    private bool _scalingRefreshQueued;
    private bool _contentScrollQueued;
    private bool _closed;
    private readonly DispatcherTimer _geometryCommitTimer;

    public ZzzOverlayInfoPanelWindow()
    {
        Title = string.Empty;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;

        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8, 0),
        };
        _fontDecreaseButton = CreateEditButton("A−", OnFontDecrease);
        _fontIncreaseButton = CreateEditButton("A⁺", OnFontIncrease);
        _opacityDecreaseButton = CreateEditButton("▣−", OnOpacityDecrease);
        _opacityIncreaseButton = CreateEditButton("▣⁺", OnOpacityIncrease);
        _modeButton = CreateEditButton("锁定", OnToggleMode, 44d);
        _closeEditButton = CreateEditButton("✕", OnExitEditMode);

        _header = new Grid
        {
            Height = HeaderHeight,
            IsVisible = false,
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto,Auto"),
            Children =
            {
                _title,
                _fontDecreaseButton,
                _fontIncreaseButton,
                _opacityDecreaseButton,
                _opacityIncreaseButton,
                _modeButton,
                _closeEditButton,
            },
        };
        Grid.SetColumn(_fontDecreaseButton, 1);
        Grid.SetColumn(_fontIncreaseButton, 2);
        Grid.SetColumn(_opacityDecreaseButton, 3);
        Grid.SetColumn(_opacityIncreaseButton, 4);
        Grid.SetColumn(_modeButton, 5);
        Grid.SetColumn(_closeEditButton, 6);

        _content = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };
        _contentScrollViewer = new ScrollViewer
        {
            Content = _content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
            IsHitTestVisible = false,
        };
        Grid panelContent = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            ClipToBounds = true,
            Children = { _header, _contentScrollViewer },
        };
        Grid.SetRow(_contentScrollViewer, 1);
        _panelBorder = new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Child = panelContent,
        };
        Content = _panelBorder;

        _geometryCommitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _geometryCommitTimer.Tick += OnGeometryCommitTimerTick;
        PositionChanged += OnWindowGeometryChanged;
        Resized += OnWindowGeometryChanged;
        ScalingChanged += OnScalingChanged;
        Closed += OnClosed;
        _panelBorder.PointerPressed += OnPanelPointerPressed;
        _panelBorder.PointerReleased += OnPanelPointerReleased;
        _panelBorder.PointerCaptureLost += OnPanelPointerCaptureLost;
        Opened += OnOpened;
    }

    public event Action<ZzzOverlayInfoPanelWindow>? GeometryCommitted;

    public event Action<ZzzOverlayInfoPanelWindow>? FreeModeChanged;

    public event Action<ZzzOverlayInfoPanelWindow>? AppearanceChanged;

    public event Action<ZzzOverlayInfoPanelWindow>? EditModeExitRequested;

    public ZzzOverlayPanelSettings? Panel => _panel;

    /// <summary>
    /// 停靠几何是否已成功应用。未生效前不得显示，否则 Avalonia 会按宿主窗口默认位置摆放面板。
    /// </summary>
    public bool GeometryApplied => _geometryApplied;

    internal bool ResourcesReleased =>
        _closed &&
        !_geometryCommitTimer.IsEnabled &&
        _panel is null &&
        _settings is null &&
        _gameWindow is null;

    internal ScrollViewer ContentScrollViewer => _contentScrollViewer;

    internal string ContentText => _content.Text ?? string.Empty;

    internal bool TryGetCurrentPhysicalBounds(
        out ZzzOverlayPhysicalRect bounds,
        out double desktopScaling,
        out uint displayDpi)
    {
        if (!TryGetCurrentPhysicalBounds(out bounds, out ZzzOverlayDisplayArea displayArea, out displayDpi))
        {
            desktopScaling = 1d;
            return false;
        }

        desktopScaling = displayArea.EffectiveScaling;
        return true;
    }

    internal bool TryGetCurrentPhysicalBounds(
        out ZzzOverlayPhysicalRect bounds,
        out ZzzOverlayDisplayArea displayArea,
        out uint displayDpi)
    {
        if (_panel is null || _gameWindow is null || !_geometryApplied || Width <= 0d || Height <= 0d)
        {
            bounds = default;
            displayArea = default;
            displayDpi = 96u;
            return false;
        }

        bounds = ReadPhysicalBounds();
        displayArea = ResolveDisplayArea(bounds);
        displayDpi = ResolveDisplayDpi(displayArea);
        return bounds.Width > 0d && bounds.Height > 0d;
    }

    public void ApplyConfiguration(ZzzOverlayPanelSettings panel, ZzzOverlayGuiSettings settings, ZzzWindowStatusDto gameWindow, bool forceGeometry = false)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(gameWindow);
        _panel = panel;
        _settings = settings;
        _gameWindow = gameWindow;

        ApplyMinimumSize(ResolveWindowScaling());
        CanResize = settings.LayoutEditMode;
        _header.IsVisible = settings.LayoutEditMode;
        _title.Text = settings.LayoutEditMode ? panel.Title : string.Empty;
        _title.FontSize = Math.Clamp(panel.FontSize + 1d, 10d, 29d);
        _content.FontSize = Math.Clamp(panel.FontSize, 10d, 28d);
        _content.FontFamily = new FontFamily(settings.FontFamily);
        _title.FontFamily = new FontFamily(settings.FontFamily);
        _content.Foreground = ParseBrush(settings.PanelTextColor, Color.FromRgb(242, 242, 242));
        _title.Foreground = _content.Foreground;
        _panelBorder.Background = new SolidColorBrush(Color.FromArgb(180, 18, 20, 24));
        _panelBorder.BorderBrush = settings.LayoutEditMode
            ? new SolidColorBrush(Color.FromArgb(210, 96, 176, 255))
            : new SolidColorBrush(Color.FromArgb(72, 255, 255, 255));
        Opacity = settings.LayoutEditMode ? 1d : Math.Clamp(panel.Opacity / 100d, 0.05d, 1d);
        _modeButton.Content = panel.IsFreeMode ? "自由" : "锁定";
        ApplyNativeStyle();

        if (!_userManipulating && (forceGeometry || !_geometryApplied))
        {
            ZzzOverlayPhysicalRect bounds = panel.IsFreeMode
                ? ResolveFreeBounds(panel)
                : ZzzOverlayPanelLayout.ResolveLocked(panel, gameWindow, settings.Panels);
            ApplyPhysicalBounds(bounds);
            _geometryApplied = true;
        }
    }

    public void UpdateContent(string content)
    {
        _content.Text = content ?? string.Empty;
        if (_contentScrollQueued)
        {
            return;
        }

        _contentScrollQueued = true;
        Dispatcher.UIThread.Post(ScrollContentToEnd, DispatcherPriority.Render);
    }

    private void ScrollContentToEnd()
    {
        _contentScrollQueued = false;
        if (_closed)
        {
            return;
        }

        double maximum = Math.Max(0d, _contentScrollViewer.Extent.Height - _contentScrollViewer.Viewport.Height);
        _contentScrollViewer.Offset = new Vector(_contentScrollViewer.Offset.X, maximum);
    }

    public void HidePanel()
    {
        if (_userManipulating)
        {
            CommitUserGeometry();
        }

        _geometryCommitTimer.Stop();
        _userManipulating = false;
        Hide();
    }

    private static Button CreateEditButton(string content, EventHandler<RoutedEventArgs> handler, double width = 30d)
    {
        Button button = new()
        {
            Content = content,
            Width = width,
            Height = 22,
            Margin = new Thickness(2, 3),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.PointerPressed += OnEditButtonPointerPressed;
        button.Click += handler;
        return button;
    }

    private static void OnEditButtonPointerPressed(object? sender, PointerPressedEventArgs args) => args.Handled = true;

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_settings?.LayoutEditMode != true || _panel is null)
        {
            return;
        }

        PointerPoint point = args.GetCurrentPoint(_panelBorder);
        if (point.Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        Point position = args.GetPosition(_panelBorder);
        WindowEdge? edge = FindResizeEdge(position);
        if (edge.HasValue)
        {
            _userManipulating = true;
            BeginResizeDrag(edge.Value, args);
            args.Handled = true;
            return;
        }

        if (position.Y <= HeaderHeight)
        {
            _userManipulating = true;
            BeginMoveDrag(args);
            args.Handled = true;
        }
    }

    private void OnPanelPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_userManipulating)
        {
            _geometryCommitTimer.Stop();
            _geometryCommitTimer.Start();
        }
    }

    private void OnPanelPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
    {
        if (_userManipulating)
        {
            _geometryCommitTimer.Stop();
            _geometryCommitTimer.Start();
        }
    }

    private WindowEdge? FindResizeEdge(Point position)
    {
        bool west = position.X <= ResizeMargin;
        bool east = position.X >= Math.Max(0d, Bounds.Width - ResizeMargin);
        bool north = position.Y <= ResizeMargin;
        bool south = position.Y >= Math.Max(0d, Bounds.Height - ResizeMargin);
        return (west, east, north, south) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null,
        };
    }

    private void OnWindowGeometryChanged()
    {
        if (_userManipulating && !_applyingGeometry)
        {
            _geometryCommitTimer.Stop();
            _geometryCommitTimer.Start();
        }
    }

    private void OnWindowGeometryChanged(object? sender, EventArgs args) => OnWindowGeometryChanged();

    private void OnGeometryCommitTimerTick(object? sender, EventArgs args) => CommitUserGeometry();

    private void OnScalingChanged(object? sender, EventArgs args) => QueueScalingRefresh();

    private void OnOpened(object? sender, EventArgs args)
    {
        ApplyNativeStyle();
        if (_panel is not null && _settings is not null && _gameWindow is not null)
        {
            ApplyConfiguration(_panel, _settings, _gameWindow, forceGeometry: true);
        }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _closed = true;
        _geometryCommitTimer.Stop();
        _userManipulating = false;
        _scalingRefreshQueued = false;
        _contentScrollQueued = false;

        _geometryCommitTimer.Tick -= OnGeometryCommitTimerTick;
        PositionChanged -= OnWindowGeometryChanged;
        Resized -= OnWindowGeometryChanged;
        ScalingChanged -= OnScalingChanged;
        Opened -= OnOpened;
        Closed -= OnClosed;
        _panelBorder.PointerPressed -= OnPanelPointerPressed;
        _panelBorder.PointerReleased -= OnPanelPointerReleased;
        _panelBorder.PointerCaptureLost -= OnPanelPointerCaptureLost;
        DetachEditButton(_fontDecreaseButton, OnFontDecrease);
        DetachEditButton(_fontIncreaseButton, OnFontIncrease);
        DetachEditButton(_opacityDecreaseButton, OnOpacityDecrease);
        DetachEditButton(_opacityIncreaseButton, OnOpacityIncrease);
        DetachEditButton(_modeButton, OnToggleMode);
        DetachEditButton(_closeEditButton, OnExitEditMode);

        GeometryCommitted = null;
        FreeModeChanged = null;
        AppearanceChanged = null;
        EditModeExitRequested = null;
        _panel = null;
        _settings = null;
        _gameWindow = null;
    }

    private static void DetachEditButton(Button button, EventHandler<RoutedEventArgs> handler)
    {
        button.PointerPressed -= OnEditButtonPointerPressed;
        button.Click -= handler;
    }

    private void CommitUserGeometry()
    {
        _geometryCommitTimer.Stop();
        if (_closed)
        {
            return;
        }

        _userManipulating = false;
        if (_panel is null || _gameWindow is null || _settings?.LayoutEditMode != true || _applyingGeometry)
        {
            return;
        }

        ZzzOverlayPhysicalRect actual = ReadPhysicalBounds();
        if (_panel.IsFreeMode)
        {
            actual = ClampToCurrentWorkArea(actual);
            ApplyPhysicalBounds(actual);
            ZzzOverlayDisplayArea displayArea = ResolveDisplayArea(actual);
            ZzzOverlayPanelLayout.StoreFree(_panel, actual, displayArea, ResolveDisplayDpi(displayArea));
        }
        else
        {
            actual = ZzzOverlayPanelLayout.ClampToGame(actual, new ZzzOverlayPhysicalRect(
                _gameWindow.X ?? 0,
                _gameWindow.Y ?? 0,
                _gameWindow.Width ?? 1,
                _gameWindow.Height ?? 1));
            ApplyPhysicalBounds(actual);
            ZzzOverlayPanelLayout.StoreLocked(_panel, actual, _gameWindow);
        }

        _geometryApplied = true;
        GeometryCommitted?.Invoke(this);
    }

    private void OnFontDecrease(object? sender, RoutedEventArgs args) => ChangeAppearance(fontDelta: -1, opacityDelta: 0);

    private void OnFontIncrease(object? sender, RoutedEventArgs args) => ChangeAppearance(fontDelta: 1, opacityDelta: 0);

    private void OnOpacityDecrease(object? sender, RoutedEventArgs args) => ChangeAppearance(fontDelta: 0, opacityDelta: -5);

    private void OnOpacityIncrease(object? sender, RoutedEventArgs args) => ChangeAppearance(fontDelta: 0, opacityDelta: 5);

    private void ChangeAppearance(int fontDelta, int opacityDelta)
    {
        if (_panel is null || _settings?.LayoutEditMode != true)
        {
            return;
        }

        _panel.FontSize = Math.Clamp(_panel.FontSize + fontDelta, 10d, 28d);
        _panel.Opacity = Math.Clamp(_panel.Opacity + opacityDelta, 5d, 100d);
        _content.FontSize = _panel.FontSize;
        _title.FontSize = Math.Clamp(_panel.FontSize + 1d, 10d, 29d);
        AppearanceChanged?.Invoke(this);
    }

    private void OnToggleMode(object? sender, RoutedEventArgs args)
    {
        if (_panel is null || _gameWindow is null || _settings?.LayoutEditMode != true)
        {
            return;
        }

        ZzzOverlayPhysicalRect current = ReadPhysicalBounds();
        _panel.IsFreeMode = !_panel.IsFreeMode;
        if (_panel.IsFreeMode)
        {
            ZzzOverlayDisplayArea displayArea = ResolveDisplayArea(current);
            ZzzOverlayPanelLayout.StoreFree(_panel, current, displayArea, ResolveDisplayDpi(displayArea));
        }
        else
        {
            ZzzOverlayPanelLayout.StoreLocked(_panel, current, _gameWindow);
        }

        _modeButton.Content = _panel.IsFreeMode ? "自由" : "锁定";
        FreeModeChanged?.Invoke(this);
    }

    private void OnExitEditMode(object? sender, RoutedEventArgs args)
    {
        if (_settings?.LayoutEditMode == true)
        {
            EditModeExitRequested?.Invoke(this);
        }
    }

    private void ApplyPhysicalBounds(ZzzOverlayPhysicalRect physicalBounds)
    {
        _applyingGeometry = true;
        try
        {
            Position = new PixelPoint((int)Math.Round(physicalBounds.X), (int)Math.Round(physicalBounds.Y));
            double scale = ResolveWindowScaling();
            ApplyMinimumSize(scale);
            Width = Math.Max(MinWidth, physicalBounds.Width / scale);
            Height = Math.Max(MinHeight, physicalBounds.Height / scale);
        }
        finally
        {
            _applyingGeometry = false;
        }
    }

    private ZzzOverlayPhysicalRect ReadPhysicalBounds()
    {
        double scale = ResolveWindowScaling();
        return new ZzzOverlayPhysicalRect(Position.X, Position.Y, Width * scale, Height * scale);
    }

    private ZzzOverlayPhysicalRect ClampToCurrentWorkArea(ZzzOverlayPhysicalRect candidate)
    {
        return ZzzOverlayPanelLayout.ClampToWorkArea(candidate, ResolveDisplayArea(candidate).WorkingArea);
    }

    private ZzzOverlayPhysicalRect ResolveFreeBounds(ZzzOverlayPanelSettings panel)
    {
        ZzzOverlayDisplayArea displayArea = ResolveFreeDisplayArea(panel);
        return ZzzOverlayPanelLayout.ClampToWorkArea(ZzzOverlayPanelLayout.ResolveFree(panel, displayArea), displayArea.WorkingArea);
    }

    private ZzzOverlayDisplayArea ResolveFreeDisplayArea(ZzzOverlayPanelSettings panel)
    {
        Screen? screen = !string.IsNullOrWhiteSpace(panel.FreeDisplayName)
            ? Screens.All.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayName, panel.FreeDisplayName, StringComparison.Ordinal))
            : null;
        if (screen is null)
        {
            screen = Screens.All.FirstOrDefault(candidate =>
            {
                PixelRect workArea = candidate.WorkingArea;
                return ZzzOverlayPanelLayout.MatchesFreeWorkAreaAnchor(
                    panel,
                    new ZzzOverlayPhysicalRect(workArea.X, workArea.Y, workArea.Width, workArea.Height));
            });
        }

        if (screen is null &&
            ZzzOverlayPanelLayout.TryGetFreeWorkAreaAnchor(panel, out ZzzOverlayPhysicalRect savedWorkArea))
        {
            PixelPoint anchor = new(
                (int)Math.Round(savedWorkArea.X + savedWorkArea.Width / 2d),
                (int)Math.Round(savedWorkArea.Y + savedWorkArea.Height / 2d));
            screen = Screens.ScreenFromPoint(anchor);
        }

        if (screen is null)
        {
            PixelPoint legacyPosition = new((int)Math.Round(panel.X), (int)Math.Round(panel.Y));
            screen = Screens.ScreenFromPoint(legacyPosition) ?? Screens.ScreenFromWindow(this) ?? Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        }

        return CreateDisplayArea(screen);
    }

    private ZzzOverlayDisplayArea ResolveDisplayArea(ZzzOverlayPhysicalRect candidate)
    {
        PixelPoint candidateOrigin = new((int)Math.Round(candidate.X), (int)Math.Round(candidate.Y));
        Screen? screen = Screens.ScreenFromPoint(candidateOrigin) ?? Screens.ScreenFromWindow(this) ?? Screens.ScreenFromPoint(Position) ?? Screens.Primary;
        return CreateDisplayArea(screen);
    }

    private ZzzOverlayDisplayArea CreateDisplayArea(Screen? screen)
    {
        if (screen is not null)
        {
            PixelRect area = screen.WorkingArea;
            return new ZzzOverlayDisplayArea(
                screen.DisplayName,
                new ZzzOverlayPhysicalRect(area.X, area.Y, area.Width, area.Height),
                screen.Scaling);
        }

        double scale = ResolveWindowScaling();
        return new ZzzOverlayDisplayArea(
            null,
            new ZzzOverlayPhysicalRect(0d, 0d, Math.Max(1d, Bounds.Width * scale), Math.Max(1d, Bounds.Height * scale)),
            scale);
    }

    private void ApplyMinimumSize(double scaling)
    {
        double minWidth = _panel?.Id == "log" ? 320d : 260d;
        double minHeight = _panel?.Id == "log" ? 130d : 100d;
        double scale = Math.Max(0.5d, scaling);
        MinWidth = minWidth / scale;
        MinHeight = minHeight / scale;
    }

    private void ReapplyGeometryForCurrentScaling()
    {
        if (!_closed && !_userManipulating && !_applyingGeometry && _panel is not null && _settings is not null && _gameWindow is not null)
        {
            ApplyConfiguration(_panel, _settings, _gameWindow, forceGeometry: true);
        }
    }

    private void QueueScalingRefresh()
    {
        if (_closed || _scalingRefreshQueued)
        {
            return;
        }

        _scalingRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scalingRefreshQueued = false;
            if (_closed)
            {
                return;
            }

            ReapplyGeometryForCurrentScaling();
        });
    }

    private double ResolveWindowScaling() => Math.Max(0.5d, DesktopScaling);

    private static uint ResolveDisplayDpi(ZzzOverlayDisplayArea displayArea) =>
        (uint)Math.Max(1d, Math.Round(displayArea.EffectiveScaling * 96d));

    private void ApplyNativeStyle()
    {
        if (_settings is not null)
        {
            ZzzOverlayNativeWindow.Apply(this, _settings.ClickThrough && !_settings.LayoutEditMode, _settings.PreventCapture);
        }
    }

    private static IBrush ParseBrush(string? color, Color fallback) =>
        Color.TryParse(color, out Color parsed) ? new SolidColorBrush(parsed) : new SolidColorBrush(fallback);
}
