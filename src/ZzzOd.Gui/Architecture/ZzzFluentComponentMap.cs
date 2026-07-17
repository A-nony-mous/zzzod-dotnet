using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Architecture;

public enum ZzzFluentComponentRole
{
    SettingsGroup,

    SettingsItem,

    ComboBox,

    NumberInput,

    CommandBar,

    Dialog,

    TeachingTip,

    InfoBar,

    Tab,

    Frame,

    Navigation,

    SymbolIcon,

    FontIcon,
}

public static class ZzzFluentComponentMap
{
    private static readonly IReadOnlyDictionary<ZzzFluentComponentRole, Type> ComponentTypes =
        new Dictionary<ZzzFluentComponentRole, Type>
        {
            [ZzzFluentComponentRole.SettingsGroup] = typeof(SettingsExpander),
            [ZzzFluentComponentRole.SettingsItem] = typeof(SettingsExpanderItem),
            [ZzzFluentComponentRole.ComboBox] = typeof(FAComboBox),
            [ZzzFluentComponentRole.NumberInput] = typeof(NumberBox),
            [ZzzFluentComponentRole.CommandBar] = typeof(CommandBar),
            [ZzzFluentComponentRole.Dialog] = typeof(ContentDialog),
            [ZzzFluentComponentRole.TeachingTip] = typeof(TeachingTip),
            [ZzzFluentComponentRole.InfoBar] = typeof(InfoBar),
            [ZzzFluentComponentRole.Tab] = typeof(TabView),
            [ZzzFluentComponentRole.Frame] = typeof(Frame),
            [ZzzFluentComponentRole.Navigation] = typeof(NavigationView),
            [ZzzFluentComponentRole.SymbolIcon] = typeof(SymbolIcon),
            [ZzzFluentComponentRole.FontIcon] = typeof(FontIcon),
        };

    public static IReadOnlyDictionary<ZzzFluentComponentRole, Type> All => ComponentTypes;

    public static Type GetRequired(ZzzFluentComponentRole role) => ComponentTypes[role];
}

