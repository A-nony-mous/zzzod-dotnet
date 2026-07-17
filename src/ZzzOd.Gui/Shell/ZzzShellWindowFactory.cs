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

        return resolution.Preset switch
        {
            ZzzGuiShellPreset.Classic => ActivatorUtilities.CreateInstance<MainWindow>(_services),
            ZzzGuiShellPreset.Mixed => ActivatorUtilities.CreateInstance<MixedShellWindow>(_services),
            ZzzGuiShellPreset.Frontier => ActivatorUtilities.CreateInstance<FrontierShellWindow>(_services),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
