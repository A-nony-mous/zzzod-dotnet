using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.PageModels.OneDragon;

namespace ZzzOd.Gui.Views.FrontierPages.OneDragon;

internal sealed partial class FrontierOneDragonPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private static readonly string[] Headers = ["一条龙运行", "体力计划", "预备编队", "灵敏度校准"];
    private readonly Control[] _pages;
    private readonly FrontierOneDragonRunPage _runPage;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ZzzPageStackHost _runPageStack;
    private readonly TabItem[] _tabs;
    private readonly TabControl _pivot;
    private Control? _activePage;
    private bool _activePageIsShown;
    private bool _isShown;

    public FrontierOneDragonPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent, ZzzGuiOperationTracker? operations = null)
    {
        _operations = operations ?? new ZzzGuiOperationTracker();
        _runPage = new FrontierOneDragonRunPage(backend, runIntent, _operations);
        _runPageStack = new ZzzPageStackHost(_runPage);
        _runPage.SecondaryPageRequested += OnSecondaryPageRequested;
        _runPageStack.BackNavigationStateChanged += OnRunBackNavigationStateChanged;
        _pages =
        [
            _runPageStack,
            new FrontierChargePlanPage(backend),
            new FrontierPredefinedTeamPage(backend, runIntent),
            new FrontierMouseSensitivityCheckerPage(backend, runIntent),
        ];

        AvaloniaXamlLoader.Load(this);
        _pivot = Required<TabControl>("OneDragonPivot");
        _tabs =
        [
            Required<TabItem>("RunTab"),
            Required<TabItem>("ChargePlanTab"),
            Required<TabItem>("PredefinedTeamTab"),
            Required<TabItem>("SensitivityTab"),
        ];

        Required<FAFrame>("RunFrame").Content = _pages[0];
        Required<FAFrame>("ChargePlanFrame").Content = _pages[1];
        Required<FAFrame>("PredefinedTeamFrame").Content = _pages[2];
        Required<FAFrame>("SensitivityFrame").Content = _pages[3];

        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        int selectedIndex = Array.FindIndex(Headers, header => string.Equals(header, evidenceTab, StringComparison.Ordinal));
        _pivot.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        _pivot.SelectionChanged += OnPivotSelectionChanged;
    }

    public IReadOnlyList<string> ItemHeaders => Headers;

    public string SelectedHeader => Headers[SelectedIndex];

    public string NavigationTargetKind => nameof(TabControl);

    internal bool ActiveChildIsShown => _activePageIsShown;

    public event EventHandler? BackNavigationStateChanged;

    public bool CanGoBack => SelectedIndex == 0 && _runPageStack.CanGoBack;

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
        if (SelectedIndex == 0)
        {
            _runPageStack.GoBack();
        }
    }

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("one-dragon", "activate-one-dragon");
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
        _runPage.SecondaryPageRequested -= OnSecondaryPageRequested;
        _runPageStack.BackNavigationStateChanged -= OnRunBackNavigationStateChanged;
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

    private void OnPivotSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        ActivateSelectedPage();
        BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSecondaryPageRequested(object? sender, Control content) => _runPageStack.PushSecondary(content);

    private void OnRunBackNavigationStateChanged(object? sender, EventArgs args) =>
        BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);

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
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"一条龙容器缺少 {name}。");
}
