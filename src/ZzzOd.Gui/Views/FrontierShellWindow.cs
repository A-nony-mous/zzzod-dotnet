using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class FrontierShellWindow : Window
{
    private readonly FrontierMainView _mainView;

    public FrontierShellWindow()
    {
        throw new InvalidOperationException("FrontierShellWindow 必须通过应用宿主的依赖注入创建。");
    }

    [ActivatorUtilitiesConstructor]
    public FrontierShellWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot)
    {
        DataContext = shellViewModel;
        AvaloniaXamlLoader.Load(this);

        ContentControl host = this.FindControl<ContentControl>("MainViewHost")
            ?? throw new InvalidOperationException("前卫 Shell 缺少 MainView Host。");
        _mainView = new FrontierMainView(
            services,
            navigationRegistry,
            pageLifecycle,
            navigationService,
            shellViewModel,
            windowRuntime,
            runRoot,
            this);
        host.Content = _mainView;

        ZzzGuiEvidenceSelection evidence = ZzzGuiEvidenceSelection.FromEnvironment();
        if (evidence.Width is double width && evidence.Height is double height)
        {
            Width = Math.Max(MinWidth, width);
            Height = Math.Max(MinHeight, height);
        }

        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs args)
    {
        Opened -= OnOpened;
        if (((ZzzShellViewModel)DataContext!).ConsumeStartupError() is { Length: > 0 } error)
        {
            _mainView.ShowToast("界面配置错误", error, TimeSpan.FromSeconds(8), FAInfoBarSeverity.Error);
        }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;
        _mainView.Dispose();
    }
}
