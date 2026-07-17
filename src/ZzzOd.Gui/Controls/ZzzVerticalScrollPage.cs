using Avalonia.Controls;
using Avalonia.Layout;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

public class ZzzVerticalScrollPage : UserControl, IZzzPageLifecycle
{
    private readonly Func<Control> _contentFactory;
    private readonly Func<Control?>? _fixedTopFactory;
    private bool _initialized;

    public ZzzVerticalScrollPage(Func<Control> contentFactory, Func<Control?>? fixedTopFactory = null)
    {
        _contentFactory = contentFactory;
        _fixedTopFactory = fixedTopFactory;
    }

    public virtual void OnPageShown()
    {
        EnsureInitialized();
    }

    public virtual void OnPageHidden()
    {
    }

    public virtual void OnPageLeave()
    {
    }

    public virtual void DisposePage()
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        StackPanel root = new()
        {
            Spacing = 0,
        };

        Control? fixedTop = _fixedTopFactory?.Invoke();
        if (fixedTop is not null)
        {
            Border topWrapper = new()
            {
                Padding = new Avalonia.Thickness(16, 12, 16, 0),
                Child = fixedTop,
            };
            root.Children.Add(topWrapper);
        }

        Border contentWrapper = new()
        {
            Padding = new Avalonia.Thickness(16, 12, 16, 16),
            Child = _contentFactory(),
        };
        ScrollViewer scrollViewer = new()
        {
            Content = contentWrapper,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        root.Children.Add(scrollViewer);
        Content = root;
        _initialized = true;
    }

    public static StackPanel CreateStack(double spacing = 10) => new()
    {
        Spacing = spacing,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}

