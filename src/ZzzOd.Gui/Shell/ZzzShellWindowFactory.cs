using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.Gui.Views;

namespace ZzzOd.Gui.Shell;

public sealed class ZzzShellWindowFactory
{
    private readonly IServiceProvider _services;
    private readonly ZzzGuiShellPresetService _presetService;

    public ZzzShellWindowFactory(IServiceProvider services, ZzzGuiShellPresetService presetService)
    {
        _services = services;
        _presetService = presetService;
    }

    public Window Create()
    {
        ZzzGuiShellPresetResolution resolution = _presetService.Read();
        if (!resolution.Success)
        {
            throw new InvalidOperationException(resolution.Error);
        }

        return (Window)ActivatorUtilities.CreateInstance(_services, GetWindowType(resolution.Preset));
    }

    internal static Type GetWindowType(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => typeof(MainWindow),
        ZzzGuiShellPreset.Mixed => typeof(MixedShellWindow),
        ZzzGuiShellPreset.Frontier => typeof(FrontierShellWindow),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
