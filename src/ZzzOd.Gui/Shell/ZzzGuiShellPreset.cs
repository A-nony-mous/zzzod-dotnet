using System.Globalization;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Shell;

public enum ZzzGuiShellPreset
{
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
            return new ZzzGuiShellPresetResolution(true, ZzzGuiShellPreset.Frontier, null);
        }

        string value = Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return ZzzGuiShellPresetService.TryParse(value, out ZzzGuiShellPreset preset)
            ? new ZzzGuiShellPresetResolution(true, preset, null)
            : new ZzzGuiShellPresetResolution(false, ZzzGuiShellPreset.Frontier, $"gui_shell_preset 的值无效: {value}。");
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
                ZzzGuiShellPreset.Frontier,
                custom.Error ?? "自定义设置读取失败。");
        }

        ZzzGuiShellPresetResolution resolution = ZzzGuiShellPresetResolution.FromValues(custom.Value.Values);
        NormalizeLegacyValue(custom.Value.Values, resolution);
        return resolution;
    }

    private void NormalizeLegacyValue(IReadOnlyDictionary<string, object?> values, ZzzGuiShellPresetResolution resolution)
    {
        // classic/mixed 历史值解析成功后归一写回 frontier;写回失败不影响启动。
        if (!resolution.Success
            || !values.TryGetValue(ConfigKey, out object? raw)
            || Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant() is null or "frontier")
        {
            return;
        }

        try
        {
            _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
                "custom",
                new Dictionary<string, object?> { [ConfigKey] = ToConfigValue(ZzzGuiShellPreset.Frontier) }));
        }
        catch (Exception)
        {
            // 归一化是尽力而为:任何后端异常都不得阻断 Shell 创建。
        }
    }

    public static bool TryParse(string? value, out ZzzGuiShellPreset preset)
    {
        // classic/mixed 是历史取值:一律落前卫且视为解析成功,保证旧配置文件不产生硬失败。
        preset = ZzzGuiShellPreset.Frontier;
        return value?.Trim().ToLowerInvariant() is "classic" or "mixed" or "frontier";
    }

    public static string ToConfigValue(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Frontier => "frontier",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
