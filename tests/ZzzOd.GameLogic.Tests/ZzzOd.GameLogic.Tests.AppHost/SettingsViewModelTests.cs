using System.Reflection;
using Avalonia.Controls;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.Gui.PageModels.Settings;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Windows;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Views.FrontierPages.Settings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class SettingsViewModelTests
{
    private sealed class AvailableGamepadDependencyChecker : IVirtualGamepadDependencyChecker
    {
        public bool IsAvailable() => true;
    }

    public class RecordingBackendProxy : DispatchProxy
    {
        public Dictionary<string, object?> CustomValues { get; } = new(StringComparer.Ordinal)
        {
            ["ui_language"] = "zh",
            ["theme"] = "Dark",
            ["gui_shell_preset"] = "mixed",
            ["background_type"] = "static_background",
            ["custom_theme_color"] = false,
            ["custom_banner"] = false,
            ["global_theme_color"] = "71,104,179",
        };

        public Dictionary<string, object?> GameValues { get; } = new(StringComparer.Ordinal)
        {
            ["type_input_way"] = "clipboard",
            ["background_mode"] = false,
            ["background_gamepad_type"] = "xbox",
            ["mouse_flash_duration"] = 0.05d,
            ["launch_argument"] = false,
            ["screen_size"] = "1920x1080",
            ["full_screen"] = "0",
            ["popup_window"] = false,
            ["monitor"] = "1",
            ["launch_argument_advance"] = "",
            ["control_method"] = "keyboard",
            ["xbox_key_press_time"] = 0.02d,
            ["ds4_key_press_time"] = 0.06d,
            ["key_interact"] = "f",
            ["xbox_key_interact"] = "xbox_a",
            ["ds4_key_interact"] = "ds4_cross",
        };

        public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IZzzAppBackend.GetCurrentInstance))
            {
                return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(3, "03", true, "config/03"));
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.GetConfigScope) && args is not null && args[0] is string scope)
            {
                return Snapshot(scope, args[1] as int?);
            }

            if (targetMethod.Name == nameof(IZzzAppBackend.SaveConfigScope) && args is [ZzzSaveConfigScopeRequest request])
            {
                SaveRequests.Add(request);
                Dictionary<string, object?> values = request.Scope == "custom" ? CustomValues : GameValues;
                foreach ((string key, object? value) in request.Values)
                {
                    values[key] = value;
                }

                return Snapshot(request.Scope, request.InstanceIndex);
            }

            throw new NotSupportedException(targetMethod.Name);
        }

        private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope, int? instanceIndex)
        {
            Dictionary<string, object?> values = scope == "custom" ? CustomValues : GameValues;
            ZzzConfigScopeDescriptorDto descriptor = new(scope, scope, scope == "game", false, true, Array.Empty<ZzzConfigSettingDescriptorDto>());
            return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(descriptor, instanceIndex, null, new Dictionary<string, object?>(values, StringComparer.Ordinal)));
        }
    }

    [Fact]
    public void CustomSettingsLoadsStableValuesAndPersistsChanges()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzCustomSettingsViewModel viewModel = new(backend);
        int restartRequests = 0;
        viewModel.RestartRequested += (_, _) => restartRequests++;

        Assert.True(viewModel.Reload());
        Assert.Equal("zh", viewModel.SelectedLanguageValue);
        Assert.Equal("Dark", viewModel.SelectedThemeValue);
        Assert.Equal("static_background", viewModel.SelectedBackgroundTypeValue);
        Assert.Equal(0, restartRequests);

        viewModel.SelectedBackgroundTypeValue = "dynamic_background";

        ZzzSaveConfigScopeRequest request = Assert.Single(proxy.SaveRequests);
        Assert.Equal("custom", request.Scope);
        Assert.Equal("dynamic_background", request.Values["background_type"]);
    }

    [Fact]
    public void GameSettingsLoadRowsRefreshDependentRowsAndUseActiveInstance()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        RecordingBackendProxy proxy = (RecordingBackendProxy)backend;
        ZzzGameSettingsViewModel viewModel = new(backend, new AvailableGamepadDependencyChecker());

        Assert.True(viewModel.Reload());
        Assert.Equal(ZzzGameSettingsViewModel.GameKeyActions.Length, viewModel.KeyboardRows.Count);
        Assert.Equal(ZzzGameSettingsViewModel.GamepadActions.Length, viewModel.BackgroundActionRows.Count);
        Assert.Equal(ZzzGameSettingsViewModel.GameKeyActions.Length, viewModel.GamepadRows.Count);
        Assert.Equal("f", viewModel.KeyboardRows.Single(row => row.Key == "interact").Value);

        viewModel.SelectedBackgroundGamepadType = viewModel.BackgroundGamepadTypeOptions.Single(option => option.Value == "ds4");
        Assert.All(viewModel.BackgroundActionRows, row => Assert.All(row.ButtonOptions, option => Assert.StartsWith("ds4_", option.Value)));

        viewModel.SelectedGamepadDisplay = viewModel.GamepadDisplayOptions.Single(option => option.Value == "ds4");
        Assert.Equal(0.06d, viewModel.GamepadKeyPressTime);
        Assert.Equal("ds4_cross", viewModel.GamepadRows.Single(row => row.Key == "interact").SelectedOption?.Value);

        viewModel.GamepadKeyPressTime = 0.07d;

        Assert.All(proxy.SaveRequests.Where(request => request.Scope == "game"), request => Assert.Equal(3, request.InstanceIndex));
        ZzzSaveConfigScopeRequest keyPressRequest = proxy.SaveRequests.Last(request => request.Values.ContainsKey("ds4_key_press_time"));
        Assert.Equal(0.07d, keyPressRequest.Values["ds4_key_press_time"]);
    }

    [Fact]
    public void SettingsPagesLoadBoundStateOnTheirFirstLifecycleShow()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-settings-mvvm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        try
        {
            GuiParityAndFacadeTests.RunOnUiThread(() =>
            {
                using ZzzGlobalInputMonitor inputMonitor = new();
                FrontierGameSettingsPage gamePage = new(backend, inputMonitor: inputMonitor);
                gamePage.OnPageShown();
                Assert.Equal(ZzzGameSettingsViewModel.GameKeyActions.Length, gamePage.KeyboardRows.Count);
                Assert.Equal("f", gamePage.KeyboardRows.Single(row => row.Key == "interact").Value);
                gamePage.DisposePage();

                FrontierCustomSettingsPage customPage = new(backend, new ZzzLauncherMediaService(new ZzzRunRoot(runRoot), backend));
                customPage.OnPageShown();
                ZzzCustomSettingsViewModel viewModel = Assert.IsType<ZzzCustomSettingsViewModel>(customPage.DataContext);
                Assert.Equal("zh", viewModel.SelectedLanguageValue);
                Assert.Equal("Dark", viewModel.SelectedThemeValue);
                customPage.DisposePage();
            });
        }
        finally
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    [Fact]
    public void CustomSettingsPagesKeepTheirViewModelWhenAttachedToAHostContext()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-settings-datacontext", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        try
        {
            GuiParityAndFacadeTests.RunOnUiThread(() =>
            {
                AssertCustomPageOwnsDataContext(
                    new FrontierCustomSettingsPage(
                        backend,
                        new ZzzLauncherMediaService(new ZzzRunRoot(runRoot), backend)));
            });
        }
        finally
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    [Fact]
    public void CustomSettingsFirstShowSelectsEachClosedComboBoxItem()
    {
        IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
        string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-settings-mvvm", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        try
        {
            GuiParityAndFacadeTests.RunOnUiThread(() =>
            {
                FrontierCustomSettingsPage page = new(backend, new ZzzLauncherMediaService(new ZzzRunRoot(runRoot), backend));
                Window window = new() { Content = page };
                try
                {
                    window.Show();
                    page.OnPageShown();

                AssertSelectedIndexAndLabel(page, "LanguageCombo", 1, "简体中文");
                AssertSelectedIndexAndLabel(page, "ThemeCombo", 2, "深色");
                AssertSelectedIndexAndLabel(page, "BackgroundTypeCombo", 1, "静态背景");
                }
                finally
                {
                    page.DisposePage();
                    window.Close();
                }
            });
        }
        finally
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    private static void AssertSelectedIndexAndLabel(FrontierCustomSettingsPage page, string name, int expectedIndex, string expectedLabel)
    {
        ComboBox comboBox = page.FindControl<ComboBox>(name)!;
        Assert.Equal(expectedIndex, comboBox.SelectedIndex);
        ZzzOd.Gui.PageModels.Settings.ZzzCustomOption option = Assert.IsType<ZzzOd.Gui.PageModels.Settings.ZzzCustomOption>(comboBox.SelectedItem);
        Assert.Equal(expectedLabel, option.Label);
    }

    private static void AssertCustomPageOwnsDataContext(Control page)
    {
        Window window = new()
        {
            DataContext = new object(),
            Content = page,
        };
        try
        {
            window.Show();
            Assert.IsType<ZzzCustomSettingsViewModel>(page.DataContext);
        }
        finally
        {
            if (page is IZzzPageLifecycle lifecycle)
            {
                lifecycle.DisposePage();
            }

            window.Close();
        }
    }
}
