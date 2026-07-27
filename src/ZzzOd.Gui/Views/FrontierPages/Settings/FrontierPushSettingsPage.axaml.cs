using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Notifications;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed partial class FrontierPushSettingsPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzPushSettingsViewModel _viewModel;
    private readonly IZzzPushNotificationService _pushService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly FAInfoBar _resultBar;
    private readonly FAComboBox _channelCombo;
    private readonly FAComboBox _emailServiceCombo;
    private readonly FASettingsExpander _curlItem;
    private readonly StackPanel _emailServicePanel;
    private readonly ItemsControl _channelFieldList;
    private readonly Dictionary<string, ZzzPushFieldModel[]> _channelFields = new(StringComparer.Ordinal);
    private bool _loading;

    public FrontierPushSettingsPage(IZzzAppBackend backend, IZzzPushNotificationService pushService, ZzzGuiOperationTracker? operations = null)
    {
        _pushService = pushService;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);

        _resultBar = Required<FAInfoBar>("ResultBar");
        _channelCombo = Required<FAComboBox>("ChannelCombo");
        _emailServiceCombo = Required<FAComboBox>("EmailServiceCombo");
        _curlItem = Required<FASettingsExpander>("CurlItem");
        _emailServicePanel = Required<StackPanel>("EmailServicePanel");
        _channelFieldList = Required<ItemsControl>("ChannelFieldList");

        _viewModel = new ZzzPushSettingsViewModel(backend, pushService, ShowError);
        DataContext = _viewModel;
        _channelCombo.ItemsSource = _pushService.Channels
            .Select(channel => new ZzzPushOption(channel.ChannelName, channel.ChannelId))
            .ToArray();
        _emailServiceCombo.ItemsSource = _pushService.EmailServices.Keys.ToArray();
        foreach (ZzzPushChannelDescriptor channel in _pushService.Channels)
        {
            _channelFields[channel.ChannelId] = channel.Fields.Select(CreateFieldModel).ToArray();
        }
    }

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-push", "reload-push-settings");
        try
        {
            _operations.Complete(operationId, Reload() ? ZzzGuiOperationState.Succeeded : ZzzGuiOperationState.Failed);
        }
        catch (Exception exception)
        {
            _operations.Complete(operationId, ZzzGuiOperationState.Failed, exception: exception);
            ShowError(exception.Message);
        }
    }

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
        _viewModel.DisposePage();
    }

    internal IReadOnlyList<ZzzPushChannelDescriptor> ChannelsForTest => _pushService.Channels;

    internal string GenerateWebhookCurlForTest(string style) => GenerateCurl(style);

    internal void SaveValueForTest(string scope, string key, object? value) => _viewModel.SaveValue(scope, key, value);

    private bool Reload()
    {
        _loading = true;
        _resultBar.IsOpen = false;
        _viewModel.OnPageShown();
        if (_viewModel.LastError is not null)
        {
            _loading = false;
            return false;
        }

        foreach (ZzzPushFieldModel field in _channelFields.Values.SelectMany(fields => fields))
        {
            string value = Convert.ToString(
                _viewModel.GetPushValue(field.Descriptor.Key, field.Descriptor.DefaultValue),
                System.Globalization.CultureInfo.InvariantCulture) ?? field.Descriptor.DefaultValue;
            field.Value = value;
            field.SelectedOption = field.Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
        }

        if (_channelCombo.SelectedIndex < 0 && _pushService.Channels.Count > 0)
        {
            _channelCombo.SelectedIndex = 0;
        }

        UpdateChannelFields();
        _loading = false;
        return true;
    }

    private ZzzPushFieldModel CreateFieldModel(ZzzPushFieldDescriptor descriptor)
    {
        ZzzPushOption[] options = descriptor.Options.Select(option => new ZzzPushOption(option, option)).ToArray();
        return new ZzzPushFieldModel
        {
            Descriptor = descriptor,
            Options = options,
            Value = descriptor.DefaultValue,
            SelectedOption = options.FirstOrDefault(option => string.Equals(option.Value, descriptor.DefaultValue, StringComparison.Ordinal)),
        };
    }

    private void OnChannelChanged(object? sender, SelectionChangedEventArgs args) => UpdateChannelFields();

    private void OnDynamicTextChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzPushFieldModel field })
        {
            _viewModel.SaveValue("push", field.Descriptor.Key, field.Value);
        }
    }

    private void OnDynamicComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is FAComboBox { DataContext: ZzzPushFieldModel field } && field.SelectedOption is not null)
        {
            _viewModel.SaveValue("push", field.Descriptor.Key, field.SelectedOption.Value);
        }
    }

    private void OnEmailServiceChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _emailServiceCombo.SelectedItem is not string name || !_pushService.EmailServices.TryGetValue(name, out ZzzEmailServicePreset? preset))
        {
            return;
        }

        SetField("SMTP", "smtp_server", $"{preset.Host}:{preset.Port}");
        SetField("SMTP", "smtp_ssl", preset.Secure ? "true" : "false");
    }

    private async void OnTestCurrentClicked(object? sender, RoutedEventArgs args) => await SendTestAsync(SelectedValue(_channelCombo)).ConfigureAwait(true);

    private async void OnTestAllClicked(object? sender, RoutedEventArgs args) => await SendTestAsync(null).ConfigureAwait(true);

    private async Task SendTestAsync(string? channelId)
    {
        SetTestButtonsEnabled(false);
        try
        {
            ZzzPushTestResult result = await _pushService.SendTestAsync(
                channelId,
                "测试推送通知",
                "这是一条测试消息").ConfigureAwait(true);
            if (result.Success)
            {
                ShowSuccess(channelId is null ? "已向所有已配置的通知方式发送测试消息" : "已向当前通知方式发送测试消息");
            }
            else
            {
                ShowError(result.Message);
            }
        }
        catch (Exception exception)
        {
            ShowError($"测试推送失败: {exception.Message}");
        }
        finally
        {
            SetTestButtonsEnabled(true);
        }
    }

    private async void OnPowerShellCurlClicked(object? sender, RoutedEventArgs args) => await CopyCurlAsync("pwsh").ConfigureAwait(true);

    private async void OnUnixCurlClicked(object? sender, RoutedEventArgs args) => await CopyCurlAsync("unix").ConfigureAwait(true);

    private async Task CopyCurlAsync(string style)
    {
        try
        {
            string command = GenerateCurl(style);
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is null)
            {
                ShowError("剪贴板不可用。");
                return;
            }

            await topLevel.Clipboard.SetTextAsync(command).ConfigureAwait(true);
            ShowSuccess("cURL 命令已复制到剪贴板！");
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            ShowError(exception.Message);
        }
    }

    private string GenerateCurl(string style)
    {
        IReadOnlyDictionary<string, ZzzPushFieldModel> fields = _channelFields["WEBHOOK"].ToDictionary(field => field.Descriptor.Key, StringComparer.Ordinal);
        string url = fields["webhook_url"].Value;
        string method = fields["webhook_method"].Value;
        string contentType = fields["webhook_content_type"].Value;
        string headers = fields["webhook_headers"].Value;
        string body = fields["webhook_body"].Value;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("请先配置 Webhook URL");
        }

        if (!new[] { url, headers, body }.Any(value => value.Contains("$content", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("URL、请求头或者请求体中必须包含 $content 变量");
        }

        if (string.Equals(contentType, "application/json", StringComparison.Ordinal))
        {
            _ = JsonDocument.Parse(body);
        }

        using JsonDocument? headerDocument = ParseHeaders(headers);
        bool powerShell = string.Equals(style, "pwsh", StringComparison.Ordinal);
        StringBuilder command = new(powerShell ? "curl.exe" : "curl");
        command.Append(" -X ").Append(method).Append(' ').Append(Quote(url, powerShell));
        command.Append(" -H ").Append(Quote($"Content-Type: {contentType}", powerShell));
        if (headerDocument is not null)
        {
            foreach (JsonProperty property in headerDocument.RootElement.EnumerateObject())
            {
                command.Append(" -H ").Append(Quote($"{property.Name}: {property.Value}", powerShell));
            }
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            command.Append(" --data-raw ").Append(Quote(body, powerShell));
        }

        return command.ToString();
    }

    private static JsonDocument? ParseHeaders(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers) || string.Equals(headers.Trim(), "{}", StringComparison.Ordinal))
        {
            return null;
        }

        JsonDocument document = JsonDocument.Parse(headers);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            return document;
        }

        document.Dispose();
        throw new InvalidOperationException("请求头不是合法的JSON格式");
    }

    private void UpdateChannelFields()
    {
        string channelId = SelectedValue(_channelCombo);
        _channelFieldList.ItemsSource = _channelFields.GetValueOrDefault(channelId, []);
        _emailServicePanel.IsVisible = string.Equals(channelId, "SMTP", StringComparison.Ordinal);
        _curlItem.IsVisible = string.Equals(channelId, "WEBHOOK", StringComparison.Ordinal);
    }

    private void SetField(string channelId, string key, string value)
    {
        ZzzPushFieldModel? field = _channelFields[channelId].FirstOrDefault(candidate => string.Equals(candidate.Descriptor.Key, key, StringComparison.Ordinal));
        if (field is null)
        {
            return;
        }

        field.Value = value;
        field.SelectedOption = field.Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
        _viewModel.SaveValue("push", key, value);
    }

    private void SetTestButtonsEnabled(bool enabled)
    {
        Required<Button>("TestCurrentButton").IsEnabled = enabled;
        Required<Button>("TestAllButton").IsEnabled = enabled;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo("https://one-dragon.com/zzz/zh/setting_notify.html") { UseShellExecute = true });

    private void ShowError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _resultBar.IsOpen = false;
            return;
        }

        _resultBar.Title = "错误";
        _resultBar.Message = message;
        _resultBar.Severity = FAInfoBarSeverity.Error;
        _resultBar.IsOpen = true;
    }

    private void ShowSuccess(string message)
    {
        _resultBar.Title = "成功";
        _resultBar.Message = message;
        _resultBar.Severity = FAInfoBarSeverity.Success;
        _resultBar.IsOpen = true;
    }

    private static string SelectedValue(SelectingItemsControl combo) => combo.SelectedItem is ZzzPushOption option ? option.Value : string.Empty;

    private static string Quote(string value, bool powerShell) => powerShell ? $"'{value.Replace("'", "''", StringComparison.Ordinal)}'" : $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private T Required<T>(string name) where T : Control => this.FindControl<T>(name) ?? throw new InvalidOperationException($"通知设置页缺少 {name}。");
}
