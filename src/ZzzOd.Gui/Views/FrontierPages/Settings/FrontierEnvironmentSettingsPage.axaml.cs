using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Pages.Settings;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed record ZzzEnvironmentOption(string Label, string Value);

internal sealed partial class FrontierEnvironmentSettingsPage : UserControl, IZzzPageLifecycle
{
    private static readonly ZzzEnvironmentOption[] ScreenshotMethods =
    [
        new("自动", "auto"),
        new("Windows Graphics Capture", "wgc"),
        new("Print Window", "print_window"),
        new("BitBlt", "bitblt"),
    ];

    private static readonly ZzzEnvironmentOption[] ProxyTypes =
    [
        new("无", "None"),
        new("个人代理", "personal"),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly ZzzGlobalInputMonitor _inputMonitor;
    private readonly IZzzEnvironmentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly bool _ownsInputMonitor;
    private readonly FAInfoBar _actionBar;
    private readonly FAComboBox _screenshotMethodCombo;
    private readonly ToggleSwitch _debugToggle;
    private readonly ToggleSwitch _copyScreenshotToggle;
    private readonly FAComboBox _proxyTypeCombo;
    private readonly FASettingsExpanderItem _personalProxyItem;
    private readonly TextBox _personalProxyInput;
    private readonly IReadOnlyDictionary<string, Button> _hotkeyButtons;
    private bool _loading;
    private Button? _captureButton;
    private IDisposable? _hotkeyActionSuspension;

    public FrontierEnvironmentSettingsPage(
        IZzzAppBackend backend,
        ZzzGlobalInputMonitor? inputMonitor = null,
        IZzzEnvironmentRuntimeCoordinator? runtimeCoordinator = null,
        ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _inputMonitor = inputMonitor ?? new ZzzGlobalInputMonitor();
        _runtimeCoordinator = runtimeCoordinator;
        _operations = operations ?? new ZzzGuiOperationTracker();
        _ownsInputMonitor = inputMonitor is null;
        AvaloniaXamlLoader.Load(this);

        _actionBar = Required<FAInfoBar>("ActionBar");
        _screenshotMethodCombo = Required<FAComboBox>("ScreenshotMethodCombo");
        _debugToggle = Required<ToggleSwitch>("DebugToggle");
        _copyScreenshotToggle = Required<ToggleSwitch>("CopyScreenshotToggle");
        _proxyTypeCombo = Required<FAComboBox>("ProxyTypeCombo");
        _personalProxyItem = Required<FASettingsExpanderItem>("PersonalProxyItem");
        _personalProxyInput = Required<TextBox>("PersonalProxyInput");
        _hotkeyButtons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["key_start_running"] = Required<Button>("StartRunningKeyButton"),
            ["key_stop_running"] = Required<Button>("StopRunningKeyButton"),
            ["key_screenshot"] = Required<Button>("ScreenshotKeyButton"),
            ["key_debug"] = Required<Button>("DebugKeyButton"),
        };

        _screenshotMethodCombo.ItemsSource = ScreenshotMethods;
        _proxyTypeCombo.ItemsSource = ProxyTypes;
    }

    internal bool PersonalProxyVisible => _personalProxyItem.IsVisible;

    internal string? SelectedProxyType => (_proxyTypeCombo.SelectedItem as ZzzEnvironmentOption)?.Value;

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-environment", "reload-environment-settings");
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

    public void OnPageHidden() => CancelHotkeyCapture();

    public void DisposePage()
    {
        CancelHotkeyCapture();
        if (_ownsInputMonitor)
        {
            _inputMonitor.Dispose();
        }
    }

    internal void SaveStringForTest(string key, string value)
    {
        if (!SaveValue(key, value))
        {
            return;
        }

        if (string.Equals(key, "proxy_type", StringComparison.Ordinal))
        {
            _loading = true;
            try
            {
                SelectOption(_proxyTypeCombo, value);
                UpdateProxyVisibility();
            }
            finally
            {
                _loading = false;
            }

            ApplyProcessProxy(value, _personalProxyInput.Text ?? string.Empty);
        }
        else if (string.Equals(key, "personal_proxy", StringComparison.Ordinal))
        {
            _personalProxyInput.Text = value;
            ApplyProcessProxy(SelectedProxyType, value);
        }
    }

