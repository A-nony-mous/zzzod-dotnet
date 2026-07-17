namespace ZzzOd.Gui.Shell;

public interface IZzzShellBackNavigationHost
{
    event EventHandler? BackNavigationStateChanged;

    bool CanGoBack { get; }

    void GoBack();
}
