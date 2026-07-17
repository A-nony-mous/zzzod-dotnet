using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZzzOd.Gui.Views;

namespace ZzzOd.Gui.Shell;

public sealed class ZzzShellWindowFactory
{
    private readonly IServiceProvider _services;
    private readonly ZzzGuiShellPresetService _presetService;
    private readonly ILogger<ZzzShellWindowFactory> _logger;

    public ZzzShellWindowFactory(IServiceProvider services, ZzzGuiShellPresetService presetService, ILogger<ZzzShellWindowFactory> logger)
    {
        _services = services;
        _presetService = presetService;
        _logger = logger;
    }

    public Window Create()
    {
        ZzzGuiShellPresetResolution resolution = _presetService.Read();
        if (!resolution.Success)
        {
            throw new InvalidOperationException(resolution.Error);
        }

        Type windowType = GetWindowType(resolution.Preset);
        _logger.LogInformation("创建 {ShellPreset} Shell 窗口 {WindowType}", resolution.Preset, windowType.Name);
        return (Window)ActivatorUtilities.CreateInstance(_services, windowType);
    }

    internal static Type GetWindowType(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => typeof(MainWindow),
        ZzzGuiShellPreset.Mixed => typeof(MixedShellWindow),
        ZzzGuiShellPreset.Frontier => typeof(FrontierShellWindow),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
