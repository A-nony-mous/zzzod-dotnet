using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.Run;

internal sealed class ZzzRunPanelHotkeyViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField StartHotkeyField = new("key_start_running", typeof(string), string.Empty);
    private static readonly ZzzConfigField StopHotkeyField = new("key_stop_running", typeof(string), string.Empty);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [StartHotkeyField, StopHotkeyField];

    public ZzzRunPanelHotkeyViewModel(IZzzAppBackend backend, Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
    }

    protected override string ScopeName => "env";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public string StartHotkey => Normalize(GetValue<string>(StartHotkeyField));

    public string StopHotkey => Normalize(GetValue<string>(StopHotkeyField));

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.ToUpperInvariant();
}
