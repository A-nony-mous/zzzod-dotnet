using Avalonia.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Architecture;

public abstract class ZzzPageView : UserControl, IZzzPageLifecycle
{
    private readonly IZzzPageLifecycle _lifecycle;

    protected ZzzPageView(ZzzPageViewModel viewModel)
    {
        ViewModel = viewModel;
        _lifecycle = viewModel;
        DataContext = viewModel;
    }

    public ZzzPageViewModel ViewModel { get; }

    public void OnPageLeave() => _lifecycle.OnPageLeave();

    public void OnPageShown() => _lifecycle.OnPageShown();

    public void OnPageHidden() => _lifecycle.OnPageHidden();

    public void DisposePage() => _lifecycle.DisposePage();
}
