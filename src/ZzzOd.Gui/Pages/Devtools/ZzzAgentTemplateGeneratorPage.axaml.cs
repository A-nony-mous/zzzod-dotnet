using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Devtools;

internal sealed record ZzzAgentTemplateCardView(
    int Index,
    string Name,
    Bitmap? Preview,
    bool IsEnabled,
    bool CanSave);

internal sealed partial class ZzzAgentTemplateGeneratorPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzAgentTemplateGeneratorState _state;
    private readonly TextBox _agentIdBox;
    private readonly Button _generateAllButton;
    private readonly ItemsControl _templateCards;
    private readonly FAInfoBar _statusBar;
    private readonly List<Bitmap> _previews = [];

    public ZzzAgentTemplateGeneratorPage(IZzzAppBackend backend)
    {
        _state = new ZzzAgentTemplateGeneratorState(backend);
        AvaloniaXamlLoader.Load(this);
        _agentIdBox = Required<TextBox>("AgentIdBox");
        _generateAllButton = Required<Button>("GenerateAllButton");
        _templateCards = Required<ItemsControl>("TemplateCards");
        _statusBar = Required<FAInfoBar>("StatusBar");
        Refresh();
    }

    public IReadOnlyList<ZzzAgentTemplateCardState> Cards => _state.Cards;

    public string LastStatusText => _state.LastStatusText;

    public string? LastSavedPath => _state.LastSavedPath;

    public void OnPageShown() => Refresh();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
        foreach (Bitmap preview in _previews)
        {
            preview.Dispose();
        }

        _previews.Clear();
    }

    public bool SetAgentIdForTest(string value)
    {
        bool valid = _state.SetAgentId(value);
        Refresh();
        return valid;
    }

    public void ChooseScreenshotForTest(int index, string filePath)
    {
        _state.ChooseScreenshot(index, filePath);
        Refresh();
    }

    public void CaptureGameScreenshotForTest(int index)
    {
        _state.CaptureGameScreenshot(index);
        Refresh();
    }

    public string? SaveTemplateForTest(int index)
    {
        string? result = _state.SaveTemplate(index);
        Refresh();
        return result;
    }

    public int GenerateAllForTest()
    {
        int result = _state.GenerateAll();
        Refresh();
        return result;
    }

    private void OnAgentIdChanged(object? sender, TextChangedEventArgs args)
    {
        _ = args;
        _state.SetAgentId(_agentIdBox.Text ?? string.Empty);
        Refresh();
    }

    private void OnGenerateAllClicked(object? sender, RoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        _state.GenerateAll();
        Refresh();
    }

    private async void OnChooseScreenshotClicked(object? sender, RoutedEventArgs args)
    {
        _ = args;
        if (TryGetIndex(sender, out int index))
        {
            await _state.ChooseScreenshotWithPickerAsync(this, index);
            Refresh();
        }
    }

    private void OnCaptureGameClicked(object? sender, RoutedEventArgs args)
    {
        _ = args;
        if (TryGetIndex(sender, out int index))
        {
            _state.CaptureGameScreenshot(index);
            Refresh();
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs args)
    {
        _ = args;
        if (TryGetIndex(sender, out int index))
        {
            _state.SaveTemplate(index);
            Refresh();
        }
    }

    private void Refresh()
    {
        foreach (Bitmap preview in _previews)
        {
            preview.Dispose();
        }

        _previews.Clear();
        bool enabled = _state.AgentId is not null;
        _generateAllButton.IsEnabled = enabled;
        _templateCards.ItemsSource = _state.Cards.Select((card, index) =>
        {
            Bitmap? preview = card.PreviewBytes is null ? null : LoadBitmap(card.PreviewBytes);
            if (preview is not null)
            {
                _previews.Add(preview);
            }

            return new ZzzAgentTemplateCardView(index, card.Name, preview, enabled, enabled && card.ScreenBytes is not null && !card.Saved);
        }).ToArray();

        string status = _state.LastStatusText;
        _statusBar.IsOpen = !string.IsNullOrWhiteSpace(status);
        _statusBar.Title = status;
        _statusBar.Severity = status == "全部模板已生成" ? FAInfoBarSeverity.Success : FAInfoBarSeverity.Warning;
    }

    private static Bitmap? LoadBitmap(byte[] bytes)
    {
        try
        {
            return new Bitmap(new MemoryStream(bytes, writable: false));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetIndex(object? sender, out int index)
    {
        if (sender is Control { DataContext: ZzzAgentTemplateCardView view })
        {
            index = view.Index;
            return true;
        }

        index = -1;
        return false;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"缺少控件 {name}。");
}

