using System.Globalization;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Shell;

public enum ZzzGuiShellPreset
{
    Classic,
    Frontier,
}

public readonly record struct ZzzGuiShellPresetResolution(
    bool Success,
    ZzzGuiShellPreset Preset,
    string? Error)
{
    public static ZzzGuiShellPresetResolution FromValues(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!values.TryGetValue(ZzzGuiShellPresetService.ConfigKey, out object? raw))
        {
            return new ZzzGuiShellPresetResolution(true, ZzzGuiShellPreset.Classic, null);
        }

        string value = Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return ZzzGuiShellPresetService.TryParse(value, out ZzzGuiShellPreset preset)
            ? new ZzzGuiShellPresetResolution(true, preset, null)
            : new ZzzGuiShellPresetResolution(false, ZzzGuiShellPreset.Classic, $"gui_shell_preset 的值无效: {value}。");
    }
}

public sealed class ZzzGuiShellPresetService
{
    public const string ConfigKey = "gui_shell_preset";

    private readonly IZzzAppBackend _backend;

    public ZzzGuiShellPresetService(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public ZzzGuiShellPresetResolution Read()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> custom = _backend.GetConfigScope("custom");
        if (!custom.Success || custom.Value is null)
        {
            return new ZzzGuiShellPresetResolution(
                false,
                ZzzGuiShellPreset.Classic,
                custom.Error ?? "自定义设置读取失败。");
        }

        return ZzzGuiShellPresetResolution.FromValues(custom.Value.Values);
    }

    public static bool TryParse(string? value, out ZzzGuiShellPreset preset)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "classic":
                preset = ZzzGuiShellPreset.Classic;
                return true;
            case "mixed":
                preset = ZzzGuiShellPreset.Frontier;
                return true;
            case "frontier":
                preset = ZzzGuiShellPreset.Frontier;
                return true;
            default:
                preset = ZzzGuiShellPreset.Classic;
                return false;
        }
    }

    public static string ToConfigValue(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => "classic",
        ZzzGuiShellPreset.Frontier => "frontier",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
