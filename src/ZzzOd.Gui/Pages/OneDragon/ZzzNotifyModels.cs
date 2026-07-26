using ZzzOd.GameLogic.Config;

namespace ZzzOd.Gui.Pages.OneDragon;

internal sealed record ZzzNotifyModeOption(string Label, string Value);

internal sealed class ZzzNotifyAppRowModel
{
    public required string AppId { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<ZzzNotifyModeOption> LifecycleOptions { get; init; }

    public required IReadOnlyList<ZzzNotifyModeOption> DetailOptions { get; init; }

    public ZzzNotifyModeOption? SelectedLifecycle { get; set; }

    public ZzzNotifyModeOption? SelectedDetail { get; set; }
}

internal static class ZzzNotifySettingsReader
{
    internal static Dictionary<string, NotifyApplicationSetting> ReadApplications(IReadOnlyDictionary<string, object?> values)
    {
        if (values.TryGetValue("applications", out object? value)
            && value is Dictionary<string, NotifyApplicationSetting> applications)
        {
            return applications.ToDictionary(
                pair => pair.Key,
                pair => new NotifyApplicationSetting
                {
                    Lifecycle = pair.Value.Lifecycle,
                    Detail = pair.Value.Detail,
                },
                StringComparer.Ordinal);
        }

        return new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);
    }

}
