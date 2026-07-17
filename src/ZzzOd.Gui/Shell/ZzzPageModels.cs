using Avalonia.Controls;

namespace ZzzOd.Gui.Shell;

public enum ZzzPageStatusSeverity
{
    Info,

    Success,

    Warning,

    Error,
}

public sealed record ZzzPageStatusModel(ZzzPageStatusSeverity Severity, string Title, string Message);

public sealed record ZzzPageControlModel(
    string Key,
    string Label,
    object? Value,
    bool Visible = true,
    bool Enabled = true,
    string? ValidationMessage = null);

public sealed class ZzzPageModel
{
    private readonly List<ZzzPageControlModel> _controls = [];
    private readonly List<ZzzPageStatusModel> _statuses = [];

    public ZzzPageModel(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }

    public string Title { get; }

    public IReadOnlyList<ZzzPageControlModel> Controls => _controls;

    public IReadOnlyList<ZzzPageStatusModel> Statuses => _statuses;

    public ZzzPageModel AddControl(ZzzPageControlModel control)
    {
        _controls.Add(control);
        return this;
    }

    public ZzzPageModel AddStatus(ZzzPageStatusModel status)
    {
        _statuses.Add(status);
        return this;
    }
}

public sealed class ZzzUnavailablePageModel
{
    public ZzzUnavailablePageModel(string title, string reason, string? missingService = null)
    {
        Title = title;
        Reason = reason;
        MissingService = missingService;
    }

    public string Title { get; }

    public string Reason { get; }

    public string? MissingService { get; }

    public ZzzPageStatusModel ToStatus() =>
        new(ZzzPageStatusSeverity.Warning, Title, string.IsNullOrWhiteSpace(MissingService) ? Reason : $"{Reason}：{MissingService}");

    public Control ToControl() => new ZzzOd.Gui.Controls.ZzzInfoBar(Title, ToStatus().Message, ZzzOd.Gui.Controls.ZzzInfoBarSeverity.Warning);
}

public sealed record ZzzGuiEvidenceSelection(
    string Page,
    string? Tab,
    string Theme,
    string Pane,
    double? Width,
    double? Height,
    bool DevToolsEnabled)
{
    public static ZzzGuiEvidenceSelection FromEnvironment()
    {
        (double? width, double? height) = ParseSize(Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_SIZE"));
        string page = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE") ?? "home";
        string? tab = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB");
        string theme = Environment.GetEnvironmentVariable("ZZZOD_GUI_THEME") ?? "Light";
        string pane = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PANE") ?? "expanded";
        bool devTools = string.Equals(Environment.GetEnvironmentVariable("ZZZOD_GUI_DEV_MODE"), "1", StringComparison.Ordinal);
        return new ZzzGuiEvidenceSelection(
            string.IsNullOrWhiteSpace(page) ? "home" : page,
            string.IsNullOrWhiteSpace(tab) ? null : tab,
            string.IsNullOrWhiteSpace(theme) ? "Light" : theme,
            string.IsNullOrWhiteSpace(pane) ? "expanded" : pane,
            width,
            height,
            devTools);
    }

    private static (double? Width, double? Height) ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        string[] parts = value.Split(['x', 'X'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], out double width)
            || !double.TryParse(parts[1], out double height))
        {
            return (null, null);
        }

        return (width, height);
    }
}
