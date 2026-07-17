using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Devtools;

internal sealed partial class ZzzDevtoolsPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private static readonly string[] Headers = ["图像分析", "模板管理", "画面管理", "代理人模板生成", "截图助手", "指令调试"];
    private readonly Control[] _pages;
    private readonly TabViewItem[] _tabs;
    private readonly TabView _pivot;
    private Control? _activePage;
    private bool _activePageIsShown;
    private bool _isShown;

    public ZzzDevtoolsPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent, IZzzScreenManageService screenManageService, IZzzImageAnalysisService imageAnalysisService)
    {
        _pages =
        [
            new ZzzImageAnalysisPage(backend, imageAnalysisService),
            new ZzzTemplateHelperAxamlPage(backend),
            new ZzzScreenManagePage(screenManageService),
            new ZzzAgentTemplateGeneratorPage(backend),
            new ZzzScreenshotHelperAxamlPage(backend, runIntent),
            new ZzzOperationDebugAxamlPage(backend, runIntent),
        ];

        AvaloniaXamlLoader.Load(this);
        _pivot = Required<TabView>("DevtoolsPivot");
        _tabs =
        [
            Required<TabViewItem>("ImageAnalysisTab"),
            Required<TabViewItem>("TemplateHelperTab"),
            Required<TabViewItem>("ScreenManageTab"),
            Required<TabViewItem>("AgentTemplateTab"),
            Required<TabViewItem>("ScreenshotHelperTab"),
            Required<TabViewItem>("OperationDebugTab"),
        ];

        Required<Frame>("ImageAnalysisFrame").Content = _pages[0];
        Required<Frame>("TemplateHelperFrame").Content = _pages[1];
        Required<Frame>("ScreenManageFrame").Content = _pages[2];
        Required<Frame>("AgentTemplateFrame").Content = _pages[3];
        Required<Frame>("ScreenshotHelperFrame").Content = _pages[4];
        Required<Frame>("OperationDebugFrame").Content = _pages[5];

        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        int selectedIndex = Array.FindIndex(Headers, header => string.Equals(header, evidenceTab, StringComparison.Ordinal));
        _pivot.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _pivot.SelectionChanged += OnPivotSelectionChanged;
    }

    public IReadOnlyList<string> ItemHeaders => Headers;

    public string SelectedHeader => Headers[SelectedIndex];

    public string NavigationTargetKind => nameof(TabView);

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
        _isShown = true;
        ActivateSelectedPage();
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
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"开发工具容器缺少 {name}。");
}

