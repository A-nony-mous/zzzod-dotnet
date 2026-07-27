using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierDevtoolsPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private static readonly string[] Headers = ["图像分析", "模板管理", "画面管理", "代理人模板生成", "截图助手", "指令调试"];
    private readonly Control[] _pages;
    private readonly TabItem[] _tabs;
    private readonly TabControl _pivot;
    private Control? _activePage;
    private bool _activePageIsShown;
    private bool _isShown;

    public FrontierDevtoolsPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent, IZzzScreenManageService screenManageService, IZzzImageAnalysisService imageAnalysisService)
    {
        _pages =
        [
            new FrontierImageAnalysisPage(backend, imageAnalysisService),
            new FrontierTemplateHelperPage(backend),
            new FrontierScreenManagePage(screenManageService),
            new FrontierAgentTemplateGeneratorPage(backend),
            new FrontierScreenshotHelperPage(backend, runIntent),
            new FrontierOperationDebugPage(backend, runIntent),
        ];

        AvaloniaXamlLoader.Load(this);
        _pivot = Required<TabControl>("DevtoolsPivot");
        _tabs =
        [
            Required<TabItem>("ImageAnalysisTab"),
            Required<TabItem>("TemplateHelperTab"),
            Required<TabItem>("ScreenManageTab"),
            Required<TabItem>("AgentTemplateTab"),
            Required<TabItem>("ScreenshotHelperTab"),
            Required<TabItem>("OperationDebugTab"),
        ];

        Required<FAFrame>("ImageAnalysisFrame").Content = _pages[0];
        Required<FAFrame>("TemplateHelperFrame").Content = _pages[1];
        Required<FAFrame>("ScreenManageFrame").Content = _pages[2];
        Required<FAFrame>("AgentTemplateFrame").Content = _pages[3];
        Required<FAFrame>("ScreenshotHelperFrame").Content = _pages[4];
        Required<FAFrame>("OperationDebugFrame").Content = _pages[5];

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
