using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views;

public sealed partial class FrontierShellWindow : FAAppWindow
{
    private readonly ContentControl _mainViewHost;
    private readonly FrontierStartupSplashScreen? _startupSplash;
    private Func<FrontierMainView>? _mainViewFactory;
    private FrontierMainView? _mainView;
    private Application? _themeApplication;

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
        : this(
            services,
            navigationRegistry,
            pageLifecycle,
            navigationService,
            shellViewModel,
            windowRuntime,
            runRoot,
            enableSplash: true)
    {
    }

    internal FrontierShellWindow(
        IServiceProvider services,
        ZzzNavigationRegistry navigationRegistry,
        ZzzPageLifecycleService pageLifecycle,
        ZzzShellNavigationService navigationService,
        ZzzShellViewModel shellViewModel,
        IZzzShellWindowRuntime windowRuntime,
        ZzzRunRoot runRoot,
        bool enableSplash)
    {
        DataContext = shellViewModel;
        AvaloniaXamlLoader.Load(this);

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 48;
        ApplyWindowMaterial();

        _themeApplication = Application.Current;
        if (_themeApplication is not null)
        {
            _themeApplication.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }

        _mainViewHost = this.FindControl<ContentControl>("MainViewHost")
            ?? throw new InvalidOperationException("前卫 Shell 缺少 MainView Host。");
        _mainViewFactory = () => new FrontierMainView(
                services,
                navigationRegistry,
                pageLifecycle,
                navigationService,
                shellViewModel,
                windowRuntime,
                runRoot,
                this);
        if (enableSplash)
        {
            _startupSplash = new FrontierStartupSplashScreen(
                runRoot.Path,
                CreateMainView,
                StartInitialNavigation);
            SplashScreen = _startupSplash;
        }

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
        ApplyWindowMaterial();
        if (((ZzzShellViewModel)DataContext!).ConsumeStartupError() is { Length: > 0 } error)
        {
            _mainView?.ShowToast("界面配置错误", error, TimeSpan.FromSeconds(8), FAInfoBarSeverity.Error);
        }
    }

    private void CreateMainView()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_mainView is not null || _mainViewFactory is null)
        {
            return;
        }

        FrontierMainView mainView = _mainViewFactory();
        _mainViewFactory = null;
        _mainView = mainView;
        _mainViewHost.Content = mainView;
    }

    private void StartInitialNavigation()
    {
        Dispatcher.UIThread.VerifyAccess();
        _mainView?.StartInitialNavigation();
    }

    internal void InitializeMainViewForTesting(Action<FrontierMainView>? configure = null)
    {
        CreateMainView();
        if (_mainView is not null && configure is not null)
        {
            configure(_mainView);
        }

        StartInitialNavigation();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;
        if (_themeApplication is not null)
        {
            _themeApplication.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
            _themeApplication = null;
        }

        _mainViewFactory = null;
        _mainView?.Dispose();
        _mainView = null;
        _mainViewHost.Content = null;
        _startupSplash?.Dispose();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActualThemeVariantProperty)
        {
            ApplyWindowMaterial();
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs args) => ApplyWindowMaterial();

    private void ApplyWindowMaterial()
    {
        IBrush fallback = ResolveTransparencyFallback();
        TransparencyBackgroundFallback = fallback;

        if (ActualThemeVariant == FluentAvaloniaTheme.HighContrastTheme)
        {
            Background = fallback;
            TransparencyLevelHint = [WindowTransparencyLevel.None];
            return;
        }

        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur];
    }

    private IBrush ResolveTransparencyFallback()
    {
        if (TryGetResource("ZzzFrontierWindowBackgroundBrush", ActualThemeVariant, out object? resource)
            && resource is ISolidColorBrush brush)
        {
            Color color = brush.Color;
            if (color.A == byte.MaxValue && brush.Opacity == 1d)
            {
                return brush;
            }

            return new SolidColorBrush(Color.FromArgb(byte.MaxValue, color.R, color.G, color.B));
        }

        return ActualThemeVariant == ThemeVariant.Dark
            ? new SolidColorBrush(Color.Parse("#202020"))
            : new SolidColorBrush(Color.Parse("#F3F3F3"));
    }
}
