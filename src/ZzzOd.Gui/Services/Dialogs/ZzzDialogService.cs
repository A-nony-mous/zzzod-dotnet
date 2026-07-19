using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace ZzzOd.Gui.Services.Dialogs;

public interface IZzzDialogService
{
    event EventHandler<ZzzToastRequest>? ToastRequested;

    Task ShowMessageAsync(Window owner, string title, string message);

    FAContentDialog CreateMessageDialog(string title, string message);

    FATeachingTip CreateTeachingTip(string title, string subtitle, Control? target = null);

    void ShowToast(string title, string message);
}

public sealed record ZzzToastRequest(string Title, string Message);

public sealed class ZzzDialogService : IZzzDialogService
{
    public event EventHandler<ZzzToastRequest>? ToastRequested;

    public async Task ShowMessageAsync(Window owner, string title, string message)
    {
        FAContentDialog dialog = CreateMessageDialog(title, message);
        await dialog.ShowAsync(owner).ConfigureAwait(true);
    }

    public FAContentDialog CreateMessageDialog(string title, string message) => new()
    {
        Title = title,
        Content = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        },
        CloseButtonText = "确定",
        DefaultButton = FAContentDialogButton.Close,
    };

    public FATeachingTip CreateTeachingTip(string title, string subtitle, Control? target = null) => new()
    {
        Title = title,
        Subtitle = subtitle,
        Target = target,
        CloseButtonContent = "知道了",
        IsOpen = true,
        PreferredPlacement = FATeachingTipPlacementMode.Auto,
    };

    public void ShowToast(string title, string message)
    {
        ToastRequested?.Invoke(this, new ZzzToastRequest(title, message));
    }
}
