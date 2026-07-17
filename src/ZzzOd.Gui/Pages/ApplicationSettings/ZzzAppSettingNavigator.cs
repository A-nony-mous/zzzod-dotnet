using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Pages.OneDragon;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed class ZzzAppSettingNavigator
{
    private readonly IZzzAppBackend _backend;

    public ZzzAppSettingNavigator(IZzzAppBackend backend)
    {
        _backend = backend;
    }

    public bool Open(string appId, string groupId, Button target, Action<Control> pushSecondary)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pushSecondary);

        if (!ZzzAppSettingProviderRegistry.TryGetImplemented(appId, out ZzzAppSettingProviderDescriptor provider))
        {
            return false;
        }

        ZzzBackendResult<ZzzInstanceDto> current = _backend.GetCurrentInstance();
        if (!current.Success || current.Value is null)
        {
            return false;
        }

        Control? content = CreateTarget(provider.ImplementedTarget!, current.Value.Index, groupId);
        if (content is null)
        {
            return false;
        }

        if (provider.SettingType == ZzzAppSettingType.Interface)
        {
            pushSecondary(content);
            return true;
        }

        Flyout flyout = new() { Content = content };
        FlyoutBase.SetAttachedFlyout(target, flyout);
        FlyoutBase.ShowAttachedFlyout(target);
        return true;
    }

    private Control? CreateTarget(string targetKey, int instanceIndex, string groupId)
    {
        _ = instanceIndex;
        _ = groupId;
        return targetKey switch
        {
            "world-patrol-settings" when _backend is IZzzWorldPatrolSettingsBackend worldPatrolBackend =>
                new ZzzWorldPatrolAppSettingPage(_backend, worldPatrolBackend, instanceIndex, groupId),
            "withered-domain-settings" => new ZzzWitheredDomainAppSettingPage(_backend, instanceIndex, groupId),
            "one-dragon-charge-plan" => new ZzzChargePlanPage(_backend),
            "drive-disc-dismantle-flyout" => new ZzzDriveDiscDismantleSettingsFlyoutContent(_backend, instanceIndex, groupId),
            "redemption-code-settings" when _backend is IZzzRedemptionCodeBackend redemptionCodeBackend =>
                new ZzzRedemptionCodeAppSettingPage(redemptionCodeBackend),
            "lost-void-settings" when _backend is IZzzLostVoidSettingsBackend lostVoidBackend =>
                new ZzzLostVoidAppSettingPage(_backend, lostVoidBackend, instanceIndex, groupId),
            "suibian-temple-settings" => new ZzzSuibianTempleAppSettingPage(_backend, instanceIndex, groupId),
            "coffee-settings" => new ZzzCoffeeAppSettingPage(_backend, instanceIndex, groupId),
            "notorious-hunt-settings" => new ZzzNotoriousHuntAppSettingPage(_backend, instanceIndex, groupId),
            "random-play-flyout" => new ZzzRandomPlaySettingsFlyoutContent(_backend, instanceIndex, groupId),
            "life-on-line-flyout" => new ZzzLifeOnLineSettingsFlyoutContent(_backend, instanceIndex, groupId),
            "intel-board-flyout" when _backend is IZzzIntelBoardProgressBackend progressBackend =>
                new ZzzIntelBoardSettingsFlyoutContent(_backend, progressBackend, instanceIndex, groupId),
            "shiyu-defense-settings" => new ZzzShiyuDefenseAppSettingPage(_backend, instanceIndex, groupId),
            _ => null,
        };
    }
}

