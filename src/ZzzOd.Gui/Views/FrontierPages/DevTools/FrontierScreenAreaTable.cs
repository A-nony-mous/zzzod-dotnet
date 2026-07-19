using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using ZzzOd.Gui.Pages.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierScreenAreaTable : UserControl
{
    public FrontierScreenAreaTable()
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
