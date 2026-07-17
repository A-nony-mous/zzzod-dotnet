using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ZzzOd.Gui.Services.Windows;

internal interface IZzzImageClipboardService
{
    Task CopyPngAsync(byte[] pngBytes, CancellationToken cancellationToken);
}

internal sealed class ZzzImageClipboardService : IZzzImageClipboardService
{
    public Task CopyPngAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime
                    || lifetime.MainWindow?.Clipboard is not { } clipboard)
                {
                    throw new InvalidOperationException("当前窗口剪贴板不可用。");
                }

                using MemoryStream stream = new(pngBytes, writable: false);
                using Bitmap bitmap = new(stream);
                await clipboard.SetBitmapAsync(bitmap).ConfigureAwait(true);
                completion.SetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.SetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }
}
