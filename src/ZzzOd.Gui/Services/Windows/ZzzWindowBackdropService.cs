using Avalonia.Controls;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Services.Windows;

public sealed class ZzzWindowBackdropService
{
    private readonly ZzzGuiShellPresetService _presetService;

    public ZzzWindowBackdropService(ZzzGuiShellPresetService presetService)
    {
        _presetService = presetService;
    }

    public WindowTransparencyLevel? ActualLevel { get; private set; }

    public void Apply(Window window)
    {
        ZzzGuiShellPresetResolution resolution = _presetService.Read();
        window.TransparencyLevelHint = GetTransparencyLevels(
            resolution.Success ? resolution.Preset : ZzzGuiShellPreset.Classic);
        ActualLevel = window.ActualTransparencyLevel;
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                ActualLevel = window.ActualTransparencyLevel;
            }
        };
    }

    internal static IReadOnlyList<WindowTransparencyLevel> GetTransparencyLevels(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => [WindowTransparencyLevel.None],
        ZzzGuiShellPreset.Mixed => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        ZzzGuiShellPreset.Frontier => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
