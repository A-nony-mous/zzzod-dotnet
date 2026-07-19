using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ZzzOd.Gui.Pages.Devtools;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed record ZzzWorldPatrolImageViewerOption(string Label, string Value)
{
    public override string ToString() => Label;
}

internal sealed class ZzzWorldPatrolImagePointEventArgs(
    int x,
    int y,
    double displayX,
    double displayY,
    double displayWidth,
    double displayHeight) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public double DisplayX { get; } = displayX;
    public double DisplayY { get; } = displayY;
    public double DisplayWidth { get; } = displayWidth;
    public double DisplayHeight { get; } = displayHeight;
}

internal sealed class ZzzWorldPatrolImageAreaEventArgs(int x, int y, int width, int height) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Width { get; } = width;
    public int Height { get; } = height;
}

internal sealed partial class ZzzWorldPatrolImageViewer : UserControl
{
    private readonly FAComboBox _modeCombo;
    private readonly FANumberBox _scaleBox;
    private readonly TextBlock _selectionText;
    private readonly FACommandBarButton _clearSelectionButton;
    private readonly ScrollViewer _scrollViewer;
    private readonly Canvas _canvas;
    private readonly Image _image;
    private readonly Rectangle _selectionRectangle;
    private PixelSize? _sourceSize;
    private Point? _selectionStart;
    private Point? _selectionEnd;
    private double _scaleFactor = 1;
    private bool _selecting;
    private bool _updatingScale;

    public ZzzWorldPatrolImageViewer()
    {
        AvaloniaXamlLoader.Load(this);
        _modeCombo = Required<FAComboBox>("ModeCombo");
        _scaleBox = Required<FANumberBox>("ScaleBox");
        _selectionText = Required<TextBlock>("SelectionText");
        _clearSelectionButton = Required<FACommandBarButton>("ClearSelectionButton");
        _scrollViewer = Required<ScrollViewer>("ImageScrollViewer");
        _canvas = Required<Canvas>("ImageCanvas");
        _image = Required<Image>("DisplayImage");
        _selectionRectangle = Required<Rectangle>("SelectionRectangle");
        _modeCombo.ItemsSource = new[]
        {
            new ZzzWorldPatrolImageViewerOption("点击模式", "click"),
            new ZzzWorldPatrolImageViewerOption("框选模式", "select"),
        };
        _modeCombo.SelectedIndex = 0;
    }

    public event EventHandler<ZzzWorldPatrolImagePointEventArgs>? PointClicked;

    public event EventHandler<ZzzWorldPatrolImageAreaEventArgs>? AreaSelected;

    public void SetImage(byte[]? bytes)
    {
        Vector previousOffset = _scrollViewer.Offset;
        PixelSize? previousSize = _sourceSize;
        double previousScale = _scaleFactor;
        if (bytes is null || bytes.Length == 0 || !ZzzDevtoolsImageLoader.TryLoadBitmap(_image, bytes))
        {
            _sourceSize = null;
            _canvas.Width = 0;
            _canvas.Height = 0;
            ClearSelection();
            return;
        }

        Bitmap bitmap = (Bitmap)_image.Source!;
        _sourceSize = bitmap.PixelSize;
        if (previousSize == _sourceSize)
        {
            SetScaleFactor(previousScale, updateNumberBox: true);
            Dispatcher.UIThread.Post(() => _scrollViewer.Offset = previousOffset, DispatcherPriority.Loaded);
        }
        else
        {
            ClearSelection();
            Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
        }
    }

    private bool IsSelectMode => _modeCombo.SelectedItem is ZzzWorldPatrolImageViewerOption { Value: "select" };

    private void OnModeChanged(object? sender, SelectionChangedEventArgs args)
    {
        _selectionText.IsVisible = IsSelectMode;
        _clearSelectionButton.IsVisible = IsSelectMode;
        if (!IsSelectMode)
        {
            ClearSelection();
        }
    }