    private bool Reload()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("env");
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "脚本环境读取失败。");
            return false;
        }

        _loading = true;
        try
        {
            IReadOnlyDictionary<string, object?> values = result.Value.Values;
            _runtimeCoordinator?.UpdateEnvironmentConfiguration(result.Value);
            SelectOption(_screenshotMethodCombo, NormalizeScreenshotMethodForDisplay(ReadString(values, "screenshot_method")));
            _debugToggle.IsChecked = ReadBool(values, "is_debug");
            _copyScreenshotToggle.IsChecked = ReadBool(values, "copy_screenshot");
            SelectOption(_proxyTypeCombo, ReadString(values, "proxy_type"));
            _personalProxyInput.Text = ReadString(values, "personal_proxy");
            foreach ((string key, Button button) in _hotkeyButtons)
            {
                button.Content = ReadString(values, key).ToUpperInvariant();
            }

            UpdateProxyVisibility();
            ApplyProcessProxy(SelectedProxyType, _personalProxyInput.Text ?? string.Empty);
            _actionBar.IsOpen = false;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
            return false;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnScreenshotMethodChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _screenshotMethodCombo.SelectedItem is ZzzEnvironmentOption option)
        {
            SaveValue("screenshot_method", option.Value);
        }
    }

    private async void OnDebugChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && _debugToggle.IsChecked is bool value)
        {
            if (!SaveValue("is_debug", value) || _runtimeCoordinator is null)
            {
                return;
            }

            ZzzBackendResult<bool> result = await _runtimeCoordinator.ReinitializeContextAsync();
            if (!result.Success)
            {
				ShowError(result.Error ?? "脚本环境重新初始化失败。");
            }
        }
    }

    private void OnCopyScreenshotChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && _copyScreenshotToggle.IsChecked is bool value)
        {
            SaveValue("copy_screenshot", value);
        }
    }

    private void OnProxyTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        UpdateProxyVisibility();
        if (_loading || _proxyTypeCombo.SelectedItem is not ZzzEnvironmentOption option)
        {
            return;
        }

        if (SaveValue("proxy_type", option.Value))
        {
            ApplyProcessProxy(option.Value, _personalProxyInput.Text ?? string.Empty);
        }
    }

    private void OnPersonalProxyLostFocus(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        string value = _personalProxyInput.Text ?? string.Empty;
        if (SaveValue("personal_proxy", value))
        {
            ApplyProcessProxy(SelectedProxyType, value);
        }
    }

    private void OnHotkeyCaptureClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button button)
        {
            return;
        }

        CancelHotkeyCapture();
        _captureButton = button;
        _hotkeyActionSuspension = _runtimeCoordinator?.SuspendHotkeyActions();
			button.Content = "请按键";
        button.Focus();
        _inputMonitor.InputPressed += OnGlobalInputPressed;
        if (!_inputMonitor.EnsureStarted())
        {
				ShowError(_inputMonitor.LastError ?? "全局按键监听启动失败。");
            CancelHotkeyCapture();
        }
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs args)
    {
        if (!ReferenceEquals(sender, _captureButton)
            || sender is not Button button
            || button.Tag is not string key)
        {
            return;
        }

        CompleteHotkeyCapture(button, key, args.Key.ToString().ToLowerInvariant());
        args.Handled = true;
    }

    private void OnGlobalInputPressed(object? sender, string value)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_captureButton is Button { Tag: string key } button)
            {
                CompleteHotkeyCapture(button, key, value);
            }
        });
    }

    private void CompleteHotkeyCapture(Button button, string key, string value)
    {
        if (SaveValue(key, value))
        {
            button.Content = value.ToUpperInvariant();
        }

        _inputMonitor.InputPressed -= OnGlobalInputPressed;
        _hotkeyActionSuspension?.Dispose();
        _hotkeyActionSuspension = null;
        _captureButton = null;
    }

    private void CancelHotkeyCapture()
    {
        _inputMonitor.InputPressed -= OnGlobalInputPressed;
        _hotkeyActionSuspension?.Dispose();
        _hotkeyActionSuspension = null;
        if (_captureButton?.Tag is string key)
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope("env");
            if (result.Success
                && result.Value is not null
                && result.Value.Values.TryGetValue(key, out object? value))
            {
                _captureButton.Content = Convert.ToString(value, CultureInfo.InvariantCulture)?.ToUpperInvariant() ?? string.Empty;
            }
        }

        _captureButton = null;
    }

    private bool SaveValue(string key, object value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            "env",
            new Dictionary<string, object?> { [key] = value }));
        if (result.Success)
        {
            if (result.Value is not null)
            {
                _runtimeCoordinator?.UpdateEnvironmentConfiguration(result.Value);
            }

            _actionBar.IsOpen = false;
            return true;
        }

        ShowError(result.Error ?? (key + " 保存失败。"));
        return false;
    }

    private void UpdateProxyVisibility() =>
        _personalProxyItem.IsVisible = string.Equals(SelectedProxyType, "personal", StringComparison.Ordinal);

    private static void ApplyProcessProxy(string? proxyType, string personalProxy)
    {
        string value = string.Equals(proxyType, "personal", StringComparison.Ordinal) ? personalProxy : string.Empty;
        Environment.SetEnvironmentVariable("HTTP_PROXY", value);
        Environment.SetEnvironmentVariable("HTTPS_PROXY", value);
    }

    private static void SelectOption(SelectingItemsControl comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.ItemsSource?
            .OfType<ZzzEnvironmentOption>()
            .FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("脚本环境缺少配置项 " + key + "。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// 把配置里的截图方法值映射到当前可选项。
    /// </summary>
    /// <param name="value">配置中的取值。</param>
    /// <returns>下拉框中对应的取值。</returns>
    /// <remarks>
    /// 桌面取像类的旧值并入 BitBlt，已退役后端的取值显示为自动选择，
    /// 与配置解析层的折叠规则保持一致。
    /// </remarks>
    private static string NormalizeScreenshotMethodForDisplay(string value) => value switch
    {
        "mss" or "pil" => "bitblt",
        "dwm_shared_surface" => "auto",
        _ => value,
    };

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("脚本环境缺少配置项 " + key + "。");
        }

        return value is bool flag ? flag : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private void ShowError(string message)
    {
        _actionBar.Title = "脚本环境读取失败";
        _actionBar.Message = message;
        _actionBar.Severity = FAInfoBarSeverity.Error;
        _actionBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"脚本环境页面缺少 {name}。");
}
