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
        if (!resolution.Success || resolution.Preset is ZzzGuiShellPreset.Classic)
        {
            window.TransparencyLevelHint = [WindowTransparencyLevel.None];
            ActualLevel = window.ActualTransparencyLevel;
            return;
        }

        window.TransparencyLevelHint =
        [
            WindowTransparencyLevel.Mica,
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.None,
        ];
        ActualLevel = window.ActualTransparencyLevel;
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                ActualLevel = window.ActualTransparencyLevel;
            }
        };
    }
}
