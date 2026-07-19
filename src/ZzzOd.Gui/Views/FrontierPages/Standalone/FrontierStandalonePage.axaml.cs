using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;
using ZzzOd.Gui.Views.FrontierPages;

namespace ZzzOd.Gui.Views.FrontierPages.Standalone;

internal sealed partial class FrontierStandalonePage : FrontierEmbeddedPage
{
    public FrontierStandalonePage(
        IZzzAppBackend backend,
        ZzzGuiRunIntentService runIntent,
        ZzzGuiOperationTracker? operations = null)
        : this(CreateContent(backend, runIntent, operations))
    {
    }

    public FrontierStandalonePage(Control content)
        : base(content)
    {
        AvaloniaXamlLoader.Load(this);
        SetContent(this.FindControl<ContentControl>("ContentHost")
            ?? throw new InvalidOperationException("Frontier 独立运行页缺少内容承载。"));
    }

    private static Control CreateContent(
        IZzzAppBackend backend,
        ZzzGuiRunIntentService runIntent,
        ZzzGuiOperationTracker? operations)
    {
        FrontierAppSettingPageFactory appSettingFactory = new(backend);
        FrontierStandaloneAppRunPage runPage = new(
            backend,
            runIntent,
            operations,
            appSettingFactory.Create);
        ZzzPivotPage container = new([new("应用运行", runPage)]);
        runPage.SecondaryPageRequested += (_, content) => container.PushSecondary("应用设置", content);
        return container;
    }
}
