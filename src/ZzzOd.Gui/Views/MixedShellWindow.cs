using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class MixedShellWindow : MainWindow
{
    protected override string NavigationControlName => "MixedNavigation";
    protected override string ContentFrameControlName => "MixedContentFrame";
    protected override string TitleBarControlName => "MixedTitleBar";
    protected override string TitleBarIconControlName => "MixedTitleBarIcon";
    protected override string ToastBarControlName => "MixedToastBar";

    [ActivatorUtilitiesConstructor]
    public MixedShellWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot)
        : base(services, navigationRegistry, pageLifecycle, navigationService, shellViewModel, windowRuntime, runRoot, false)
    {
        AvaloniaXamlLoader.Load(this);
        InitializeShell(navigationRegistry, pageLifecycle, navigationService, runRoot);
    }
}
