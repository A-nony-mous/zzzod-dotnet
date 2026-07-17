using ZzzOd.GameLogic.Operations.EnterGame;

namespace ZzzOd.Gui.Services.Windows;

internal interface IZzzManualAutoHdrService
{
    bool SetEnabled(string gamePath, bool enabled);
}

internal sealed class ZzzWindowsManualAutoHdrService : IZzzManualAutoHdrService
{
    internal const string EnabledValue = "AutoHDREnable=2097;";
    private readonly IAutoHdrPreferenceStore _store;

    public ZzzWindowsManualAutoHdrService(IAutoHdrPreferenceStore? store = null)
    {
        _store = store ?? new WindowsAutoHdrPreferenceStore();
    }

    public bool SetEnabled(string gamePath, bool enabled)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(gamePath))
        {
            return false;
        }

        try
        {
            _store.WriteValue(gamePath, enabled ? EnabledValue : AutoHdrManager.DisabledValue);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
