using System.Globalization;
using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Controls;

internal sealed partial class ZzzRunPanel : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiRunIntentService? _runIntent;
    private readonly string? _fixedAppId;
    private readonly string? _fixedGroupId;
    private readonly Func<string?>? _appIdProvider;
    private readonly FAInfoBar _errorBar;
    private readonly TextBlock _stateText;
    private readonly FAComboBox _apps;
    private readonly Button _primaryButton;
    private readonly Button _stopButton;
    private readonly FASymbolIcon _primaryIcon;
    private readonly TextBlock _primaryLabel;
    private readonly TextBlock _stopLabel;
    private readonly Border _runOperationOverlay;
    private readonly ZzzLogDisplayCard _logCard;
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private string _startHotkey = string.Empty;
    private string _stopHotkey = string.Empty;
    private string _displayedApp = "-";
    private string _displayedInstance = "-";
    private string _displayedDuration = "-";
    private string _displayedLastStatus = "-";
    private string _primaryAction = "开始";
    private bool _stopActionAvailable;

    public ZzzRunPanel(
        IZzzAppBackend backend,
        string? fixedAppId = null,
        string? title = null,
        ZzzGuiRunIntentService? runIntent = null,
        Func<string?>? appIdProvider = null,
        string? fixedGroupId = null)
    {
        _backend = backend;
        _runIntent = runIntent;
        _fixedAppId = fixedAppId;
        _fixedGroupId = fixedGroupId;
        _appIdProvider = appIdProvider;
        AvaloniaXamlLoader.Load(this);
        _errorBar = this.FindControl<FAInfoBar>("RunErrorBar")
            ?? throw new InvalidOperationException("运行面缺少错误 InfoBar。");
        _stateText = this.FindControl<TextBlock>("StateText")
            ?? throw new InvalidOperationException("运行面缺少状态文本。");
        _apps = this.FindControl<FAComboBox>("AppCombo")
            ?? throw new InvalidOperationException("运行面缺少应用下拉框。");
        _primaryButton = this.FindControl<Button>("PrimaryButton")
            ?? throw new InvalidOperationException("运行面缺少开始按钮。");
        _stopButton = this.FindControl<Button>("StopButton")
            ?? throw new InvalidOperationException("运行面缺少停止按钮。");
        _primaryIcon = this.FindControl<FASymbolIcon>("PrimaryIcon")
            ?? throw new InvalidOperationException("运行面缺少主操作图标。");
        _primaryLabel = this.FindControl<TextBlock>("PrimaryLabel")
            ?? throw new InvalidOperationException("运行面缺少主操作文本。");
        _stopLabel = this.FindControl<TextBlock>("StopLabel")
            ?? throw new InvalidOperationException("运行面缺少停止文本。");
        _runOperationOverlay = this.FindControl<Border>("RunOperationOverlay")
            ?? throw new InvalidOperationException("运行面缺少操作遮罩。");
        ContentControl logHost = this.FindControl<ContentControl>("LogHost")
            ?? throw new InvalidOperationException("运行面缺少日志区域。");
        _logCard = new ZzzLogDisplayCard(backend);
        logHost.Content = _logCard;
        _apps.IsVisible = string.IsNullOrWhiteSpace(fixedAppId) && appIdProvider is null;
        _primaryButton.Click += OnPrimaryButtonClicked;
        _stopButton.Click += OnStopButtonClicked;
        RefreshApps();
        RefreshHotkeys();
        Refresh();
    }

    public string? SelectedAppId
    {
        get => _fixedAppId ?? _appIdProvider?.Invoke() ?? _apps.SelectedItem?.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(_fixedAppId) && _appIdProvider is null)
            {
                _apps.SelectedItem = value;
            }
        }
    }

    public string PrimaryActionText => _primaryAction;

    public bool StopActionEnabled => _stopButton.IsEnabled;

    public bool IsRunOperationPending => _runOperationOverlay.IsVisible;

    public string DisplayedApp => _displayedApp;

    public string DisplayedInstance => _displayedInstance;

    public string DisplayedDuration => _displayedDuration;

    public string DisplayedLastStatus => _displayedLastStatus;

    public ZzzLogDisplayCard LogCard => _logCard;

    public void RefreshState() => Refresh();

    public Task InvokePrimaryActionAsync() => OnPrimaryClickedAsync();

    public async Task InvokeStopActionAsync()
    {
        SetRunOperationPending(true);
        try
        {
            ZzzBackendResult<ZzzRunStatusDto> result = await _backend.StopRunAsync().ConfigureAwait(true);
            if (!result.Success || result.Value is null)
            {
                ShowError(result.Error ?? "停止失败。");
                return;
            }

            ApplyRun(result.Value);
        }
        finally
        {
            SetRunOperationPending(false);
        }
    }

    public void OnPageShown()
    {
        RefreshApps();
        RefreshHotkeys();
        StartRunEvents();
        _logCard.OnPageShown();
        if (_fixedAppId == ZzzApplicationIds.OneDragon && _runIntent?.ConsumeStartOneDragon() == true)
        {
            _ = StartRequestedRunAsync();
            return;
        }

        Refresh();
    }

    public void OnPageHidden()
    {
        StopRunEvents();
        _logCard.OnPageHidden();
    }

    public void OnPageLeave() => OnPageHidden();

    public void DisposePage()
    {
        StopRunEvents();
        _primaryButton.Click -= OnPrimaryButtonClicked;
        _stopButton.Click -= OnStopButtonClicked;
        _logCard.DisposePage();
    }

    private void RefreshApps()
    {
        if (!string.IsNullOrWhiteSpace(_fixedAppId) || _appIdProvider is not null)
        {
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzAppDto>> result = _backend.GetApps();
        if (!result.Success || result.Value is null)
        {
            _apps.ItemsSource = Array.Empty<string>();
            _apps.IsEnabled = false;
            ShowError(result.Error ?? "应用列表读取失败。");
            return;
        }

        string[] appIds = result.Value.Select(app => app.AppId).ToArray();
        object? selected = _apps.SelectedItem;
        _apps.ItemsSource = appIds;
        _apps.IsEnabled = appIds.Length > 0;
        _apps.SelectedItem = selected is string selectedApp && appIds.Contains(selectedApp, StringComparer.Ordinal)
            ? selectedApp
            : appIds.FirstOrDefault();
    }

    private void RefreshHotkeys()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("env");
        if (!result.Success || result.Value is null)
        {
            _startHotkey = string.Empty;
            _stopHotkey = string.Empty;
            ShowError(result.Error ?? "运行热键读取失败。");
            ApplyButtonLabels(PrimaryActionText);
            return;
        }

        _startHotkey = ReadHotkey(result.Value.Values, "key_start_running");
        _stopHotkey = ReadHotkey(result.Value.Values, "key_stop_running");
        ApplyButtonLabels(PrimaryActionText);
    }

    private async Task OnPrimaryClickedAsync()
    {
        ZzzBackendResult<ZzzRunStatusDto> current = _backend.GetCurrentRun();
        if (!current.Success || current.Value is null)
        {
			ShowError(current.Error ?? "运行状态读取失败。");
            return;
        }

        ZzzBackendResult<ZzzRunStatusDto> result;
        if (current.Value.State is ZzzRunState.Running)
        {
            result = _backend.PauseRun();
        }
        else if (current.Value.State is ZzzRunState.Paused)
        {
            result = _backend.ResumeRun();
        }
        else
        {
            string? appId = SelectedAppId;
            if (string.IsNullOrWhiteSpace(appId))
            {
				ShowError("未选择运行应用。");
                return;
            }

            SetRunOperationPending(true);
            try
            {
                result = await _backend.StartRunAsync(new ZzzStartRunRequest(appId, GroupId: _fixedGroupId)).ConfigureAwait(true);
            }
            finally
            {
                SetRunOperationPending(false);
            }
        }

        if (!result.Success || result.Value is null)
        {

            return;
        }

        ApplyRun(result.Value);
    }

    private async Task StartRequestedRunAsync()
    {
        SetRunOperationPending(true);
        ZzzBackendResult<ZzzRunStatusDto> result;
        try
        {
            result = await _backend
                .StartRunAsync(new ZzzStartRunRequest(ZzzApplicationIds.OneDragon, GroupId: _fixedGroupId))
                .ConfigureAwait(true);
        }
        finally
        {
            SetRunOperationPending(false);
        }
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "启动失败。");
            return;
        }

        ApplyRun(result.Value);
    }

    private void Refresh()
    {
        ZzzBackendResult<ZzzRunStatusDto> result = _backend.GetCurrentRun();
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "运行状态读取失败。");
            return;
        }

        ApplyRun(result.Value);
    }

    private void ApplyRun(ZzzRunStatusDto run)
    {
        _errorBar.IsOpen = false;
        string state = FormatState(run.State);
        _stateText.Text = "当前状态 " + state;
        _displayedApp = run.AppName ?? run.AppId ?? "-";
        _displayedInstance = run.InstanceIndex?.ToString("00", CultureInfo.InvariantCulture) ?? "-";
        _displayedDuration = run.DurationSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        _displayedLastStatus = run.LastStatus ?? "-";
        string action = run.State switch
        {
            ZzzRunState.Running => "暂停",
            ZzzRunState.Paused => "继续",
			_ => "开始", 
        };
        ApplyButtonLabels(action);
        _stopActionAvailable = run.State is ZzzRunState.Starting or ZzzRunState.Running or ZzzRunState.Paused or ZzzRunState.Stopping;
        _stopButton.IsEnabled = _stopActionAvailable && !_runOperationOverlay.IsVisible;
    }

    private void SetRunOperationPending(bool pending)
    {
        _runOperationOverlay.IsVisible = pending;
        _primaryButton.IsEnabled = !pending;
        _stopButton.IsEnabled = !pending && _stopActionAvailable;
    }

    private void ApplyButtonLabels(string action)
    {
        _primaryAction = action;
        _primaryIcon.Symbol = string.Equals(action, "暂停", StringComparison.Ordinal)
            ? FASymbol.Pause
            : FASymbol.Play;
        _primaryLabel.Text = JoinActionAndHotkey(action, _startHotkey);
        _stopLabel.Text = JoinActionAndHotkey("停止", _stopHotkey);
    }

    private void StartRunEvents()
    {
        StopRunEvents();
        _eventReader = _backend.SubscribeEvents();
        _eventCancellation = new CancellationTokenSource();
        ChannelReader<ZzzBackendEvent> reader = _eventReader;
        CancellationToken token = _eventCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (ZzzBackendEvent item in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    if (item.Type is not ("run.stateChanged" or "run.progress") || item.Data is not ZzzRunStatusDto run)
                    {
                        continue;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() => ApplyRun(run));
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void StopRunEvents()
    {
        _eventCancellation?.Cancel();
        if (_eventReader is not null)
        {
            _backend.UnsubscribeEvents(_eventReader);
        }

        _eventCancellation?.Dispose();
        _eventCancellation = null;
        _eventReader = null;
    }

    private void ShowError(string message)
    {
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private void OnPrimaryButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        _ = OnPrimaryClickedAsync();

    private void OnStopButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        _ = InvokeStopActionAsync();

    private static string ReadHotkey(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text.ToUpperInvariant()
            : string.Empty;

    private static string JoinActionAndHotkey(string action, string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? action : $"{action} {hotkey}";

    private static string FormatState(ZzzRunState state) =>
        state switch
        {
            ZzzRunState.Starting => "启动中",
            ZzzRunState.Running => "运行中",
            ZzzRunState.Paused => "已暂停",
            ZzzRunState.Stopping => "停止中",
            ZzzRunState.Succeeded => "已完成",
            ZzzRunState.Cancelled => "已停止",
            ZzzRunState.Failed => "异常",
            _ => "空闲",
        };
}

