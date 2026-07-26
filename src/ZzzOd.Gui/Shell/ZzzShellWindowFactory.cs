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
        // Shell 只有前卫一种;仍读取配置是为了把未注册的 gui_shell_preset 值以真实配置错误上报。
        ZzzGuiShellPresetResolution resolution = _presetService.Read();
        if (!resolution.Success)
        {
            string error = resolution.Error ?? "GUI Shell 配置读取失败。";
            _logger.LogError("{ShellPresetError} 使用前卫 Shell 启动", error);
            _services.GetRequiredService<ZzzShellViewModel>().ReportStartupError(error);
        }

        _logger.LogInformation("创建前卫 Shell 窗口 {WindowType}", nameof(FrontierShellWindow));
        Window window = (Window)ActivatorUtilities.CreateInstance(_services, typeof(FrontierShellWindow));
        _logger.LogInformation("已创建前卫 Shell 窗口 {WindowType}", nameof(FrontierShellWindow));
        return window;
    }
}
