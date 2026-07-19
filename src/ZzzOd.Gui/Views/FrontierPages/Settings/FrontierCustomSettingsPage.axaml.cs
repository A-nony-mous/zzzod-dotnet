using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.Pages.Settings;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed record ZzzCustomOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed partial class FrontierCustomSettingsPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzLauncherMediaService _mediaService;
    private readonly ZzzCustomSettingsViewModel _viewModel;
    private readonly ComboBox _languageCombo;
    private readonly ComboBox _themeCombo;
    private readonly ComboBox _shellPresetCombo;
    private readonly ComboBox _backgroundTypeCombo;
    private readonly ToggleSwitch _customThemeColorToggle;
    private readonly ToggleSwitch _customBannerToggle;
    private readonly TextBox _themeColorPassword;
    private readonly TextBox _customBannerPassword;
    private bool _initializingThemeColorDialog;

    private const string ThemeColorPasswordHash = "b0cd76b7d7829362d581b739c0b295abf53182792609078bb17a9dd917ffba7c";
    private const string CustomBannerPasswordHash = "d678f04ece93caaa4d030696429101725cbf31657dd9ded4fdc3b71b3ee05c54";

    public FrontierCustomSettingsPage(IZzzAppBackend backend, ZzzLauncherMediaService mediaService, ZzzGuiOperationTracker? operations = null)
    {
        _mediaService = mediaService;
        ZzzCustomSettingsViewModel viewModel = new(backend, operations);
        viewModel.RestartRequested += async (sender, language) =>
        {
            FAContentDialog dialog = CreateRestartDialog();
            ConfigureRestartDialog(dialog, language);
            if (TopLevel.GetTopLevel(this) is { } owner
                && await dialog.ShowAsync(owner).ConfigureAwait(true) == FAContentDialogResult.Primary)
            {
                RestartApplication();
            }
        };
        viewModel.ShellRestartRequested += async (sender, _) =>
        {
            FAContentDialog dialog = CreateRestartDialog();
            ConfigureShellRestartDialog(dialog);
            if (TopLevel.GetTopLevel(this) is { } owner
                && await dialog.ShowAsync(owner).ConfigureAwait(true) == FAContentDialogResult.Primary)
            {
                RestartApplication();
            }
        };
        viewModel.ThemeColorEditorRequested += ShowThemeColorEditorAsync;
        viewModel.CustomBannerSelectionRequested += SelectCustomBannerAsync;
        AvaloniaXamlLoader.Load(this);
        _viewModel = viewModel;
        DataContext = _viewModel;
        _customThemeColorToggle = Required<ToggleSwitch>("CustomThemeColorToggle");
        _customBannerToggle = Required<ToggleSwitch>("CustomBannerToggle");
        _themeColorPassword = Required<TextBox>("ThemeColorPassword");
        _customBannerPassword = Required<TextBox>("CustomBannerPassword");
        _languageCombo = Required<ComboBox>("LanguageCombo");
        _themeCombo = Required<ComboBox>("ThemeCombo");
        _shellPresetCombo = Required<ComboBox>("ShellPresetCombo");
        _backgroundTypeCombo = Required<ComboBox>("BackgroundTypeCombo");
    }

    public void OnPageShown()
    {
        _viewModel.OnPageShown();
        ApplySelectedOptions();
    }
    public void OnPageHidden() => (DataContext as IZzzPageLifecycle)?.OnPageHidden();
    public void OnPageLeave() => (DataContext as IZzzPageLifecycle)?.OnPageLeave();
    public void DisposePage() => (DataContext as IZzzPageLifecycle)?.DisposePage();

    internal void SetLanguageForTest(string value)
    {
        if (DataContext is ZzzCustomSettingsViewModel vm)
        {
            vm.SelectedLanguageValue = value;
        }
    }

    internal void SetThemeForTest(string value)
    {
        if (DataContext is ZzzCustomSettingsViewModel vm)
        {
            vm.SelectedThemeValue = value;
        }
    }

    internal async Task<string> SaveCustomBackgroundForTest(string path)
    {
        string saved = await _mediaService.SaveCustomBackgroundAsync(path).ConfigureAwait(true);
        if (DataContext is not ZzzCustomSettingsViewModel viewModel || !viewModel.PersistCustomBanner())
        {
            throw new InvalidOperationException("自定义主页背景保存失败。");
        }
        return saved;
    }

    internal static bool VerifyPasswordForTest(string password, string expectedHash) => VerifyPassword(password, expectedHash);

    private void OnCustomThemeColorChanged(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ZzzCustomSettingsViewModel vm || vm.IsLoading)
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

        vm.CustomThemeColor = enabled;
    }

    private async void ShowThemeColorEditorAsync(object? sender, EventArgs args)
    {
        if (DataContext is not ZzzCustomSettingsViewModel viewModel || !viewModel.CustomThemeColor)
        {
            return;
        }

        byte[] rgb = ParseRgb(viewModel.GlobalThemeColor);
        FAContentDialog dialog = (FAContentDialog)Resources["ThemeColorDialog"]!;
        FANumberBox red = dialog.FindControl<FANumberBox>("RedNumber")!;
        FANumberBox green = dialog.FindControl<FANumberBox>("GreenNumber")!;
        FANumberBox blue = dialog.FindControl<FANumberBox>("BlueNumber")!;
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

        if (TopLevel.GetTopLevel(this) is { } owner)
        {
            await dialog.ShowAsync(owner).ConfigureAwait(true);
        }
    }

    private void OnThemeColorValueChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (_initializingThemeColorDialog || DataContext is not ZzzCustomSettingsViewModel viewModel || !viewModel.CustomThemeColor)
        {
            return;
        }

        FAContentDialog dialog = (FAContentDialog)Resources["ThemeColorDialog"]!;
        byte r = ClampColor(dialog.FindControl<FANumberBox>("RedNumber")!.Value);
        byte g = ClampColor(dialog.FindControl<FANumberBox>("GreenNumber")!.Value);
        byte b = ClampColor(dialog.FindControl<FANumberBox>("BlueNumber")!.Value);
        if (viewModel.SaveThemeColor($"{r},{g},{b}"))
        {
            App.ApplyAccentColor(Avalonia.Media.Color.FromRgb(r, g, b));
        }
    }



    private void OnCustomBannerChanged(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not ZzzCustomSettingsViewModel vm || vm.IsLoading)
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

        vm.CustomBanner = enabled;
    }

    private async void SelectCustomBannerAsync(object? sender, EventArgs args)
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
            (DataContext as ZzzCustomSettingsViewModel)?.ReportError(exception.Message);
        }
    }

    private async Task ShowPasswordErrorAsync()
    {
        FAContentDialog dialog = (FAContentDialog)Resources["PasswordErrorDialog"]!;
        if (TopLevel.GetTopLevel(this) is { } owner)
        {
            await dialog.ShowAsync(owner).ConfigureAwait(true);
        }
    }

    private static void ConfigureRestartDialog(FAContentDialog dialog, string language)
    {
        bool english = string.Equals(language, "en", StringComparison.Ordinal);
        dialog.Title = english ? "Notice" : "提示";
        dialog.Content = english
            ? "Language changed successfully. Please restart the application for changes to take effect."
            : "语言切换成功，需要重启应用程序以生效";
        dialog.PrimaryButtonText = english ? "Restart Now" : "立即重启";
        dialog.CloseButtonText = english ? "Restart Later" : "稍后重启";
    }

    private static FAContentDialog CreateRestartDialog() => new()
    {
        Title = "提示",
        PrimaryButtonText = "立即重启",
        CloseButtonText = "稍后重启",
        DefaultButton = FAContentDialogButton.Primary,
    };

    private void ApplySelectedOptions()
    {
        _languageCombo.ItemsSource = _viewModel.LanguageOptions;
        _themeCombo.ItemsSource = _viewModel.ThemeOptions;
        _shellPresetCombo.ItemsSource = _viewModel.ShellPresetOptions;
        _backgroundTypeCombo.ItemsSource = _viewModel.BackgroundTypeOptions;
        _languageCombo.SelectedItem = _viewModel.SelectedLanguage;
        _themeCombo.SelectedItem = _viewModel.SelectedTheme;
        _shellPresetCombo.SelectedItem = _viewModel.SelectedShellPreset;
        _backgroundTypeCombo.SelectedItem = _viewModel.SelectedBackgroundType;
    }

    private static void ConfigureShellRestartDialog(FAContentDialog dialog)
    {
        dialog.Title = "提示";
        dialog.Content = "界面样式已保存，重启应用后生效";
        dialog.PrimaryButtonText = "立即重启";
        dialog.CloseButtonText = "稍后重启";
    }

    private static void RestartApplication()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            ProcessStartInfo startInfo = new(path) { UseShellExecute = true };
            foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!startInfo.ArgumentList.Any(argument => string.Equals(argument, Program.RestartArgument, StringComparison.Ordinal)))
            {
                startInfo.ArgumentList.Add(Program.RestartArgument);
            }

            Process.Start(startInfo);
        }

        App.ExitForRestart();
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
