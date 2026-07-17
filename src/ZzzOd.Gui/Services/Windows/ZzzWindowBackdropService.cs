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

    public event EventHandler<WindowTransparencyLevel?>? ActualLevelChanged;

    public void Apply(Window window)
    {
        ZzzGuiShellPresetResolution resolution = _presetService.Read();
        window.TransparencyLevelHint = GetTransparencyLevels(
            resolution.Success ? resolution.Preset : ZzzGuiShellPreset.Classic);
        UpdateActualLevel(window.ActualTransparencyLevel);
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                UpdateActualLevel(window.ActualTransparencyLevel);
            }
        };
    }

    private void UpdateActualLevel(WindowTransparencyLevel? level)
    {
        if (ActualLevel == level)
        {
            return;
        }

        ActualLevel = level;
        ActualLevelChanged?.Invoke(this, level);
    }

    internal static IReadOnlyList<WindowTransparencyLevel> GetTransparencyLevels(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => [WindowTransparencyLevel.None],
        ZzzGuiShellPreset.Mixed => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        ZzzGuiShellPreset.Frontier => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
