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
        ZzzGuiShellPreset preset = resolution.Preset;
        if (!resolution.Success)
        {
            string error = resolution.Error ?? "GUI Shell 配置读取失败。";
            _logger.LogError("{ShellPresetError} 使用经典 Shell 启动", error);
            _services.GetRequiredService<ZzzShellViewModel>().ReportStartupError(error);
            preset = ZzzGuiShellPreset.Classic;
        }

        Type windowType = GetWindowType(preset);
        _logger.LogInformation("创建 {ShellPreset} Shell 窗口 {WindowType}", preset, windowType.Name);
        Window window = (Window)ActivatorUtilities.CreateInstance(_services, windowType);
        _logger.LogInformation("已创建 {ShellPreset} Shell 窗口 {WindowType}", preset, windowType.Name);
        return window;
    }

    internal static Type GetWindowType(ZzzGuiShellPreset preset) => preset switch
    {
        ZzzGuiShellPreset.Classic => typeof(MainWindow),
        ZzzGuiShellPreset.Frontier => typeof(FrontierShellWindow),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知 GUI Shell 预设。"),
    };
}
