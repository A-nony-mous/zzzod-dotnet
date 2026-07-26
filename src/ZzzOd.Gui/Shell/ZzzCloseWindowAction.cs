using System.Globalization;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Shell;

/// <summary>
/// 点击窗口关闭按钮后的行为。
/// </summary>
public enum ZzzCloseWindowAction
{
    /// <summary>
    /// 隐藏窗口，程序继续在托盘后台运行。
    /// </summary>
    MinimizeToTray,

    /// <summary>
    /// 直接退出程序。
    /// </summary>
    Exit,
}

public static class ZzzCloseWindowActionService
{
    public const string ConfigKey = "close_window_action";

    public static ZzzCloseWindowAction Read(IZzzAppBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ZzzBackendResult<ZzzConfigScopeValuesDto> custom = backend.GetConfigScope("custom");
        return custom.Success && custom.Value is not null
            ? FromValues(custom.Value.Values)
            : ZzzCloseWindowAction.MinimizeToTray;
    }

    public static ZzzCloseWindowAction FromValues(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!values.TryGetValue(ConfigKey, out object? raw))
        {
            return ZzzCloseWindowAction.MinimizeToTray;
        }

        string value = Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return TryParse(value, out ZzzCloseWindowAction action) ? action : ZzzCloseWindowAction.MinimizeToTray;
    }

    public static bool TryParse(string? value, out ZzzCloseWindowAction action)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "tray":
                action = ZzzCloseWindowAction.MinimizeToTray;
                return true;
            case "exit":
                action = ZzzCloseWindowAction.Exit;
                return true;
            default:
                action = ZzzCloseWindowAction.MinimizeToTray;
                return false;
        }
    }

    public static string ToConfigValue(ZzzCloseWindowAction action) => action switch
    {
        ZzzCloseWindowAction.MinimizeToTray => "tray",
        ZzzCloseWindowAction.Exit => "exit",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "未知关闭窗口行为。"),
    };
}
