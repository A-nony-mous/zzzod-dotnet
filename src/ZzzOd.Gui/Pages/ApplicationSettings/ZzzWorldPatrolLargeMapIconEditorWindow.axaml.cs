using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed partial class ZzzWorldPatrolLargeMapIconEditorWindow : Window
{
    private readonly ListBox _iconList;
    private readonly CommandBarButton _setTeleportButton;
    private readonly CommandBarButton _deleteIconButton;
    private readonly ContentDialog _deleteDialog;
    private List<ZzzWorldPatrolEditableIcon> _icons;

    public ZzzWorldPatrolLargeMapIconEditorWindow(IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> icons)
    {
        AvaloniaXamlLoader.Load(this);
        _iconList = Required<ListBox>("IconList");
        _setTeleportButton = Required<CommandBarButton>("SetTeleportButton");
        _deleteIconButton = Required<CommandBarButton>("DeleteIconButton");
        _deleteDialog = Required<ContentDialog>("DeleteDialog");
        _icons = icons.Select(ToEditable).ToList();
        RefreshList();
        UpdateButtons();
    }

    public event Action<int>? IconSelected;

    public event Action<IReadOnlyList<ZzzWorldPatrolLargeMapIconDto>>? IconsSaved;

    public Func<ZzzWorldPatrolRoutePositionDto?>? CurrentPositionRequested { get; set; }

    public void HighlightIcon(int index)
    {
        if (index < 0 || index >= _icons.Count)
        {
            _iconList.SelectedIndex = -1;
            return;
        }

        _iconList.SelectedIndex = index;
        _iconList.ScrollIntoView(index);
    }

    private void OnIconSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        UpdateButtons();
        if (_iconList.SelectedIndex >= 0)
        {
            IconSelected?.Invoke(_iconList.SelectedIndex);
        }
    }

    private void OnSetTeleportClicked(object? sender, RoutedEventArgs args)
    {
        int index = _iconList.SelectedIndex;
        if (index < 0
            || index >= _icons.Count
            || CurrentPositionRequested?.Invoke() is not { } position)
        {
            return;
        }

        ZzzWorldPatrolEditableIcon icon = _icons[index];
        icon.TeleportX = position.X;
        icon.TeleportY = position.Y;
        RefreshList(index);
    }

    private async void OnDeleteIconClicked(object? sender, RoutedEventArgs args)
    {
        int index = _iconList.SelectedIndex;
        if (index < 0 || index >= _icons.Count)
        {
            return;
        }

        ZzzWorldPatrolEditableIcon icon = _icons[index];
        _deleteDialog.Content = $"确定要删除图标 \"{icon.IconName}\" ({icon.TemplateId}) 吗？\n此操作不可撤销。";
        if (await _deleteDialog.ShowAsync(this).ConfigureAwait(true) != ContentDialogResult.Primary)
        {
            return;
        }

        _icons.RemoveAt(index);
        RefreshList();
        UpdateButtons();
        IconsSaved?.Invoke(BuildDtos());
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs args)
    {
        IconsSaved?.Invoke(BuildDtos());
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs args) => Close();

    private IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> BuildDtos() => _icons.Select(icon =>
        new ZzzWorldPatrolLargeMapIconDto(
            icon.IconName,
            icon.TemplateId,
            new ZzzWorldPatrolRoutePositionDto((int)icon.LargeMapX, (int)icon.LargeMapY),
            new ZzzWorldPatrolRoutePositionDto((int)icon.TeleportX, (int)icon.TeleportY)))
        .ToArray();

    private void RefreshList(int selectedIndex = -1)
    {
        _iconList.ItemsSource = null;
        _iconList.ItemsSource = _icons;
        _iconList.SelectedIndex = selectedIndex;
    }

    private void UpdateButtons()
    {
        bool selected = _iconList.SelectedIndex >= 0;
        _setTeleportButton.IsEnabled = selected;
        _deleteIconButton.IsEnabled = selected;
    }

    private static ZzzWorldPatrolEditableIcon ToEditable(ZzzWorldPatrolLargeMapIconDto icon) => new()
    {
        IconName = icon.IconName,
        TemplateId = icon.TemplateId,
        LargeMapX = icon.LargeMapPosition.X,
        LargeMapY = icon.LargeMapPosition.Y,
        TeleportX = icon.TeleportPosition.X,
        TeleportY = icon.TeleportPosition.Y,
    };

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"图标编辑器缺少 {name}。");
}
