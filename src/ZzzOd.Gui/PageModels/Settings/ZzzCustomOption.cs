namespace ZzzOd.Gui.PageModels.Settings;

internal sealed record ZzzCustomOption(string Label, string Value)
{
    public override string ToString() => Label;
}

