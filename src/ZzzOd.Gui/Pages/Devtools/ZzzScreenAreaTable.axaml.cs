using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ZzzOd.Gui.Pages.Devtools;

internal sealed partial class ZzzScreenAreaTable : UserControl
{
    public ZzzScreenAreaTable()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public event EventHandler<ZzzScreenAreaRow>? RowSelected;

    private void OnSelectClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ZzzScreenAreaRow row })
        {
            RowSelected?.Invoke(this, row);
        }
    }
}
