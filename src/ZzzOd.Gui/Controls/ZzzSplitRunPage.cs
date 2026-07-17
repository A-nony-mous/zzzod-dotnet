using Avalonia.Controls;
using Avalonia.Layout;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

internal class ZzzSplitRunPage : UserControl, IZzzPageLifecycle
{
    private readonly Control _left;
    private readonly ZzzRunPanel _runPanel;

    public ZzzSplitRunPage(Control left, ZzzRunPanel runPanel)
    {
        _left = left;
        _runPanel = runPanel;

        Grid grid = new()
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(388, GridUnitType.Pixel),
            ],
            RowDefinitions = [new RowDefinition(1, GridUnitType.Star)],
            Margin = new Avalonia.Thickness(24, 12, 24, 24),
            ColumnSpacing = 16,
        };
        ContentControl leftContent = new()
        {
            Content = _left,
            MinWidth = 420,
        };
        ContentControl rightContent = new()
        {
            Content = _runPanel,
        };
        Grid.SetColumn(leftContent, 0);
        Grid.SetColumn(rightContent, 1);
        grid.Children.Add(leftContent);
        grid.Children.Add(rightContent);
        Content = grid;
    }

    public Control LeftContent => _left;

    public ZzzRunPanel RunPanel => _runPanel;

    public void OnPageShown()
    {
        if (_left is IZzzPageLifecycle leftLifecycle)
        {
            leftLifecycle.OnPageShown();
        }

        _runPanel.OnPageShown();
    }

    public void OnPageHidden()
    {
        if (_left is IZzzPageLifecycle leftLifecycle)
        {
            leftLifecycle.OnPageHidden();
        }

        _runPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        if (_left is IZzzPageLifecycle leftLifecycle)
        {
            leftLifecycle.OnPageLeave();
        }

        _runPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        if (_left is IZzzPageLifecycle leftLifecycle)
        {
            leftLifecycle.DisposePage();
        }

        _runPanel.DisposePage();
    }
}

