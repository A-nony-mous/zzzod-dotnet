namespace ZzzOd.Gui.Services.Tasks;

public sealed record ZzzPageTaskState(bool Running, string Status, string? Error = null);

public sealed class ZzzPageTaskRunner : IDisposable
{
    private CancellationTokenSource? _cts;

    public event EventHandler<ZzzPageTaskState>? StateChanged;

    public ZzzPageTaskState State { get; private set; } = new(false, "Idle");

    public void Run(string name, Func<CancellationToken, Task> work)
    {
        Cancel();
        _cts = new CancellationTokenSource();
        SetState(new ZzzPageTaskState(true, name));
        CancellationToken token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await work(token).ConfigureAwait(false);
                SetState(new ZzzPageTaskState(false, "Finished"));
            }
            catch (OperationCanceledException)
            {
                SetState(new ZzzPageTaskState(false, "Cancelled"));
            }
            catch (Exception exception)
            {
                SetState(new ZzzPageTaskState(false, "Error", exception.Message));
            }
        }, CancellationToken.None);
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Cancel();
    }

    private void SetState(ZzzPageTaskState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
