using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.GameAssistant;

internal sealed partial class ZzzGameAssistantPage : UserControl, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private static readonly string[] Headers = ["战斗助手", "委托助手"];
    private readonly ZzzBattleAssistantPage _battlePage;
    private readonly ZzzCommissionAssistantPage _commissionPage;
    private readonly TabView _pivot;
    private readonly TabViewItem _battleTab;
    private readonly TabViewItem _commissionTab;
    private Control? _activePage;
    private bool _activePageIsShown;
    private bool _isShown;

    public ZzzGameAssistantPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _battlePage = new ZzzBattleAssistantPage(backend, runIntent);
        _commissionPage = new ZzzCommissionAssistantPage(backend, runIntent);
        AvaloniaXamlLoader.Load(this);
        _pivot = this.FindControl<TabView>("AssistantPivot")
            ?? throw new InvalidOperationException("游戏助手缺少 TabView。");
        _battleTab = this.FindControl<TabViewItem>("BattleTab")
            ?? throw new InvalidOperationException("游戏助手缺少战斗助手 TabViewItem。");
        _commissionTab = this.FindControl<TabViewItem>("CommissionTab")
            ?? throw new InvalidOperationException("游戏助手缺少委托助手 TabViewItem。");
        Frame battleFrame = this.FindControl<Frame>("BattleFrame")
            ?? throw new InvalidOperationException("游戏助手缺少战斗助手 Frame。");
        Frame commissionFrame = this.FindControl<Frame>("CommissionFrame")
            ?? throw new InvalidOperationException("游戏助手缺少委托助手 Frame。");
        battleFrame.Content = _battlePage;
        commissionFrame.Content = _commissionPage;

        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        _pivot.SelectedIndex = string.Equals(evidenceTab, Headers[1], StringComparison.Ordinal) ? 1 : 0;
        _pivot.SelectionChanged += OnPivotSelectionChanged;
    }

    public IReadOnlyList<string> ItemHeaders => Headers;

    public string SelectedHeader => Headers[_pivot.SelectedIndex is 1 ? 1 : 0];

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

    public bool FocusSegment(int index)
    {
        TabViewItem? item = index switch
        {
            0 => _battleTab,
            1 => _commissionTab,
            _ => null,
        };
        return item?.Focus(NavigationMethod.Tab) == true;
    }

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
        _battlePage.DisposePage();
        _commissionPage.DisposePage();
        _activePage = null;
        _activePageIsShown = false;
    }

    private void OnPivotSelectionChanged(object? sender, SelectionChangedEventArgs args) => ActivateSelectedPage();

    private void ActivateSelectedPage()
    {
        Control next = _pivot.SelectedIndex is 1 ? _commissionPage : _battlePage;
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
}

