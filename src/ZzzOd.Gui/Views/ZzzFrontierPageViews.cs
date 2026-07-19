using Avalonia.Controls;

namespace ZzzOd.Gui.Views;

internal sealed class FrontierHomePage : FrontierPageHost
{
    public FrontierHomePage(string title, Control content)
        : base("home", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}

internal sealed class FrontierGameAssistantPage : FrontierPageHost
{
    public FrontierGameAssistantPage(string title, Control content)
        : base("game-assistant", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}

internal sealed class FrontierOneDragonPage : FrontierPageHost
{
    public FrontierOneDragonPage(string title, Control content)
        : base("one-dragon", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}

internal sealed class FrontierStandalonePage : FrontierPageHost
{
    public FrontierStandalonePage(string title, Control content)
        : base("standalone", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}

internal sealed class FrontierDevtoolsPage : FrontierPageHost
{
    public FrontierDevtoolsPage(string title, Control content)
        : base("devtools", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}

internal sealed class FrontierAccountsPage : FrontierPageHost
{
    public FrontierAccountsPage(string title, Control content)
        : base("accounts", title, content, ZzzFrontierPageLayout.Standard)
    {
    }
}

internal sealed class FrontierSettingsPage : FrontierPageHost
{
    public FrontierSettingsPage(string title, Control content)
        : base("settings", title, content, ZzzFrontierPageLayout.Standard)
    {
    }
}

internal sealed class FrontierDiagnosticsPage : FrontierPageHost
{
    public FrontierDiagnosticsPage(string title, Control content)
        : base("diagnostics", title, content, ZzzFrontierPageLayout.Surface)
    {
    }
}
