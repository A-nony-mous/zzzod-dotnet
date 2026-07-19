using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

using ZzzOd.Gui.Pages.Settings;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed record ZzzPushOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzPushFieldModel : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private ZzzPushOption? _selectedOption;

    public required ZzzPushFieldDescriptor Descriptor { get; init; }

    public string DisplayTitle => Descriptor.Required ? $"{Descriptor.Title} *" : Descriptor.Title;

    public string Placeholder => Descriptor.Placeholder;

    public bool IsCombo => Descriptor.FieldType == ZzzPushFieldType.Combo;

    public bool IsText => !IsCombo;

    public bool AcceptsReturn => Descriptor.FieldType is ZzzPushFieldType.KeyValue or ZzzPushFieldType.CodeEditor;

    public IReadOnlyList<ZzzPushOption> Options { get; init; } = [];

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value ?? string.Empty);
    }

    public ZzzPushOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetField(ref _selectedOption, value) && value is not null)
            {
                Value = value.Value;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

internal sealed partial class FrontierPushSettingsPage : UserControl, IZzzPageLifecycle
{
    private readonly IZzzAppBackend _backend;
    private readonly IZzzPushNotificationService _pushService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly FAInfoBar _resultBar;
    private readonly FAComboBox _proxyCombo;
    private readonly FAComboBox _channelCombo;
    private readonly FAComboBox _emailServiceCombo;
    private readonly FASettingsExpander _personalProxyItem;
    private readonly FASettingsExpander _curlItem;
    private readonly StackPanel _emailServicePanel;
    private readonly ItemsControl _channelFieldList;
    private readonly Dictionary<string, ZzzPushFieldModel[]> _channelFields = new(StringComparer.Ordinal);
    private bool _loading;

    public FrontierPushSettingsPage(IZzzAppBackend backend, IZzzPushNotificationService pushService, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _pushService = pushService;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);

        _resultBar = Required<FAInfoBar>("ResultBar");
        _proxyCombo = Required<FAComboBox>("ProxyCombo");
        _channelCombo = Required<FAComboBox>("ChannelCombo");
        _emailServiceCombo = Required<FAComboBox>("EmailServiceCombo");
        _personalProxyItem = Required<FASettingsExpander>("PersonalProxyItem");
        _curlItem = Required<FASettingsExpander>("CurlItem");
        _emailServicePanel = Required<StackPanel>("EmailServicePanel");
        _channelFieldList = Required<ItemsControl>("ChannelFieldList");

        _proxyCombo.ItemsSource = Options(("不启用", "NONE"), ("个人代理", "PERSONAL"));
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
    }

    internal IReadOnlyList<ZzzPushChannelDescriptor> ChannelsForTest => _pushService.Channels;

    internal string GenerateWebhookCurlForTest(string style) => GenerateCurl(style);

    internal void SaveValueForTest(string scope, string key, object? value) => Save(scope, key, value);

    private bool Reload()
    {
        _loading = true;
        _resultBar.IsOpen = false;
        ZzzBackendResult<ZzzConfigScopeValuesDto> notify = _backend.GetConfigScope("notify");
        ZzzBackendResult<ZzzConfigScopeValuesDto> push = _backend.GetConfigScope("push");
        ZzzBackendResult<ZzzConfigScopeValuesDto> env = _backend.GetConfigScope("env");
        if (!notify.Success || notify.Value is null || !push.Success || push.Value is null || !env.Success || env.Value is null)
        {
            ShowError(notify.Error ?? push.Error ?? env.Error ?? "通知设置读取失败。");
            _loading = false;
            return false;
        }

        Required<TextBox>("TitleTextBox").Text = ReadString(notify.Value.Values, "title");
        Required<ToggleSwitch>("SendImageToggle").IsChecked = ReadBool(push.Value.Values, "send_image");
        string proxy = ReadString(push.Value.Values, "proxy");
        Select(_proxyCombo, proxy);
        Required<TextBox>("PersonalProxyTextBox").Text = ReadString(env.Value.Values, "personal_proxy");
        foreach (ZzzPushFieldModel field in _channelFields.Values.SelectMany(fields => fields))
        {
            string value = push.Value.Values.TryGetValue(field.Descriptor.Key, out object? raw)
                ? Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
                : field.Descriptor.DefaultValue;
            field.Value = value;
            field.SelectedOption = field.Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
        }

        _personalProxyItem.IsVisible = string.Equals(proxy, "PERSONAL", StringComparison.Ordinal);
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

    private void OnTitleChanged(object? sender, RoutedEventArgs args) => Save("notify", "title", (sender as TextBox)?.Text ?? string.Empty);

    private void OnSendImageChanged(object? sender, RoutedEventArgs args) => Save("push", "send_image", (sender as ToggleSwitch)?.IsChecked == true);

    private void OnProxyChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _proxyCombo.SelectedItem is not ZzzPushOption option)
        {
            return;
        }

        Save("push", "proxy", option.Value);
        _personalProxyItem.IsVisible = string.Equals(option.Value, "PERSONAL", StringComparison.Ordinal);
    }

    private void OnPersonalProxyChanged(object? sender, RoutedEventArgs args) => Save("env", "personal_proxy", (sender as TextBox)?.Text ?? string.Empty);

    private void OnChannelChanged(object? sender, SelectionChangedEventArgs args) => UpdateChannelFields();

    private void OnDynamicTextChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && sender is TextBox { DataContext: ZzzPushFieldModel field })
        {
            Save("push", field.Descriptor.Key, field.Value);
        }
    }

    private void OnDynamicComboChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && sender is FAComboBox { DataContext: ZzzPushFieldModel field } && field.SelectedOption is not null)
        {
            Save("push", field.Descriptor.Key, field.SelectedOption.Value);
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
        Save("push", key, value);
    }

    private void SetTestButtonsEnabled(bool enabled)
    {
        Required<Button>("TestCurrentButton").IsEnabled = enabled;
        Required<Button>("TestAllButton").IsEnabled = enabled;
    }

    private void OnHelpClicked(object? sender, RoutedEventArgs args) =>
        Process.Start(new ProcessStartInfo("https://one-dragon.com/zzz/zh/setting_notify.html") { UseShellExecute = true });

    private void Save(string scope, string key, object? value)
    {
        if (_loading)
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(scope, new Dictionary<string, object?> { [key] = value }));
        if (!result.Success)
        {
            ShowError(result.Error ?? "通知设置保存失败。");
        }
    }

    private void ShowError(string message)
    {
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

    private static IReadOnlyList<ZzzPushOption> Options(params (string Label, string Value)[] options) => options.Select(option => new ZzzPushOption(option.Label, option.Value)).ToArray();

    private static void Select(SelectingItemsControl combo, string value) => combo.SelectedItem = combo.ItemsSource?.OfType<ZzzPushOption>().FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static string SelectedValue(SelectingItemsControl combo) => combo.SelectedItem is ZzzPushOption option ? option.Value : string.Empty;

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key) => values.TryGetValue(key, out object? value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) => values.TryGetValue(key, out object? value) && Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);

    private static string Quote(string value, bool powerShell) => powerShell ? $"'{value.Replace("'", "''", StringComparison.Ordinal)}'" : $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    private T Required<T>(string name) where T : Control => this.FindControl<T>(name) ?? throw new InvalidOperationException($"通知设置页缺少 {name}。");
}
