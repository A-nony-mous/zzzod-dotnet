using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class MixedShellWindow : MainWindow
{
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
