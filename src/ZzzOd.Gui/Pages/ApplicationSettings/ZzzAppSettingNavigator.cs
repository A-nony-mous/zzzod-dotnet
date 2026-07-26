using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed class ZzzAppSettingNavigator
{
    private readonly IZzzAppBackend _backend;
    private readonly Func<string, int, string, Control?> _targetFactory;

    public ZzzAppSettingNavigator(
        IZzzAppBackend backend,
        Func<string, int, string, Control?> targetFactory)
    {
        _backend = backend;
        _targetFactory = targetFactory;
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

        Control? content = _targetFactory(provider.ImplementedTarget!, current.Value.Index, groupId);
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
}

