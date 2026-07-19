using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

public sealed partial class ZzzPageStackHost : UserControl, IZzzPageLifecycle
{
    private readonly Control _rootContent;
    private readonly FAFrame _frame;
    private Control? _secondaryContent;

    public ZzzPageStackHost()
    {
        throw new InvalidOperationException("ZzzPageStackHost 必须提供根内容。");
    }

    public ZzzPageStackHost(Control rootContent)
    {
        _rootContent = rootContent;
        AvaloniaXamlLoader.Load(this);
        _frame = this.FindControl<FAFrame>("PageFrame")
            ?? throw new InvalidOperationException("ZzzPageStackHost 缺少 Frame。");
        _frame.Content = rootContent;
    }

    public event EventHandler? BackNavigationStateChanged;

    public FAFrame FAFrame => _frame;

    public bool CanGoBack => _secondaryContent is not null;

    public void PushSecondary(Control content)
    {
        Deactivate(CurrentContent);
        DisposeSecondary();
        _secondaryContent = content;
        _frame.Content = content;
        Activate(content);
        BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack()
    {
        if (_secondaryContent is null)
        {
            return;
        }

        Deactivate(_secondaryContent);
        DisposeSecondary();
        _frame.Content = _rootContent;
        Activate(_rootContent);
        BackNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnPageShown() => Activate(CurrentContent);

    public void OnPageHidden()
    {
        if (CurrentContent is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageHidden();
        }
    }

    public void OnPageLeave()
    {
        if (CurrentContent is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageLeave();
        }
    }

    public void DisposePage()
    {
        DisposeSecondary();
        if (_rootContent is IZzzPageLifecycle lifecycle)
        {
            lifecycle.DisposePage();
        }
    }

    private Control CurrentContent => _secondaryContent ?? _rootContent;

    private static void Activate(Control content)
    {
        if (content is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageShown();
        }
    }

    private static void Deactivate(Control content)
    {
        if (content is IZzzPageLifecycle lifecycle)
        {
            lifecycle.OnPageLeave();
            lifecycle.OnPageHidden();
        }
    }

    private void DisposeSecondary()
    {
        Control? secondary = _secondaryContent;
        if (secondary is null)
        {
            return;
        }

        _secondaryContent = null;
        if (secondary is IZzzPageLifecycle lifecycle)
        {
            lifecycle.DisposePage();
        }
    }
}

