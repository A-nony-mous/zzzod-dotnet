using Avalonia.Threading;

namespace ZzzOd.Gui.Shell;

public interface IZzzUiDispatcher
{
    void Post(Action action);
}

public sealed class ZzzAvaloniaUiDispatcher : IZzzUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
