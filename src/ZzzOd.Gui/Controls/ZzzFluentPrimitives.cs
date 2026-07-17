using Avalonia.Controls;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Controls;

public sealed class ZzzStatusPill : InfoBar
{
    public ZzzStatusPill(string text)
    {
        Message = text;
        IsOpen = true;
        IsClosable = false;
        IsIconVisible = false;
    }

    public string Text
    {
        get => Message ?? string.Empty;
        set => Message = value;
    }
}

public enum ZzzInfoBarSeverity
{
    Informational,

    Success,

    Warning,

    Error,
}

public sealed class ZzzCommandBar : CommandBar
{
    public ZzzCommandBar(params Control[] commands)
        : this(commands, [])
    {
    }

    public ZzzCommandBar(IEnumerable<Control> primaryCommands, IEnumerable<Control> secondaryCommands)
    {
        Classes.Add("zzz-command-bar");
        DefaultLabelPosition = CommandBarDefaultLabelPosition.Right;
        HorizontalAlignment = HorizontalAlignment.Left;
        foreach (Control command in primaryCommands)
        {
            PrimaryCommands.Add(new CommandBarElementContainer { Content = command });
        }

        foreach (Control command in secondaryCommands)
        {
            SecondaryCommands.Add(new CommandBarElementContainer { Content = command });
        }
    }
}

public sealed class ZzzInfoBar : InfoBar
{
    public ZzzInfoBar(
        string title,
        string message,
        ZzzInfoBarSeverity severity = ZzzInfoBarSeverity.Informational,
        Control? actionButton = null,
        bool isClosable = false)
    {
        Classes.Add("zzz-info-bar");
        Title = title;
        Message = message;
        Severity = ToFluentSeverity(severity);
        IsOpen = true;
        IsClosable = isClosable;
        ActionButton = actionButton;
    }

    private static InfoBarSeverity ToFluentSeverity(ZzzInfoBarSeverity severity) =>
        severity switch
        {
            ZzzInfoBarSeverity.Success => InfoBarSeverity.Success,
            ZzzInfoBarSeverity.Warning => InfoBarSeverity.Warning,
            ZzzInfoBarSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
}

public sealed class ZzzSettingsGroup : SettingsExpander
{
    public ZzzSettingsGroup(string title, IEnumerable<Control> children, string? description = null)
    {
        Header = title;
        Description = description;
        IsExpanded = true;
        ItemsSource = children.ToArray();
    }
}
