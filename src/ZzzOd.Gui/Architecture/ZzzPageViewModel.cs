using CommunityToolkit.Mvvm.ComponentModel;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Architecture;

public abstract class ZzzPageViewModel : ObservableObject, IZzzPageLifecycle
{
    private bool _disposed;

    public virtual void OnPageLeave()
    {
    }

    public virtual void OnPageShown()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public virtual void OnPageHidden()
    {
    }

    public void DisposePage()
    {
        if (_disposed)
        {
            return;
        }

        DisposePageCore();
        _disposed = true;
    }

    protected virtual void DisposePageCore()
    {
    }

}
