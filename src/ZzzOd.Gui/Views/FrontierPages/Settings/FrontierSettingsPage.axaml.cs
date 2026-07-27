using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Resources;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Settings;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class FrontierSettingsPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private static readonly string[] Headers = ["游戏设置", "Overlay", "资源下载", "脚本环境", "通知设置", "自定义设置"];
    private readonly Control[] _pages;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _ownsInputMonitor;
    private readonly TabItem[] _tabs;
    private readonly TabControl _pivot;
    private Control? _activePage;
    private bool _activePageIsShown;
    private bool _isShown;

    public FrontierSettingsPage(
        IZzzAppBackend backend,
        ZzzOverlayController overlayController,
        ZzzLauncherMediaService mediaService,
        IZzzResourceDownloadService resourceDownloadService,
        IZzzPushNotificationService pushNotificationService,
        ZzzGlobalInputMonitor? inputMonitor = null,
        IZzzEnvironmentRuntimeCoordinator? environmentRuntimeCoordinator = null,
        ZzzGuiOperationTracker? operations = null)
    {
        ZzzGlobalInputMonitor monitor = inputMonitor ?? new ZzzGlobalInputMonitor();
        _inputMonitor = monitor;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _ownsInputMonitor = inputMonitor is null;
        _pages =
        [
            new FrontierGameSettingsPage(backend, inputMonitor: monitor, operations: _operations),
            new FrontierOverlaySettingsPage(backend, overlayController, _operations),
            new FrontierResourceDownloadPage(backend, resourceDownloadService, _operations),
            new FrontierEnvironmentSettingsPage(backend, monitor, environmentRuntimeCoordinator, _operations),
            new FrontierPushSettingsPage(backend, pushNotificationService, _operations),
            new FrontierCustomSettingsPage(backend, mediaService, _operations),
        ];

        AvaloniaXamlLoader.Load(this);
        _pivot = Required<TabControl>("SettingsPivot");
        _tabs =
        [
            Required<TabItem>("GameTab"),
            Required<TabItem>("OverlayTab"),
            Required<TabItem>("ResourceDownloadTab"),
            Required<TabItem>("EnvironmentTab"),
            Required<TabItem>("PushTab"),
            Required<TabItem>("CustomTab"),
        ];

        Required<FAFrame>("GameFrame").Content = _pages[0];
        Required<FAFrame>("OverlayFrame").Content = _pages[1];
        Required<FAFrame>("ResourceDownloadFrame").Content = _pages[2];
        Required<FAFrame>("EnvironmentFrame").Content = _pages[3];
        Required<FAFrame>("PushFrame").Content = _pages[4];
        Required<FAFrame>("CustomFrame").Content = _pages[5];

        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        int selectedIndex = Array.FindIndex(Headers, header => string.Equals(header, evidenceTab, StringComparison.Ordinal));
        _pivot.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _pivot.SelectionChanged += OnPivotSelectionChanged;
    }

    public IReadOnlyList<string> ItemHeaders => Headers;

    public string SelectedHeader => Headers[SelectedIndex];

    public string NavigationTargetKind => nameof(TabControl);

    internal bool ActiveChildIsShown => _activePageIsShown;

    public event EventHandler? BackNavigationStateChanged
    {
        add { }
        remove { }
    }

    public bool CanGoBack => false;

    public bool SelectByHeader(string header)
    {
        int index = Array.FindIndex(Headers, candidate => string.Equals(candidate, header, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _pivot.SelectedIndex = index;
        ActivateSelectedPage();
        return true;
    }

    public bool FocusSegment(int index) => index >= 0 && index < _tabs.Length && _tabs[index].Focus(NavigationMethod.Tab);

    public void GoBack()
    {
    }

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings", "activate-settings");
        _isShown = true;
        try
        {
            ActivateSelectedPage();
            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            throw;
        }
    }

    public void CancelPageOperations(string reason)
    {
        if (_activePage is IZzzPageLifecycle lifecycle)
        {
            lifecycle.CancelPageOperations(reason);
        }
    }

    public void OnPageLeave()
    {
        if (_activePage is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageLeave();
        }
    }

    public void OnPageHidden()
    {
        _isShown = false;
        if (_activePageIsShown && _activePage is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageHidden();
        }

        _activePageIsShown = false;
    }

    public void DisposePage()
    {
        _pivot.SelectionChanged -= OnPivotSelectionChanged;
        foreach (Control page in _pages)
        {
            if (page is IZzzPageLifecycle lifecycle)
            {
                lifecycle.DisposePage();
            }
        }

        _activePage = null;
        _activePageIsShown = false;
        if (_ownsInputMonitor)
        {
            _inputMonitor.Dispose();
        }
    }

    private int SelectedIndex => _pivot.SelectedIndex >= 0 && _pivot.SelectedIndex < _pages.Length ? _pivot.SelectedIndex : 0;

    private void OnPivotSelectionChanged(object? sender, SelectionChangedEventArgs args) => ActivateSelectedPage();

    private void ActivateSelectedPage()
    {
        Control next = _pages[SelectedIndex];
        if (ReferenceEquals(next, _activePage))
        {
            if (_isShown && !_activePageIsShown && next is IZzzPageLifecycle activeLifecycle)
            {
                activeLifecycle.OnPageShown();
                _activePageIsShown = true;
            }

            return;
        }

        if (_activePageIsShown && _activePage is IZzzPageLifecycle previous)
        {
            previous.CancelPageOperations("settings-tab-changed");
            previous.OnPageLeave();
            previous.OnPageHidden();
        }

        _activePage = next;
        _activePageIsShown = false;
        if (_isShown && next is IZzzPageLifecycle current)
        {
            current.OnPageShown();
            _activePageIsShown = true;
        }
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"设置容器缺少 {name}。");
}
