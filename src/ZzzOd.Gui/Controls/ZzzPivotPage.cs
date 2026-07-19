using System.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using FluentAvalonia.UI.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

public class ZzzPivotPage : FATabView, IZzzPageLifecycle, IZzzShellBackNavigationHost, IZzzPivotNavigationHost
{
    private readonly IReadOnlyList<ZzzPivotPageItem> _items;
    private readonly IReadOnlyList<ZzzPageStackHost> _pageHosts;
    private readonly IReadOnlyList<FATabViewItem> _tabItems;
    private ZzzPageStackHost? _currentHost;
    private int _lastNotifiedIndex = int.MinValue;

    protected override Type StyleKeyOverride => typeof(FATabView);

    public ZzzPivotPage(IEnumerable<ZzzPivotPageItem> items)
    {
        _items = items.ToArray();
        _pageHosts = _items.Select(item => new ZzzPageStackHost(item.Content)).ToArray();
        foreach (ZzzPageStackHost host in _pageHosts)
        {
            host.BackNavigationStateChanged += OnHostBackNavigationStateChanged;
        }

        _tabItems = _items.Select((item, index) => new FATabViewItem
        {
            Header = item.Header,
            Content = _pageHosts[index],
            IsClosable = false,
            Focusable = true,
        }).ToArray();
        IsAddTabButtonVisible = false;
        CanDragTabs = false;
        CanReorderTabs = false;
        foreach (FATabViewItem tabItem in _tabItems)
        {
            ((IList)TabItems).Add(tabItem);
        }

        int initialIndex = 0;
        string? evidenceTab = ZzzGuiEvidenceSelection.FromEnvironment().Tab;
        if (!string.IsNullOrWhiteSpace(evidenceTab))
        {
            int index = Array.FindIndex(_items.ToArray(), item => string.Equals(item.Header, evidenceTab, StringComparison.Ordinal));
            if (index >= 0)
            {
                initialIndex = index;
            }
        }

        base.SelectedIndex = _items.Count == 0 ? -1 : initialIndex;
        _lastNotifiedIndex = base.SelectedIndex;
        base.SelectionChanged += (_, _) => ApplySelection(notifySelectionChanged: true);
    }

    public string NavigationTargetKind => nameof(FATabView);

    public IReadOnlyList<string> ItemHeaders => _items.Select(item => item.Header).ToArray();

    public IReadOnlyList<string> ItemAutomationNames => _items.Select(item => $"{item.Header} 选项卡").ToArray();

    public IReadOnlyList<bool> ItemFocusableStates => _tabItems.Select(item => item.Focusable).ToArray();

    public new int SelectedIndex
    {
        get => base.SelectedIndex;
        set
        {
            base.SelectedIndex = value;
            ApplySelection(notifySelectionChanged: true);
        }
    }

    public new event EventHandler? SelectionChanged;

    public event EventHandler? BackNavigationStateChanged;

    public bool CanGoBack => SelectedHost?.CanGoBack == true;

    public string? SelectedHeader => SelectedIndex < 0 || SelectedIndex >= _items.Count
        ? null
        : _items[SelectedIndex].Header;

    public Control? SelectedContent => SelectedIndex < 0 || SelectedIndex >= _items.Count
        ? null
        : _items[SelectedIndex].Content;

    private ZzzPageStackHost? SelectedHost => SelectedIndex < 0 || SelectedIndex >= _pageHosts.Count
        ? null
        : _pageHosts[SelectedIndex];

    public ContentControl FAFrame => SelectedHost?.FAFrame
        ?? throw new InvalidOperationException("当前 Pivot 没有可用页面。");

    public bool SelectByHeader(string header)
    {
        int index = Array.FindIndex(_items.ToArray(), item => string.Equals(item.Header, header, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    public bool FocusSegment(int index)
    {
        if (index < 0 || index >= _tabItems.Count)
        {
            return false;
        }

        bool canFocus = _tabItems[index].Focusable;
        if (canFocus)
        {
            _tabItems[index].Focus(NavigationMethod.Tab);
        }

        return canFocus;
    }

    public void PushSecondary(string title, Control content)
    {
        _ = title;
        SelectedHost?.PushSecondary(content);
    }

    public void GoBack() => SelectedHost?.GoBack();

    public void OnPageShown() => ActivateSelectedChild();

    public void OnPageHidden()
    {
        _currentHost?.OnPageHidden();
    }

    public void OnPageLeave()
    {
        _currentHost?.OnPageLeave();
    }

    public void DisposePage()
    {
        foreach (ZzzPageStackHost host in _pageHosts)
        {
            host.BackNavigationStateChanged -= OnHostBackNavigationStateChanged;
            host.DisposePage();
        }
    }

    private void ApplySelection(bool notifySelectionChanged)
    {
        ActivateSelectedChild();
        BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        if (notifySelectionChanged && _lastNotifiedIndex != SelectedIndex)
        {
            _lastNotifiedIndex = SelectedIndex;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ActivateSelectedChild()
    {
        ZzzPageStackHost? next = SelectedHost;
        if (next is null || ReferenceEquals(_currentHost, next))
        {
            return;
        }

        if (_currentHost is not null)
        {
            _currentHost.OnPageLeave();
            _currentHost.OnPageHidden();
        }

        _currentHost = next;
        next.OnPageShown();
    }

    private void OnHostBackNavigationStateChanged(object? sender, EventArgs args)
    {
        if (ReferenceEquals(sender, SelectedHost))
        {
            BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed record ZzzPivotPageItem(string Header, Control Content);

