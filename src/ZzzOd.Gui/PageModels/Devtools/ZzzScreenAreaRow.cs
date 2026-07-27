namespace ZzzOd.Gui.PageModels.Devtools;

internal sealed class ZzzScreenAreaRow
{
    public string AreaName { get; set; } = string.Empty;
    public bool IdMark { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Text { get; set; } = string.Empty;
    public double LcsPercent { get; set; } = 0.5d;
    public string TemplateSubDir { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public double TemplateMatchThreshold { get; set; } = 0.7d;
    public string ColorRangeText { get; set; } = string.Empty;
    public string GotoListText { get; set; } = string.Empty;
    public string? GamepadKey { get; set; }
}
