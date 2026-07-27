using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Architecture;

public enum ZzzFluentComponentRole
{
    SettingsGroup,

    SettingsItem,

    ComboBox,

    NumberInput,

    FACommandBar,

    Dialog,

    FATeachingTip,

    FAInfoBar,

    Tab,

    FAFrame,

    Navigation,

    FASymbolIcon,

    FAFontIcon,
}

public static class ZzzFluentComponentMap
{
    private static readonly IReadOnlyDictionary<ZzzFluentComponentRole, Type> ComponentTypes =
        new Dictionary<ZzzFluentComponentRole, Type>
        {
            [ZzzFluentComponentRole.SettingsGroup] = typeof(FASettingsExpander),
            [ZzzFluentComponentRole.SettingsItem] = typeof(FASettingsExpanderItem),
            [ZzzFluentComponentRole.ComboBox] = typeof(FAComboBox),
            [ZzzFluentComponentRole.NumberInput] = typeof(FANumberBox),
            [ZzzFluentComponentRole.FACommandBar] = typeof(FACommandBar),
            [ZzzFluentComponentRole.Dialog] = typeof(FAContentDialog),
            [ZzzFluentComponentRole.FATeachingTip] = typeof(FATeachingTip),
            [ZzzFluentComponentRole.FAInfoBar] = typeof(FAInfoBar),
            [ZzzFluentComponentRole.Tab] = typeof(TabControl),
            [ZzzFluentComponentRole.FAFrame] = typeof(FAFrame),
            [ZzzFluentComponentRole.Navigation] = typeof(FANavigationView),
            [ZzzFluentComponentRole.FASymbolIcon] = typeof(FASymbolIcon),
            [ZzzFluentComponentRole.FAFontIcon] = typeof(FAFontIcon),
        };

    public static IReadOnlyDictionary<ZzzFluentComponentRole, Type> All => ComponentTypes;

    public static Type GetRequired(ZzzFluentComponentRole role) => ComponentTypes[role];
}