    private void OnScaleChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args)
    {
        if (!_updatingScale)
        {
            SetScaleFactor(sender.Value / 100, updateNumberBox: false);
        }
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs args) =>
        _scaleBox.Value = Math.Max(10, _scaleBox.Value - 10);

    private void OnZoomInClicked(object? sender, RoutedEventArgs args) =>
        _scaleBox.Value = Math.Min(1000, _scaleBox.Value + 10);

    private void OnFitClicked(object? sender, RoutedEventArgs args) => FitToWindow();

    private void OnOriginalClicked(object? sender, RoutedEventArgs args) => SetScaleFactor(1, updateNumberBox: true);

    private void OnClearSelectionClicked(object? sender, RoutedEventArgs args) => ClearSelection();

    private void FitToWindow()
    {
        if (_sourceSize is not { } size || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        double availableWidth = _scrollViewer.Viewport.Width > 0 ? _scrollViewer.Viewport.Width : _scrollViewer.Bounds.Width;
        double availableHeight = _scrollViewer.Viewport.Height > 0 ? _scrollViewer.Viewport.Height : _scrollViewer.Bounds.Height;
        double scale = Math.Min(Math.Min(availableWidth / size.Width, availableHeight / size.Height), 1);
        SetScaleFactor(scale > 0 ? scale : 1, updateNumberBox: true);
    }

    private void SetScaleFactor(double factor, bool updateNumberBox)
    {
        if (_sourceSize is not { } size)
        {
            return;
        }

        _scaleFactor = Math.Clamp(factor, 0.1, 10);
        double width = Math.Max(1, Math.Round(size.Width * _scaleFactor));
        double height = Math.Max(1, Math.Round(size.Height * _scaleFactor));
        _canvas.Width = width;
        _canvas.Height = height;
        _image.Width = width;
        _image.Height = height;
        UpdateSelectionRectangle();
        if (updateNumberBox)
        {
            _updatingScale = true;
            _scaleBox.Value = Math.Clamp(Math.Round(_scaleFactor * 100), 10, 1000);
            _updatingScale = false;
        }
    }

    private void OnImagePointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_image).Properties.IsLeftButtonPressed
            || !TryMapToOriginal(args.GetPosition(_image), out Point original))
        {
            return;
        }

        Point display = args.GetPosition(_image);
        PointClicked?.Invoke(this, new ZzzWorldPatrolImagePointEventArgs(
            (int)original.X,
            (int)original.Y,
            display.X,
            display.Y,
            _image.Bounds.Width,
            _image.Bounds.Height));
        if (IsSelectMode)
        {
            _selectionStart = original;
            _selectionEnd = original;
            _selecting = true;
            args.Pointer.Capture(_image);
            UpdateSelectionRectangle();
        }
    }

    private void OnImagePointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_selecting || !IsSelectMode || !TryMapToOriginal(args.GetPosition(_image), out Point original))
        {
            return;
        }

        _selectionEnd = original;
        UpdateSelectionRectangle();
    }

    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (!_selecting || !IsSelectMode)
        {
            return;
        }

        if (TryMapToOriginal(args.GetPosition(_image), out Point original))
        {
            _selectionEnd = original;
        }

        _selecting = false;
        args.Pointer.Capture(null);
        if (_selectionStart is not { } start || _selectionEnd is not { } end)
        {
            ClearSelection();
            return;
        }

        int left = (int)Math.Min(start.X, end.X);
        int top = (int)Math.Min(start.Y, end.Y);
        int width = (int)Math.Abs(end.X - start.X);
        int height = (int)Math.Abs(end.Y - start.Y);
        int minimumOriginalSize = (int)(5 / _scaleFactor);
        if (width <= minimumOriginalSize || height <= minimumOriginalSize)
        {
            ClearSelection();
            return;
        }

        _selectionText.Text = $"选择区域: ({left}, {top}, {width}, {height})";
        AreaSelected?.Invoke(this, new ZzzWorldPatrolImageAreaEventArgs(left, top, width, height));
        UpdateSelectionRectangle();
    }

    private bool TryMapToOriginal(Point display, out Point original)
    {
        if (_sourceSize is not { } size
            || display.X < 0
            || display.Y < 0
            || display.X >= _image.Bounds.Width
            || display.Y >= _image.Bounds.Height)
        {
            original = default;
            return false;
        }

        original = new Point(
            Math.Clamp((int)(display.X / _scaleFactor), 0, size.Width - 1),
            Math.Clamp((int)(display.Y / _scaleFactor), 0, size.Height - 1));
        return true;
    }

    private void UpdateSelectionRectangle()
    {
        if (!IsSelectMode || _selectionStart is not { } start || _selectionEnd is not { } end)
        {
            _selectionRectangle.IsVisible = false;
            return;
        }

        double left = Math.Min(start.X, end.X) * _scaleFactor;
        double top = Math.Min(start.Y, end.Y) * _scaleFactor;
        _selectionRectangle.Width = Math.Abs(end.X - start.X) * _scaleFactor;
        _selectionRectangle.Height = Math.Abs(end.Y - start.Y) * _scaleFactor;
        Canvas.SetLeft(_selectionRectangle, left);
        Canvas.SetTop(_selectionRectangle, top);
        _selectionRectangle.IsVisible = true;
    }

    private void ClearSelection()
    {
        _selectionStart = null;
        _selectionEnd = null;
        _selecting = false;
        _selectionRectangle.IsVisible = false;
        _selectionText.Text = "选择区域: 未选择";
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"图片查看器缺少 {name}。");
}
