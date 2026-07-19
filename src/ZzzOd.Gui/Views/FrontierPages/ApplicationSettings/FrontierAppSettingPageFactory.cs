using Avalonia.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Views.FrontierPages.OneDragon;
using ZzzOd.Gui.Views.FrontierPages.WorldPatrol;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed class FrontierAppSettingPageFactory
{
    private readonly IZzzAppBackend _backend;

    public FrontierAppSettingPageFactory(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public Control? Create(string targetKey, int instanceIndex, string groupId) => targetKey switch
    {
        "world-patrol-settings" when _backend is IZzzWorldPatrolSettingsBackend worldPatrolBackend =>
            new FrontierWorldPatrolPage(_backend, worldPatrolBackend, instanceIndex, groupId),
        "withered-domain-settings" => new FrontierWitheredDomainAppSettingPage(_backend, instanceIndex, groupId),
        "one-dragon-charge-plan" => new FrontierChargePlanPage(_backend),
        "drive-disc-dismantle-flyout" => new FrontierDriveDiscDismantleSettingsFlyoutContent(_backend, instanceIndex, groupId),
        "redemption-code-settings" when _backend is IZzzRedemptionCodeBackend redemptionCodeBackend =>
            new FrontierRedemptionCodeAppSettingPage(redemptionCodeBackend),
        "lost-void-settings" when _backend is IZzzLostVoidSettingsBackend lostVoidBackend =>
            new FrontierLostVoidAppSettingPage(_backend, lostVoidBackend, instanceIndex, groupId),
        "suibian-temple-settings" => new FrontierSuibianTempleAppSettingPage(_backend, instanceIndex, groupId),
        "coffee-settings" => new FrontierCoffeeAppSettingPage(_backend, instanceIndex, groupId),
        "notorious-hunt-settings" => new FrontierNotoriousHuntAppSettingPage(_backend, instanceIndex, groupId),
        "random-play-flyout" => new FrontierRandomPlaySettingsFlyoutContent(_backend, instanceIndex, groupId),
        "life-on-line-flyout" => new FrontierLifeOnLineSettingsFlyoutContent(_backend, instanceIndex, groupId),
        "intel-board-flyout" when _backend is IZzzIntelBoardProgressBackend progressBackend =>
            new FrontierIntelBoardSettingsFlyoutContent(_backend, progressBackend, instanceIndex, groupId),
        "shiyu-defense-settings" => new FrontierShiyuDefenseAppSettingPage(_backend, instanceIndex, groupId),
        _ => null,
    };
}
