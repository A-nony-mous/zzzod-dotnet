using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Services.Windows;

namespace ZzzOd.Gui.Views.FrontierPages.Settings;

internal sealed class ZzzEnvironmentSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField ScreenshotMethodField = new(
        "screenshot_method",
        typeof(string),
        new EnvConfig().ScreenshotMethod);
    private static readonly ZzzConfigField IsDebugField = new("is_debug", typeof(bool), false);
    private static readonly ZzzConfigField CopyScreenshotField = new("copy_screenshot", typeof(bool), true);
    private static readonly ZzzConfigField ProxyTypeField = new("proxy_type", typeof(string), "None");
    private static readonly ZzzConfigField PersonalProxyField = new("personal_proxy", typeof(string), string.Empty);
    private static readonly ZzzConfigField KeyStartRunningField = new("key_start_running", typeof(string), "f9");
    private static readonly ZzzConfigField KeyStopRunningField = new("key_stop_running", typeof(string), "f10");
    private static readonly ZzzConfigField KeyScreenshotField = new("key_screenshot", typeof(string), "f11");
    private static readonly ZzzConfigField KeyDebugField = new("key_debug", typeof(string), "f12");
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        ScreenshotMethodField,
        IsDebugField,
        CopyScreenshotField,
        ProxyTypeField,
        PersonalProxyField,
        KeyStartRunningField,
        KeyStopRunningField,
        KeyScreenshotField,
        KeyDebugField,
    ];

    private readonly IZzzEnvironmentRuntimeCoordinator? _runtimeCoordinator;
    private readonly Func<Task>? _reinitializeContextAsync;

    public ZzzEnvironmentSettingsViewModel(
        IZzzAppBackend backend,
        IReadOnlyList<ZzzEnvironmentOption> screenshotMethods,
        IReadOnlyList<ZzzEnvironmentOption> proxyTypes,
        IZzzEnvironmentRuntimeCoordinator? runtimeCoordinator = null,
        Action<string?>? errorReporter = null,
        Func<Task>? reinitializeContextAsync = null)
        : base(backend, errorReporter)
    {
        ScreenshotMethods = screenshotMethods;
        ProxyTypes = proxyTypes;
        _runtimeCoordinator = runtimeCoordinator;
        _reinitializeContextAsync = reinitializeContextAsync;
    }

    protected override string ScopeName => "env";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public IReadOnlyList<ZzzEnvironmentOption> ScreenshotMethods { get; }

    public IReadOnlyList<ZzzEnvironmentOption> ProxyTypes { get; }

    public string ScreenshotMethod
    {
        get => GetValue<string>(ScreenshotMethodField);
        set
        {
            if (SetValue(ScreenshotMethodField, value))
            {
                OnPropertyChanged(nameof(SelectedScreenshotMethod));
            }
        }
    }

    public ZzzEnvironmentOption? SelectedScreenshotMethod
    {
        get => FindOption(ScreenshotMethods, NormalizeScreenshotMethodForDisplay(ScreenshotMethod));
        set
        {
            if (value is not null)
            {
                ScreenshotMethod = value.Value;
            }
        }
    }

    public bool IsDebug
    {
        get => GetValue<bool>(IsDebugField);
        set
        {
            if (SetValue(IsDebugField, value) && !IsLoading && LastError is null)
            {
                _ = _reinitializeContextAsync is null
                    ? ReinitializeContextAsync()
                    : _reinitializeContextAsync();
            }
        }
    }

    public bool CopyScreenshot
    {
        get => GetValue<bool>(CopyScreenshotField);
        set => SetValue(CopyScreenshotField, value);
    }

    public string ProxyType
    {
        get => GetValue<string>(ProxyTypeField);
        set
        {
            if (SetValue(ProxyTypeField, value))
            {
                OnPropertyChanged(nameof(SelectedProxyType));
                OnPropertyChanged(nameof(PersonalProxyVisible));
                if (!IsLoading && LastError is null)
                {
                    ApplyProcessProxy(ProxyType, PersonalProxy);
                }
            }
        }
    }

    public ZzzEnvironmentOption? SelectedProxyType
    {
        get => FindOption(ProxyTypes, ProxyType);
        set
        {
            if (value is not null)
            {
                ProxyType = value.Value;
            }
        }
    }

    public bool PersonalProxyVisible => string.Equals(ProxyType, "personal", StringComparison.Ordinal);

    public string PersonalProxy
    {
        get => GetValue<string>(PersonalProxyField);
        set
        {
            if (SetValue(PersonalProxyField, value) && !IsLoading && LastError is null)
            {
                ApplyProcessProxy(ProxyType, PersonalProxy);
            }
        }
    }

    public string KeyStartRunning
    {
        get => GetValue<string>(KeyStartRunningField);
        set => SetValue(KeyStartRunningField, value);
    }

    public string KeyStopRunning
    {
        get => GetValue<string>(KeyStopRunningField);
        set => SetValue(KeyStopRunningField, value);
    }

    public string KeyScreenshot
    {
        get => GetValue<string>(KeyScreenshotField);
        set => SetValue(KeyScreenshotField, value);
    }

    public string KeyDebug
    {
        get => GetValue<string>(KeyDebugField);
        set => SetValue(KeyDebugField, value);
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is null)
        {
            ApplyProcessProxy(ProxyType, PersonalProxy);
        }
    }

    public bool SaveString(string key, string value)
    {
        switch (key)
        {
            case "screenshot_method": ScreenshotMethod = value; break;
            case "proxy_type": ProxyType = value; break;
            case "personal_proxy": PersonalProxy = value; break;
            case "key_start_running": KeyStartRunning = value; break;
            case "key_stop_running": KeyStopRunning = value; break;
            case "key_screenshot": KeyScreenshot = value; break;
            case "key_debug": KeyDebug = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "未知脚本环境配置项。");
        }

        return LastError is null;
    }

    public string GetHotkey(string key) => key switch
    {
        "key_start_running" => KeyStartRunning,
        "key_stop_running" => KeyStopRunning,
        "key_screenshot" => KeyScreenshot,
        "key_debug" => KeyDebug,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知脚本按键配置项。"),
    };

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        _runtimeCoordinator?.UpdateEnvironmentConfiguration(values);
        OnPropertyChanged(nameof(SelectedScreenshotMethod));
        OnPropertyChanged(nameof(SelectedProxyType));
        OnPropertyChanged(nameof(PersonalProxyVisible));
    }

    protected override void OnFieldSaved(ZzzConfigField field, ZzzConfigScopeValuesDto values) =>
        _runtimeCoordinator?.UpdateEnvironmentConfiguration(values);

    private async Task ReinitializeContextAsync()
    {
        if (_runtimeCoordinator is null)
        {
            return;
        }

        try
        {
            ZzzBackendResult<bool> result = await _runtimeCoordinator.ReinitializeContextAsync();
            if (!result.Success)
            {
                ReportError(result.Error ?? "脚本环境重新初始化失败。");
            }
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private static ZzzEnvironmentOption? FindOption(
        IReadOnlyList<ZzzEnvironmentOption> options,
        string value) =>
        options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));

    private static string NormalizeScreenshotMethodForDisplay(string value) => value switch
    {
        "mss" or "pil" => "bitblt",
        "dwm_shared_surface" => "auto",
        _ => value,
    };

    private static void ApplyProcessProxy(string? proxyType, string personalProxy)
    {
        string value = string.Equals(proxyType, "personal", StringComparison.Ordinal) ? personalProxy : string.Empty;
        Environment.SetEnvironmentVariable("HTTP_PROXY", value);
        Environment.SetEnvironmentVariable("HTTPS_PROXY", value);
    }
}
