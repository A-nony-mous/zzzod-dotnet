using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Pages.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierScreenshotHelperPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "screenshot-helper";
    private readonly IZzzAppBackend _backend;
    private readonly ZzzGuiRunIntentService _runIntent;
    private readonly FAInfoBar _configErrorBar;
    private readonly FANumberBox _frequencyBox;
    private readonly FANumberBox _lengthBox;
    private readonly Button _saveKeyButton;
    private readonly ToggleSwitch _dodgeDetectToggle;
    private readonly ToggleSwitch _screenshotBeforeKeyToggle;
    private readonly ToggleSwitch _miniMapAngleDetectToggle;
    private int? _instanceIndex;
    private string? _saveKey;
    private volatile bool _capturingKey;
    private bool _loading;
    private IDisposable? _inputSuspension;

    public FrontierScreenshotHelperPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _backend = backend;
        _runIntent = runIntent;
        _runIntent.GlobalInputPressed += OnGlobalInputPressed;
        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.ScreenshotHelper,
            runIntent: runIntent,
            fixedGroupId: ScreenshotHelperConstants.DefaultGroupId);

        AvaloniaXamlLoader.Load(this);
        _configErrorBar = Required<FAInfoBar>("ConfigErrorBar");
        _frequencyBox = Required<FANumberBox>("FrequencyBox");
        _lengthBox = Required<FANumberBox>("LengthBox");
        _saveKeyButton = Required<Button>("SaveKeyButton");
        _dodgeDetectToggle = Required<ToggleSwitch>("DodgeDetectToggle");
        _screenshotBeforeKeyToggle = Required<ToggleSwitch>("ScreenshotBeforeKeyToggle");
        _miniMapAngleDetectToggle = Required<ToggleSwitch>("MiniMapAngleDetectToggle");
        Required<ContentControl>("RunHost").Content = RunPanel;
        SetInputsEnabled(false);
    }

    internal ZzzRunPanel RunPanel { get; }

    internal int? InstanceIndex => _instanceIndex;

    public void OnPageShown()
    {
        Reload();
        RunPanel.OnPageShown();
    }

    public void OnPageHidden()
    {
        StopKeyCapture();
        RunPanel.OnPageHidden();
    }

    public void OnPageLeave()
    {
        StopKeyCapture();
        RunPanel.OnPageLeave();
    }

    public void DisposePage()
    {
        StopKeyCapture();
        _runIntent.GlobalInputPressed -= OnGlobalInputPressed;
        RunPanel.DisposePage();
    }

    private void Reload()
    {
        ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
        if (!current.Success || current.Value is null)
        {
            _instanceIndex = null;
            SetInputsEnabled(false);
            ShowConfigError(current.Error ?? "当前实例读取失败。");
            return;
        }

        _instanceIndex = current.Value.Index;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            ScreenshotHelperConstants.DefaultGroupId);
        if (!result.Success || result.Value is null)
        {
            SetInputsEnabled(false);
            ShowConfigError(result.Error ?? "截图助手配置读取失败。");
            return;
        }

        ApplyValues(result.Value.Values);
    }

    private void OnFrequencyChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (!_loading && !double.IsNaN(args.NewValue))
        {
            Save("frequency_second", args.NewValue);
        }
    }

    private void OnLengthChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (!_loading && !double.IsNaN(args.NewValue))
        {
            Save("length_second", args.NewValue);
        }
    }

    private void OnSaveKeyClicked(object? sender, RoutedEventArgs args)
    {
        StopKeyCapture();
        _capturingKey = true;
        _inputSuspension = ScreenshotHelperGlobalInputSource.Suspend();
		_saveKeyButton.Content = "请按键";
        _saveKeyButton.Focus();
    }

    private void OnGlobalInputPressed(object? sender, string key)
    {
        if (_capturingKey)
        {
            Dispatcher.UIThread.Post(() => CompleteKeyCapture(key));
        }
    }

    private void OnDodgeDetectChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("dodge_detect", _dodgeDetectToggle.IsChecked == true);
        }
    }

    private void OnScreenshotBeforeKeyChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("screenshot_before_key", _screenshotBeforeKeyToggle.IsChecked == true);
        }
    }

    private void OnMiniMapAngleDetectChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("mini_map_angle_detect", _miniMapAngleDetectToggle.IsChecked == true);
        }
    }

    private void CompleteKeyCapture(string key)
    {
        _capturingKey = false;
        _inputSuspension?.Dispose();
        _inputSuspension = null;
        Save("key_save", key);
    }

    private void StopKeyCapture()
    {
        _capturingKey = false;
        _inputSuspension?.Dispose();
        _inputSuspension = null;
        _saveKeyButton.Content = _saveKey?.ToUpperInvariant() ?? string.Empty;
    }

    private void Save(string key, object value)
    {
        if (_instanceIndex is null)
        {
            SetInputsEnabled(false);
			ShowConfigError("当前实例不可用。");
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            ScreenshotHelperConstants.DefaultGroupId));
        if (!result.Success || result.Value is null)
        {
            ShowConfigError(result.Error ?? "截图助手配置保存失败。");
            StopKeyCapture();
            return;
        }

        ApplyValues(result.Value.Values);
    }

    private void ApplyValues(IReadOnlyDictionary<string, object?> values)
    {
        _loading = true;
        try
        {
            _frequencyBox.Value = RequiredDouble(values, "frequency_second");
            _lengthBox.Value = RequiredDouble(values, "length_second");
            _saveKey = RequiredString(values, "key_save");
            _saveKeyButton.Content = _saveKey.ToUpperInvariant();
            _dodgeDetectToggle.IsChecked = RequiredBool(values, "dodge_detect");
            _screenshotBeforeKeyToggle.IsChecked = RequiredBool(values, "screenshot_before_key");
            _miniMapAngleDetectToggle.IsChecked = RequiredBool(values, "mini_map_angle_detect");
            SetInputsEnabled(true);
            _configErrorBar.IsOpen = false;
        }
        catch (Exception exception) when (exception is KeyNotFoundException or FormatException or InvalidCastException or JsonException)
        {
            SetInputsEnabled(false);
            ShowConfigError($"截图助手配置读取失败：{exception.Message}");
        }
        finally
        {
            _loading = false;
            _capturingKey = false;
        }
    }

    private void SetInputsEnabled(bool enabled)
    {
        _frequencyBox.IsEnabled = enabled;
        _lengthBox.IsEnabled = enabled;
        _saveKeyButton.IsEnabled = enabled;
        _dodgeDetectToggle.IsEnabled = enabled;
        _screenshotBeforeKeyToggle.IsEnabled = enabled;
        _miniMapAngleDetectToggle.IsEnabled = enabled;
    }

    private void ShowConfigError(string message)
    {
        _configErrorBar.Title = "截图助手配置错误";
        _configErrorBar.Message = message;
        _configErrorBar.IsOpen = true;
    }

    private static double RequiredDouble(IReadOnlyDictionary<string, object?> values, string key) =>
        Convert.ToDouble(RequiredValue(values, key), CultureInfo.InvariantCulture);

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        object value = RequiredValue(values, key);
        return value is JsonElement element
            ? element.GetBoolean()
            : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        object value = RequiredValue(values, key);
        return value is JsonElement element
            ? element.GetString() ?? throw new FormatException($"配置 {key} 不能为空。")
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new FormatException($"配置 {key} 不能为空。");
    }

    private static object RequiredValue(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is null)
        {
			throw new KeyNotFoundException("缺少配置 " + key + "。");
        }

        return value;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"截图助手页缺少 {name}。");
}
