using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzWorldPatrolOption(string Label, object Value)
{
    public override string ToString() => Label;
}

internal sealed record ZzzWorldPatrolRouteOption(string Label, string FullId)
{
    public override string ToString() => Label;
}

internal sealed record ZzzWorldPatrolRouteEditorOption(string Label, ZzzWorldPatrolRouteDto Route)
{
    public override string ToString() => Label;
}

internal sealed class ZzzWorldPatrolEditableOperation
{
    public required int Index { get; init; }

    public string OpType { get; set; } = "move";

    public string Data1 { get; set; } = "0";

    public string Data2 { get; set; } = "0";
}

internal sealed class ZzzWorldPatrolEditableIcon
{
    public string IconName { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public double LargeMapX { get; set; }

    public double LargeMapY { get; set; }

    public double TeleportX { get; set; }

    public double TeleportY { get; set; }
}
