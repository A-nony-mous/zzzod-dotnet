using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Settings;

internal sealed record ZzzCustomOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzCustomSettingsAxamlPage : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "custom";
    private readonly IZzzAppBackend _backend;
    private readonly ZzzLauncherMediaService _mediaService;
    private readonly ZzzGuiOperationTracker _operations;
    private readonly InfoBar _errorBar;
    private readonly FAComboBox _languageCombo;
    private readonly FAComboBox _themeCombo;
    private readonly FAComboBox _visualPresetCombo;
    private readonly FAComboBox _backgroundTypeCombo;
    private readonly ToggleSwitch _customThemeColorToggle;
    private readonly ToggleSwitch _customBannerToggle;
    private readonly TextBox _themeColorPassword;
    private readonly TextBox _customBannerPassword;
    private readonly Button _chooseThemeColorButton;
    private readonly Button _selectBannerButton;
    private bool _loading;
    private bool _initializingThemeColorDialog;

    private const string ThemeColorPasswordHash = "b0cd76b7d7829362d581b739c0b295abf53182792609078bb17a9dd917ffba7c";
    private const string CustomBannerPasswordHash = "d678f04ece93caaa4d030696429101725cbf31657dd9ded4fdc3b71b3ee05c54";

    public ZzzCustomSettingsAxamlPage(IZzzAppBackend backend, ZzzLauncherMediaService mediaService, ZzzGuiOperationTracker? operations = null)
    {
        _backend = backend;
        _mediaService = mediaService;
        _operations = operations ?? new ZzzGuiOperationTracker();
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<InfoBar>("ErrorBar");
        _languageCombo = Required<FAComboBox>("LanguageCombo");
        _themeCombo = Required<FAComboBox>("ThemeCombo");
        _visualPresetCombo = Required<FAComboBox>("VisualPresetCombo");
        _backgroundTypeCombo = Required<FAComboBox>("BackgroundTypeCombo");
        _customThemeColorToggle = Required<ToggleSwitch>("CustomThemeColorToggle");
        _customBannerToggle = Required<ToggleSwitch>("CustomBannerToggle");
        _themeColorPassword = Required<TextBox>("ThemeColorPassword");
        _customBannerPassword = Required<TextBox>("CustomBannerPassword");
        _chooseThemeColorButton = Required<Button>("ChooseThemeColorButton");
        _selectBannerButton = Required<Button>("SelectBannerButton");

        _languageCombo.ItemsSource = Options(("跟随系统", "auto"), ("简体中文", "zh"), ("English", "en"));
        _themeCombo.ItemsSource = Options(("跟随系统", "Auto"), ("浅色", "Light"), ("深色", "Dark"));
        _visualPresetCombo.ItemsSource = Options(("Baseline 兼容", "baseline-parity"), ("Store Fluent", "store-fluent"));
        _backgroundTypeCombo.ItemsSource = Options(
            ("版本海报", "version_poster"),
            ("静态背景", "static_background"),
            ("动态背景", "dynamic_background"),
            ("无", "none"));
    }

    public void OnPageShown()
    {
        Guid operationId = _operations.Start("settings-custom", "reload-custom-settings");
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

    internal void SetLanguageForTest(string value) => SelectAndSave(_languageCombo, value, "ui_language");

    internal void SetThemeForTest(string value) => SelectAndSave(_themeCombo, value, "theme");

    internal async Task<string> SaveCustomBackgroundForTest(string path)
    {
        string saved = await _mediaService.SaveCustomBackgroundAsync(path).ConfigureAwait(true);
        ZzzBackendResult<ZzzConfigScopeValuesDto> current = _backend.GetConfigScope(ScopeName);
        if (!current.Success || current.Value is null)
        {
			throw new InvalidOperationException(current.Error ?? "自定义设置读取失败。");
        }

        Save("custom_banner", RequiredBool(current.Value.Values, "custom_banner"));
        return saved;
    }

    internal static bool VerifyPasswordForTest(string password, string expectedHash) => VerifyPassword(password, expectedHash);

    private bool Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName);
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "自定义设置读取失败。");
            _loading = false;
            return false;
        }

        IReadOnlyDictionary<string, object?> values = result.Value.Values;
        try
        {
            Select(_languageCombo, RequiredString(values, "ui_language"));
            Select(_themeCombo, RequiredString(values, "theme"));
            Select(_visualPresetCombo, ReadString(values, "fluent_visual_preset", "baseline-parity"));
            Select(_backgroundTypeCombo, RequiredString(values, "background_type"));
            _customThemeColorToggle.IsChecked = RequiredBool(values, "custom_theme_color");
            _customBannerToggle.IsChecked = RequiredBool(values, "custom_banner");
            _chooseThemeColorButton.IsEnabled = _customThemeColorToggle.IsChecked == true;
            _selectBannerButton.IsEnabled = _customBannerToggle.IsChecked == true;
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _loading = false;
        }

        return !_errorBar.IsOpen;
    }

    private async void OnLanguageChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _languageCombo.SelectedItem is not ZzzCustomOption option || !Save("ui_language", option.Value))
        {
            return;
        }

        ContentDialog dialog = (ContentDialog)Resources["RestartDialog"]!;
        ConfigureRestartDialog(dialog, option.Value);
        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
        {
            RestartApplication();
        }
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _themeCombo.SelectedItem is not ZzzCustomOption option || !Save("theme", option.Value))
        {
            return;
        }

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = option.Value switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    private void OnVisualPresetChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _visualPresetCombo.SelectedItem is not ZzzCustomOption option || !Save("fluent_visual_preset", option.Value))
        {
            return;
        }

        App.ApplyVisualPreset(option.Value);
    }

    private void OnCustomThemeColorChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        bool enabled = _customThemeColorToggle.IsChecked == true;
        if (enabled && !VerifyPassword(_themeColorPassword.Text ?? string.Empty, ThemeColorPasswordHash))
        {
            _customThemeColorToggle.IsChecked = false;
            _ = ShowPasswordErrorAsync();
            return;
        }

        if (Save("custom_theme_color", enabled))
        {
            _chooseThemeColorButton.IsEnabled = enabled;
        }
    }

    private async void OnChooseThemeColorClicked(object? sender, RoutedEventArgs args)
    {
        if (!_customThemeColorToggle.IsChecked.GetValueOrDefault())
        {
            return;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> current = _backend.GetConfigScope(ScopeName);
        if (!current.Success || current.Value is null)
        {
			ShowError(current.Error ?? "自定义设置读取失败。");
            return;
        }

        string value = RequiredString(current.Value.Values, "global_theme_color");
        byte[] rgb = ParseRgb(value);
        ContentDialog dialog = (ContentDialog)Resources["ThemeColorDialog"]!;
        NumberBox red = dialog.FindControl<NumberBox>("RedNumber")!;
        NumberBox green = dialog.FindControl<NumberBox>("GreenNumber")!;
        NumberBox blue = dialog.FindControl<NumberBox>("BlueNumber")!;
        _initializingThemeColorDialog = true;
        try
        {
            red.Value = rgb[0];
            green.Value = rgb[1];
            blue.Value = rgb[2];
        }
        finally
        {
            _initializingThemeColorDialog = false;
        }

        await dialog.ShowAsync().ConfigureAwait(true);
    }

    private void OnThemeColorValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_initializingThemeColorDialog || !_customThemeColorToggle.IsChecked.GetValueOrDefault())
        {
            return;
        }

        ContentDialog dialog = (ContentDialog)Resources["ThemeColorDialog"]!;
        byte r = ClampColor(dialog.FindControl<NumberBox>("RedNumber")!.Value);
        byte g = ClampColor(dialog.FindControl<NumberBox>("GreenNumber")!.Value);
        byte b = ClampColor(dialog.FindControl<NumberBox>("BlueNumber")!.Value);
        if (Save("global_theme_color", $"{r},{g},{b}"))
        {
            App.ApplyAccentColor(Avalonia.Media.Color.FromRgb(r, g, b));
        }
    }

    private void OnBackgroundTypeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _backgroundTypeCombo.SelectedItem is ZzzCustomOption option)
        {
            Save("background_type", option.Value);
        }
    }

    private void OnCustomBannerChanged(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        bool enabled = _customBannerToggle.IsChecked == true;
        if (enabled && !VerifyPassword(_customBannerPassword.Text ?? string.Empty, CustomBannerPasswordHash))
        {
            _customBannerToggle.IsChecked = false;
            _ = ShowPasswordErrorAsync();
            return;
        }

        if (Save("custom_banner", enabled))
        {
            _selectBannerButton.IsEnabled = enabled;
        }
    }

    private async void OnSelectBannerClicked(object? sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择你的背景图片",
            FileTypeFilter =
            [
                new FilePickerFileType("Images and Videos")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.avi", "*.mov", "*.webm", "*.mp4", "*.mkv"],
                },
            ],
        }).ConfigureAwait(true);
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await SaveCustomBackgroundForTest(path).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(exception.Message);
        }
    }

    private void SelectAndSave(SelectingItemsControl combo, string value, string key)
    {
        Select(combo, value);
        Save(key, value);
    }

    private bool Save(string key, object? value) => Save(new Dictionary<string, object?> { [key] = value });

    private bool Save(IReadOnlyDictionary<string, object?> values)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(ScopeName, values));
        if (!result.Success)
        {
            ShowError(result.Error ?? "自定义设置保存失败。");
        }

        return result.Success;
    }

    private void ShowError(string message)
    {
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private async Task ShowPasswordErrorAsync()
    {
        ContentDialog dialog = (ContentDialog)Resources["PasswordErrorDialog"]!;
        await dialog.ShowAsync().ConfigureAwait(true);
    }

    private static void ConfigureRestartDialog(ContentDialog dialog, string language)
    {
        bool english = string.Equals(language, "en", StringComparison.Ordinal);
        dialog.Title = english ? "Notice" : "提示";
        dialog.Content = english
            ? "Language changed successfully. Please restart the application for changes to take effect."
            : "语言切换成功，需要重启应用程序以生效";
        dialog.PrimaryButtonText = english ? "Restart Now" : "立即重启";
        dialog.CloseButtonText = english ? "Restart Later" : "稍后重启";
    }

    private static void RestartApplication()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private static IReadOnlyList<ZzzCustomOption> Options(params (string Label, string Value)[] values) =>
        values.Select(value => new ZzzCustomOption(value.Label, value.Value)).ToArray();

    private static void Select(SelectingItemsControl combo, string value)
    {
        combo.SelectedItem = combo.ItemsSource?.OfType<ZzzCustomOption>()
            .FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("自定义设置缺少 " + key + "。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> values, string key, string defaultValue) =>
        values.TryGetValue(key, out object? value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? defaultValue : defaultValue;

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("自定义设置缺少 " + key + "。");
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static byte[] ParseRgb(string value)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || !byte.TryParse(parts[0], out byte red)
            || !byte.TryParse(parts[1], out byte green)
            || !byte.TryParse(parts[2], out byte blue))
        {
            throw new InvalidOperationException("global_theme_color 必须是 0..255 的 r,g,b。 ");
        }

        return [red, green, blue];
    }

    private static byte ClampColor(double value) => (byte)Math.Clamp((int)value, 0, 255);

    private static bool VerifyPassword(string password, string expectedHash)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return string.Equals(Convert.ToHexString(hash), expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"自定义设置页缺少 {name}。");
}

