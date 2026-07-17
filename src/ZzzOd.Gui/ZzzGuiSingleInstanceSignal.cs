using System.IO.Pipes;
using System.Text;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui;

internal sealed class ZzzGuiSingleInstanceSignal : IDisposable
{
    private const string ShowMessage = "show";
    private readonly string _pipeName;
    private readonly Action _showWindow;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;

    private ZzzGuiSingleInstanceSignal(string runRoot, Action showWindow)
    {
        _pipeName = GetPipeName(runRoot);
        _showWindow = showWindow;
        _serverTask = Task.Run(RunServerAsync);
    }

    public static ZzzGuiSingleInstanceSignal Start(string runRoot, Action showWindow) =>
        new(runRoot, showWindow);

    public static async Task<bool> TryShowExistingAsync(string runRoot)
    {
        try
        {
            await using NamedPipeClientStream client = new(".", GetPipeName(runRoot), PipeDirection.Out, PipeOptions.Asynchronous);
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(cts.Token).ConfigureAwait(false);
            byte[] bytes = Encoding.UTF8.GetBytes(ShowMessage);
            await client.WriteAsync(bytes, cts.Token).ConfigureAwait(false);
            await client.FlushAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }

    private async Task RunServerAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await using NamedPipeServerStream server = new(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                byte[] buffer = new byte[16];
                int read = await server.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                string message = Encoding.UTF8.GetString(buffer, 0, read);
                if (string.Equals(message, ShowMessage, StringComparison.Ordinal))
                {
                    _showWindow();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
            }
        }
    }

    private static string GetPipeName(string runRoot) => $"ZzzOd.Gui.{ZzzRuntimeLock.GetRunRootKey(runRoot)}";
}
