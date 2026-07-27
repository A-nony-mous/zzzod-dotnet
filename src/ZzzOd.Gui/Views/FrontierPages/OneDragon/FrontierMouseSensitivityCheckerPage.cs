using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.PageModels.OneDragon;

namespace ZzzOd.Gui.Views.FrontierPages.OneDragon;

internal sealed partial class FrontierMouseSensitivityCheckerPage : UserControl, IZzzPageLifecycle
{
    public FrontierMouseSensitivityCheckerPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.MouseSensitivityChecker,
            runIntent: runIntent);
        AvaloniaXamlLoader.Load(this);
        ContentControl runHost = this.FindControl<ContentControl>("SensitivityRunHost")
            ?? throw new InvalidOperationException("灵敏度校准页缺少运行区域。");
        runHost.Content = RunPanel;
    }

    public ZzzRunPanel RunPanel { get; }

    public ZzzOneDragonPageModel PageModel => new(
        "one-dragon-sensitivity",
        "一条龙 / 灵敏度校准",
        ["使用说明", "开始/停止", "当前状态", "日志显示"],
        [ZzzApplicationIds.MouseSensitivityChecker],
        []);

    public void OnPageShown() => RunPanel.OnPageShown();

    public void OnPageHidden() => RunPanel.OnPageHidden();

    public void OnPageLeave() => RunPanel.OnPageLeave();

    public void DisposePage() => RunPanel.DisposePage();
}
