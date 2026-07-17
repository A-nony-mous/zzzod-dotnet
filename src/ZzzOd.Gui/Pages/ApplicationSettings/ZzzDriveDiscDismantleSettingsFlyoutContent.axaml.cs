using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzDriveDiscOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzDriveDiscDismantleSettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "drive-disc-dismantle";
    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly InfoBar _errorBar;
    private readonly FAComboBox _levelCombo;
    private readonly ToggleSwitch _abandonToggle;
    private bool _loading;

    public ZzzDriveDiscDismantleSettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<InfoBar>("ErrorBar");
        _levelCombo = Required<FAComboBox>("LevelCombo");
        _abandonToggle = Required<ToggleSwitch>("AbandonToggle");
        _levelCombo.ItemsSource = new[]
        {
            new ZzzDriveDiscOption("B", "B"),
            new ZzzDriveDiscOption("A及以下", "A及以下"),
            new ZzzDriveDiscOption("S及以下", "S及以下"),
        };
        Reload();
    }

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
    }

    internal void SaveForTest(string key, object? value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "驱动盘拆解配置读取失败。");
            _loading = false;
            return;
        }

        IReadOnlyDictionary<string, object?> values = result.Value.Values;
        string level = RequiredString(values, "dismantle_level");
        _levelCombo.SelectedItem = _levelCombo.ItemsSource?.OfType<ZzzDriveDiscOption>()
            .FirstOrDefault(option => string.Equals(option.Value, level, StringComparison.Ordinal));
        _abandonToggle.IsChecked = RequiredBool(values, "dismantle_abandon");
        _loading = false;
    }

    private void OnLevelChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _levelCombo.SelectedItem is ZzzDriveDiscOption option)
        {
            Save("dismantle_level", option.Value);
        }
    }

    private void OnAbandonChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading)
        {
            Save("dismantle_abandon", _abandonToggle.IsChecked == true);
        }
    }

    private void Save(string key, object? value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (!result.Success)
        {
            ShowError(result.Error ?? "驱动盘拆解配置保存失败。");
        }
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"驱动盘拆解配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"驱动盘拆解配置缺少 {key}。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"驱动盘拆解设置缺少 {name}。");
}
