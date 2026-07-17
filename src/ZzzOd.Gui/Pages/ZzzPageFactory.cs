using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.AppHost.Resources;
using ZzzOd.AppHost.Notifications;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Pages.Accounts;
using ZzzOd.Gui.Pages.ApplicationSettings;
using ZzzOd.Gui.Pages.Devtools;
using ZzzOd.Gui.Pages.GameAssistant;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Pages.Home;
using ZzzOd.Gui.Pages.OneDragon;
using ZzzOd.Gui.Pages.Settings;
using ZzzOd.Gui.Pages.Standalone;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages;

internal sealed class ZzzPageFactory
{
    private readonly IZzzAppBackend _backend;
    private readonly ZzzOverlayController _overlayController;
    private readonly ZzzLauncherMediaService _mediaService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly ZzzNoticeService _noticeService;
    private readonly ZzzDashboardReadinessService _readinessService;
    private readonly ZzzShellNavigationService _navigation;
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly IZzzDialogService _dialogService;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly IZzzEnvironmentRuntimeCoordinator _environmentRuntimeCoordinator;
    private readonly IZzzResourceDownloadService _resourceDownloadService;
    private readonly IZzzPushNotificationService _pushNotificationService;
    private readonly IZzzScreenManageService _screenManageService;
    private readonly IZzzImageAnalysisService _imageAnalysisService;

    public ZzzPageFactory(
        IZzzAppBackend backend,
        ZzzOverlayController overlayController,
        ZzzLauncherMediaService mediaService,
        ZzzGuiOperationTracker operations,
        ZzzNoticeService noticeService,
        ZzzDashboardReadinessService readinessService,
        ZzzShellNavigationService navigation,
        ZzzGuiRunIntentService runIntent,
        IZzzDialogService dialogService,
        ZzzGlobalInputMonitor inputMonitor,
        IZzzEnvironmentRuntimeCoordinator environmentRuntimeCoordinator,
        IZzzResourceDownloadService resourceDownloadService,
        IZzzPushNotificationService pushNotificationService,
        IZzzScreenManageService screenManageService,
        IZzzImageAnalysisService imageAnalysisService)
    {
        _backend = backend;
        _overlayController = overlayController;
        _mediaService = mediaService;
        _operations = operations;
        _noticeService = noticeService;
        _readinessService = readinessService;
        _navigation = navigation;
        _runIntent = runIntent;
        _dialogService = dialogService;
        _inputMonitor = inputMonitor;
        _environmentRuntimeCoordinator = environmentRuntimeCoordinator;
        _resourceDownloadService = resourceDownloadService;
        _pushNotificationService = pushNotificationService;
        _screenManageService = screenManageService;
        _imageAnalysisService = imageAnalysisService;
    }

    public Control CreateHomePage() => new ZzzHomePage(_backend, _mediaService, _noticeService, _readinessService, _navigation, _runIntent, _dialogService, _operations);

    public Control CreateGameAssistantPage() => new ZzzGameAssistantPage(_backend, _runIntent);

    public Control CreateOneDragonPage() => new ZzzOneDragonPage(_backend, _runIntent, _operations);

    public Control CreateStandalonePage()
    {
        ZzzStandaloneAppRunPage runPage = new(_backend, _runIntent, _operations);
        ZzzPivotPage container = new([new("应用运行", runPage)]);
        runPage.SecondaryPageRequested += (_, content) => container.PushSecondary("应用设置", content);
        return container;
    }

    public Control CreateDiagnosticsPage() => new ZzzDevtoolsPage(_backend, _runIntent, _screenManageService, _imageAnalysisService);

    public Control CreateDevtoolsPage() => new ZzzDevtoolsPage(_backend, _runIntent, _screenManageService, _imageAnalysisService);

    public Control CreateAccountsPage() => new ZzzAccountsPage(_backend, _operations);

    public Control CreateSettingsPage() => new ZzzSettingsPage(
        _backend,
        _overlayController,
        _mediaService,
        _resourceDownloadService,
        _pushNotificationService,
        _inputMonitor,
        _environmentRuntimeCoordinator,
        _operations);

    private Control CreateInstancesPage() => new ZzzVerticalScrollPage(() =>
    {
        StackPanel stack = Stack();
        ZzzRunStatusDto run = _backend.GetCurrentRun().Value ?? new ZzzRunStatusDto(ZzzRunState.Idle);
        bool canSwitch = !IsRunActive(run.State);
        if (!canSwitch)
        {
            stack.Children.Add(BodyText($"当前状态为 {run.State}，运行中不能切换实例。"));
        }

        foreach (ZzzInstanceDto instance in _backend.GetInstances().Value ?? [])
        {
            Button button = new()
            {
                Content = $"{instance.Index:00} {(instance.Active ? "当前" : "启用")} {instance.Name}",
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 220,
                IsEnabled = canSwitch || instance.Active,
            };
            button.Click += (_, _) =>
            {
                _backend.ActivateInstance(instance.Index);
                button.Content = $"{instance.Index:00} 当前 {instance.Name}";
            };
            stack.Children.Add(button);
        }

        return stack;
    });

    private Control CreateScreenshotToolPage() => new ZzzVerticalScrollPage(() =>
    {
        TextBlock output = BodyText(string.Empty);
        Image preview = new()
        {
            MaxWidth = 720,
            MaxHeight = 420,
            Stretch = Stretch.Uniform,
        };
        Button window = new() { Content = "检查窗口", HorizontalAlignment = HorizontalAlignment.Left };
        Button screenshot = new() { Content = "获取截图", HorizontalAlignment = HorizontalAlignment.Left };
        window.Click += (_, _) =>
        {
            ZzzBackendResult<ZzzWindowStatusDto> result = _backend.GetWindow();
            output.Text = result.Success ? $"{result.Value}" : result.Error;
        };
        screenshot.Click += (_, _) =>
        {
            ZzzBackendResult<ZzzScreenshotDto> result = _backend.GetScreenshot();
            if (result.Success && result.Value is not null)
            {
                preview.Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(result.Value.Bytes));
                output.Text = $"截图已获取：{result.Value.ContentType}，{result.Value.Bytes.Length} bytes";
                return;
            }

            preview.Source = null;
            output.Text = result.Error;
        };
        StackPanel stack = Stack();
        stack.Children.Add(window);
        stack.Children.Add(screenshot);
        stack.Children.Add(output);
        stack.Children.Add(preview);
        return stack;
    });

    private static Control Placeholder(string message) => new ZzzVerticalScrollPage(() =>
    {
        StackPanel stack = Stack();
        stack.Children.Add(BodyText(message));
        return stack;
    });

    private static bool IsRunActive(ZzzRunState state) =>
        state is ZzzRunState.Starting or ZzzRunState.Running or ZzzRunState.Paused or ZzzRunState.Stopping;

    private static StackPanel Stack() => ZzzVerticalScrollPage.CreateStack();

    private static TextBlock BodyText(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
    };
}
