using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Architecture;

public abstract class ZzzPageViewModel : INotifyPropertyChanged, IZzzPageLifecycle
{
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
