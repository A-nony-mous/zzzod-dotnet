using System.Threading.Channels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.PageModels.Accounts;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.Accounts;

internal sealed partial class ZzzFrontierAccountsPage : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly FAInfoBar _actionBar;
    private readonly ToggleSwitch _forceLoginBeforeRunToggle;
    private readonly FAContentDialog _protectionDialog;
    private readonly TextBox _protectionPasswordInput;
    private ChannelReader<ZzzBackendEvent>? _eventReader;
    private CancellationTokenSource? _eventCancellation;
    private bool _loading;

    public ZzzFrontierAccountsPage(IZzzAppBackend backend, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _operations = operations ?? new ZzzGuiOperationTracker();
        InstanceManagement = new ZzzInstanceManagementPage(backend, ReportLoadError);
        CurrentAccountSettings = new ZzzCurrentAccountSettingsPage(backend, InstanceManagement, ReportLoadError);

        AvaloniaXamlLoader.Load(this);
        _actionBar = Required<FAInfoBar>("ActionBar");
        _forceLoginBeforeRunToggle = Required<ToggleSwitch>("ForceLoginBeforeRunToggle");
        _protectionDialog = Required<FAContentDialog>("ProtectionDialog");
        _protectionPasswordInput = Required<TextBox>("ProtectionPasswordInput");
        DataContext = CurrentAccountSettings;
    }

    public ZzzInstanceManagementPage InstanceManagement { get; }

    public ZzzCurrentAccountSettingsPage CurrentAccountSettings { get; }

    public void OnPageShown()
    {
        Reload();
        StartEvents();
    }

    public void CancelPageOperations(string reason) => StopEvents();

    public void OnPageHidden() => StopEvents();

    public void OnPageLeave() => StopEvents();

    public void DisposePage() => StopEvents();

    internal void Reload()
    {
        Guid operationId = _operations.Start("accounts", "reload-accounts");
        _loading = true;
        try
        {
            _actionBar.IsOpen = false;
            InstanceManagement.Reload();
            CurrentAccountSettings.Reload();

            if (!InstanceManagement.CanSwitch)
            {
                ShowAction("一条龙运行中，不能切换实例。", FAInfoBarSeverity.Warning);
            }

            _operations.Complete(operationId, ZzzGuiOperationState.Succeeded);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowAction(exception.Message, FAInfoBarSeverity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        OpenUrl("https://one-dragon.com/zzz/zh/config.html");

    private async void OnChooseGamePathClicked(object? sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            ShowAction("当前窗口不可用。", FAInfoBarSeverity.Error);
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择你的 game.exe",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Exe") { Patterns = ["*.exe"] },
            ],
        }).ConfigureAwait(true);
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            string fullPath = Path.GetFullPath(path);
            CurrentAccountSettings.GamePath = fullPath;
        }
    }

    private void OnForceLoginBeforeRunChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        ZzzInstanceDto? current = InstanceManagement.Instances.FirstOrDefault(instance => instance.Active);
        if (current is null)
        {
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = InstanceManagement.UpdateInstance(
            current.Index,
            null,
            null,
            _forceLoginBeforeRunToggle.IsChecked == true);
        if (!result.Success)
        {
            _forceLoginBeforeRunToggle.IsChecked = current.ForceLoginBeforeRun;
        }

        ShowResult(result);
    }

    private void OnInstanceNameChanged(object? sender, TextChangedEventArgs args)
    {
        if (_loading
            || sender is not TextBox { DataContext: ZzzAccountInstanceRow row }
            || !row.HasPendingNameChange)
        {
            return;
        }

        SaveInstanceRow(row, row.Name, null);
    }

    private void OnInstanceRunChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading
            || sender is not Control { DataContext: ZzzAccountInstanceRow row }
            || !row.HasPendingRunOptionChange)
        {
            return;
        }

        SaveInstanceRow(row, null, row.SelectedRunOption.Value);
    }

    private void SaveInstanceRow(ZzzAccountInstanceRow row, string? name, bool? activeInOneDragon)
    {
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = InstanceManagement.UpdateInstance(
            row.Index,
            name,
            activeInOneDragon);
        if (result.Success)
        {
            ZzzInstanceDto? persisted = result.Value?.FirstOrDefault(instance => instance.Index == row.Index);
            if (persisted is not null)
            {
                row.SynchronizePersistedValues(persisted);
            }
        }

        ShowResult(result);
    }

    private void OnActivateInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: ZzzAccountInstanceRow row })
        {
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = InstanceManagement.ActivateInstance(row.Index);
        if (result.Success)
        {
            Reload();
        }
        else
        {
            ShowResult(result);
        }
    }

    private void OnLoginInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzAccountInstanceRow row })
        {
            ZzzBackendResult<ZzzRunStatusDto> result = InstanceManagement.LoginInstance(row.Index);
            ShowAction(
                result.Success ? $"实例 {row.Index:00} 登录已启动。" : result.Error ?? "登录失败。",
                result.Success ? FAInfoBarSeverity.Success : FAInfoBarSeverity.Error);
        }
    }

    private void OnDeleteInstanceClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: ZzzAccountInstanceRow row })
        {
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = InstanceManagement.DeleteInstance(row.Index);
        if (result.Success)
        {
            Reload();
        }
        else
        {
            ShowResult(result);
        }
    }

    private async void OnAddInstanceClicked(object? sender, RoutedEventArgs args)
    {
        ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result;
        if (InstanceManagement.Instances.Count < 5)
        {
            result = InstanceManagement.AddInstance();
        }
        else
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                ShowAction("当前窗口不可用。", FAInfoBarSeverity.Error);
                return;
            }

            _protectionPasswordInput.Text = string.Empty;
            FAContentDialogResult dialogResult = await _protectionDialog.ShowAsync(owner).ConfigureAwait(true);
            if (dialogResult != FAContentDialogResult.Primary)
            {
                return;
            }

            result = InstanceManagement.AddProtectedInstance(_protectionPasswordInput.Text ?? string.Empty);
        }

        if (result.Success)
        {
            Reload();
        }
        else
        {
            ShowResult(result);
        }
    }

    private void ShowResult<T>(ZzzBackendResult<T> result)
    {
        if (!result.Success)
        {
            ShowAction(result.Error ?? "操作失败。", FAInfoBarSeverity.Error);
        }
    }

    private void ShowAction(string message, FAInfoBarSeverity severity)
    {
        _actionBar.Message = message;
        _actionBar.Severity = severity;
        _actionBar.IsOpen = true;
    }

    private void ReportLoadError(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error) && _actionBar is not null)
        {
            ShowAction(error, FAInfoBarSeverity.Error);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void StartEvents()
    {
        StopEvents();
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
                    if (item.Type is "instance.activeChanged" or "instance.changed" or "run.stateChanged")
                    {
                        await Dispatcher.UIThread.InvokeAsync(Reload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        });
    }

    private void StopEvents()
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

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"前卫账户页缺少 {name}。");
}
