using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using FluentAvalonia.UI.Controls;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;
using AvaloniaPoint = Avalonia.Point;
using GeometryPoint = OneDragon.Core.Abstractions.Geometry.Point;

namespace ZzzOd.Gui.Pages.Devtools;

internal sealed record TemplatePointRow(string Text);

internal sealed partial class ZzzTemplateHelperAxamlPage : UserControl, IZzzPageLifecycle
{
    private static readonly TemplateShapeOption[] ShapeOptions =
    [
        new("矩形", "rectangle"),
        new("圆形", "circle"),
        new("四边形", "quadrilateral"),
        new("多边形", "polygon"),
        new("多个矩形", "multi_rect"),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly ZzzTemplateHelperService? _service;
    private readonly ObservableCollection<TemplatePointRow> _pointRows = [];
    private readonly List<PointHistoryEntry> _history = [];
    private readonly List<Bitmap> _previewBitmaps = [];
    private readonly InfoBar _statusBar;
    private readonly FAComboBox _existingTemplateCombo;
    private readonly FAComboBox _shapeCombo;
    private readonly TextBox _subDirBox;
    private readonly TextBox _templateIdBox;
    private readonly TextBox _templateNameBox;
    private readonly TextBox _horizontalMoveBox;
    private readonly TextBox _verticalMoveBox;
    private readonly ToggleSwitch _autoMaskToggle;
    private readonly TextBox _xPositionBox;
    private readonly TextBox _yPositionBox;
    private readonly ItemsControl _pointList;
    private readonly Image _screenImage;
    private readonly Image _rawPreview;
    private readonly Image _maskPreview;
    private readonly Image _mergePreview;
    private readonly Image _reversePreview;
    private readonly TeachingTip _shapeTeachingTip;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Control[] _editingControls;
    private TemplateInfo? _chosenTemplate;
    private PixelPoint? _pointerStart;
    private int _historyIndex = -1;
    private bool _loading;

    public ZzzTemplateHelperAxamlPage(IZzzAppBackend backend)
    {
        _backend = backend;
        AvaloniaXamlLoader.Load(this);
        _statusBar = Required<InfoBar>("StatusBar");
        _existingTemplateCombo = Required<FAComboBox>("ExistingTemplateCombo");
        _shapeCombo = Required<FAComboBox>("ShapeCombo");
        _subDirBox = Required<TextBox>("TemplateSubDirBox");
        _templateIdBox = Required<TextBox>("TemplateIdBox");
        _templateNameBox = Required<TextBox>("TemplateNameBox");
        _horizontalMoveBox = Required<TextBox>("HorizontalMoveBox");
        _verticalMoveBox = Required<TextBox>("VerticalMoveBox");
        _autoMaskToggle = Required<ToggleSwitch>("AutoMaskToggle");
        _xPositionBox = Required<TextBox>("XPositionBox");
        _yPositionBox = Required<TextBox>("YPositionBox");
        _pointList = Required<ItemsControl>("PointList");
        _screenImage = Required<Image>("ScreenImage");
        _rawPreview = Required<Image>("RawPreview");
        _maskPreview = Required<Image>("MaskPreview");
        _mergePreview = Required<Image>("MergePreview");
        _reversePreview = Required<Image>("ReversePreview");
        _shapeTeachingTip = Required<TeachingTip>("ShapeTeachingTip");
        _undoButton = Required<Button>("UndoButton");
        _redoButton = Required<Button>("RedoButton");
        _editingControls =
        [
            Required<Control>("CopyButton"),
            Required<Control>("DeleteButton"),
            Required<Control>("CancelButton"),
            Required<Control>("ChooseImageButton"),
            Required<Control>("SaveConfigButton"),
            Required<Control>("SaveRawButton"),
            Required<Control>("SaveMaskButton"),
            Required<Control>("ClearPointsButton"),
            _subDirBox,
            _templateIdBox,
            _templateNameBox,
            _horizontalMoveBox,
            _verticalMoveBox,
            _shapeCombo,
            _autoMaskToggle,
        ];

        _shapeCombo.ItemsSource = ShapeOptions;
        _pointList.ItemsSource = _pointRows;

        ZzzBackendResult<ZzzHealthDto> health = backend.GetHealth();
        if (health.Success && !string.IsNullOrWhiteSpace(health.Value?.RunRoot))
        {
            _service = new ZzzTemplateHelperService(health.Value.RunRoot);
        }
        else
        {
            ShowStatus(health.Error ?? "无法确定模板资源目录。", InfoBarSeverity.Error);
        }

        ReloadTemplateOptions();
        RefreshWholeDisplay();
    }

    internal IReadOnlyList<(int X, int Y)> Points =>
        _chosenTemplate?.PointList.Select(point => (point.X, point.Y)).ToArray() ?? [];

    internal string? LastSavedPath { get; private set; }

    internal string LastStatusText => _statusBar.Message ?? string.Empty;

    public void OnPageShown()
    {
        ReloadTemplateOptions();
        Focus();
    }

    public void OnPageLeave() => _shapeTeachingTip.IsOpen = false;

    public void OnPageHidden() => _shapeTeachingTip.IsOpen = false;

    public void DisposePage()
    {
        ClearPreviews();
        DisposeTemplate(_chosenTemplate);
        _chosenTemplate = null;
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        if (args.Key == Key.Z && args.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (args.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                Redo();
            }
            else
            {
                Undo();
            }

            args.Handled = true;
            return;
        }

        if (args.Key is Key.Delete or Key.Back)
        {
            ClearPoints();
            args.Handled = true;
            return;
        }

        base.OnKeyDown(args);
    }

    internal void CreateTemplateForTest(string subDir = "", string templateId = "", string name = "") =>
        CreateTemplate(subDir, templateId, name);

    internal void CopyTemplateForTest() => CopyTemplate();

    internal void ChooseImageForTest(string filePath) => LoadScreenImage(filePath);

    internal void CaptureScreenshotForTest() => CaptureScreenshot();

    internal string SaveConfigForTest() => SaveConfig();

    internal string SaveRawForTest() => SaveRaw();

    internal string SaveMaskForTest() => SaveMask();

    internal bool DeleteTemplateForTest() => DeleteTemplate();

    internal void ClearPointsForTest() => ClearPoints();

    internal void AddPointForTest(int x, int y) => ApplyPointChange(() => _chosenTemplate?.AddPoint(new GeometryPoint(x, y)));

    internal void MovePointsForTest(int dx, int dy) => ApplyPointChange(() => _chosenTemplate?.UpdateAllPoints(dx, dy));

    internal void UndoForTest() => Undo();

    internal void RedoForTest() => Redo();

    private void ReloadTemplateOptions()
    {
        if (_service is null)
        {
            _existingTemplateCombo.ItemsSource = Array.Empty<TemplateOption>();
            return;
        }

        try
        {
            _loading = true;
            _existingTemplateCombo.ItemsSource = _service.GetTemplates();
            _existingTemplateCombo.SelectedIndex = -1;
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnExistingTemplateChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _service is null || _existingTemplateCombo.SelectedItem is not TemplateOption selected)
        {
            return;
        }

        ChooseTemplate(_service.Load(selected.SubDir, selected.TemplateId));
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs args) => CreateTemplate("", "", "");

    private void CreateTemplate(string subDir, string templateId, string name)
    {
        if (_service is null || _chosenTemplate is not null)
        {
            return;
        }

        TemplateInfo template = _service.Create(subDir, templateId);
        template.TemplateName = name;
        ChooseTemplate(template);
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs args) => CopyTemplate();

    private void CopyTemplate()
    {
        if (_service is null || _chosenTemplate is null)
        {
            return;
        }

        try
        {
            ChooseTemplate(_service.Copy(_chosenTemplate));
            ReloadTemplateOptions();
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (_chosenTemplate is null)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "删除模板",
            Content = $"确定删除 {_chosenTemplate.SubDir}/{_chosenTemplate.TemplateId}？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
        {
            DeleteTemplate();
        }
    }

    private bool DeleteTemplate()
    {
        if (_service is null || _chosenTemplate is null)
        {
            return false;
        }

        try
        {
            bool deleted = _service.Delete(_chosenTemplate);
            if (deleted)
            {
                CancelEditing();
                ReloadTemplateOptions();
            }

            return deleted;
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
            return false;
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs args) => CancelEditing();

    private void CancelEditing()
    {
        DisposeTemplate(_chosenTemplate);
        _chosenTemplate = null;
        _existingTemplateCombo.SelectedIndex = -1;
        ClearHistory();
        RefreshWholeDisplay();
    }

    private async void OnChooseImageClicked(object? sender, RoutedEventArgs args)
    {
        string? path = await ZzzDevtoolsImageLoader.PickLocalFileAsync(this, "选择图片", "*.png").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            LoadScreenImage(path);
        }
    }

    private void LoadScreenImage(string filePath)
    {
        if (_service is null || _chosenTemplate is null)
        {
            return;
        }

        try
        {
            _service.SetScreenImage(_chosenTemplate, File.ReadAllBytes(filePath));
            ClearHistory();
            RefreshImages();
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OnScreenshotClicked(object? sender, RoutedEventArgs args) => CaptureScreenshot();

    private void CaptureScreenshot()
    {
        if (_service is null)
        {
            return;
        }

        ZzzBackendResult<ZzzScreenshotDto> result = _backend.GetScreenshot();
        if (!result.Success || result.Value is null)
        {
            ShowStatus(result.Error ?? "截图失败。", InfoBarSeverity.Error);
            return;
        }

        if (_chosenTemplate is null)
        {
            CreateTemplate("", "", "");
        }

        try
        {
            _service.SetScreenImage(_chosenTemplate!, result.Value.Bytes);
            ClearHistory();
            RefreshImages();
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OnSaveConfigClicked(object? sender, RoutedEventArgs args) => RunSave(SaveConfig);

    private string SaveConfig()
    {
        EnsureCurrentTemplate();
        _service!.SaveConfig(_chosenTemplate!);
        LastSavedPath = _chosenTemplate!.ConfigPath;
        ReloadTemplateOptions();
        ShowStatus($"已保存 {_chosenTemplate.SubDir}/{_chosenTemplate.TemplateId}/config.yml", InfoBarSeverity.Success);
        return LastSavedPath;
    }

    private void OnSaveRawClicked(object? sender, RoutedEventArgs args) => RunSave(SaveRaw);

    private string SaveRaw()
    {
        EnsureCurrentTemplate();
        _service!.SaveRaw(_chosenTemplate!);
        LastSavedPath = _chosenTemplate!.RawPath;
        ReloadTemplateOptions();
        RefreshImages();
        ShowStatus($"已保存 {_chosenTemplate.SubDir}/{_chosenTemplate.TemplateId}/raw.png", InfoBarSeverity.Success);
        return LastSavedPath;
    }

    private void OnSaveMaskClicked(object? sender, RoutedEventArgs args) => RunSave(SaveMask);

    private string SaveMask()
    {
        EnsureCurrentTemplate();
        _service!.SaveMask(_chosenTemplate!);
        LastSavedPath = _chosenTemplate!.MaskPath;
        ReloadTemplateOptions();
        RefreshImages();
        ShowStatus($"已保存 {_chosenTemplate.SubDir}/{_chosenTemplate.TemplateId}/mask.png", InfoBarSeverity.Success);
        return LastSavedPath;
    }

    private void EnsureCurrentTemplate()
    {
        if (_service is null || _chosenTemplate is null)
        {
            throw new InvalidOperationException("请先选择或创建模板。");
        }

        _service.ValidateIdentity(_chosenTemplate.SubDir, _chosenTemplate.TemplateId);
    }

    private void RunSave(Func<string> save)
    {
        try
        {
            save();
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OnTemplateInfoChanged(object? sender, TextChangedEventArgs args)
    {
        if (_loading || _chosenTemplate is null)
        {
            return;
        }

        _chosenTemplate.SubDir = _subDirBox.Text?.Trim() ?? string.Empty;
        _chosenTemplate.TemplateId = _templateIdBox.Text?.Trim() ?? string.Empty;
        _chosenTemplate.TemplateName = _templateNameBox.Text ?? string.Empty;
    }

    private void OnMoveClicked(object? sender, RoutedEventArgs args)
    {
        int.TryParse(_horizontalMoveBox.Text, out int dx);
        int.TryParse(_verticalMoveBox.Text, out int dy);
        if (dx != 0 || dy != 0)
        {
            ApplyPointChange(() => _chosenTemplate?.UpdateAllPoints(dx, dy));
        }
    }

    private void OnShapeChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _chosenTemplate is null || _shapeCombo.SelectedItem is not TemplateShapeOption option)
        {
            return;
        }

        _chosenTemplate.UpdateTemplateShape(option.Value);
        RefreshPointRows();
        RefreshImages();
    }

    private void OnShapeHelpClicked(object? sender, RoutedEventArgs args)
    {
        if (_chosenTemplate is null)
        {
            ShowStatus("请先选择或创建模板", InfoBarSeverity.Warning);
            return;
        }

        string text = _chosenTemplate.TemplateShape switch
        {
            "rectangle" => "矩形模板：左键拖拽选择矩形区域，或单击两个对角点",
            "circle" => "圆形模板：左键拖拽选择外接矩形，或单击圆心和边界点",
            "quadrilateral" => "四边形模板：左键拖拽选择矩形区域，或依次单击四个顶点",
            "polygon" => "多边形模板：左键单击添加顶点，或拖拽添加矩形顶点",
            "multi_rect" => "多矩形模板：左键拖拽添加矩形区域，或单击添加点位",
            _ => "左键单击添加点位，右键显示颜色信息",
        };
        _shapeTeachingTip.Subtitle = text + "\n\n快捷键：Ctrl+Z撤销，Ctrl+Shift+Z恢复，Del清除";
        _shapeTeachingTip.IsOpen = true;
    }

    private void OnAutoMaskChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && _chosenTemplate is not null && _autoMaskToggle.IsChecked is bool value)
        {
            _chosenTemplate.AutoMask = value;
            RefreshImages();
        }
    }

    private void OnPointDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: TemplatePointRow row })
        {
            int index = _pointRows.IndexOf(row);
            if (index >= 0)
            {
                ApplyPointChange(() => _chosenTemplate?.RemovePointByIndex(index));
            }
        }
    }

    private void OnPointTextLostFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is not TextBox { DataContext: TemplatePointRow row } box ||
            !TryParsePoint(box.Text, out GeometryPoint point))
        {
            RefreshPointRows();
            return;
        }

        int index = _pointRows.IndexOf(row);
        if (_chosenTemplate is null || index < 0 || index >= _chosenTemplate.PointList.Count)
        {
            return;
        }

        ApplyPointChange(() =>
        {
            GeometryPoint[] points = _chosenTemplate.PointList.ToArray();
            points[index] = point;
            _chosenTemplate.SetPointList(points);
        });
    }

    private void OnClearPointsClicked(object? sender, RoutedEventArgs args) => ClearPoints();

    private void ClearPoints()
    {
        if (_chosenTemplate?.PointList.Count > 0)
        {
            ApplyPointChange(() => _chosenTemplate.SetPointList([]));
        }
    }

    private void OnUndoClicked(object? sender, RoutedEventArgs args) => Undo();

    private void OnRedoClicked(object? sender, RoutedEventArgs args) => Redo();

    private void ApplyPointChange(Action change)
    {
        if (_chosenTemplate is null)
        {
            return;
        }

        GeometryPoint[] before = ClonePoints(_chosenTemplate.PointList);
        change();
        GeometryPoint[] after = ClonePoints(_chosenTemplate.PointList);
        if (before.SequenceEqual(after))
        {
            return;
        }

        if (_historyIndex + 1 < _history.Count)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(new PointHistoryEntry(before, after));
        _historyIndex = _history.Count - 1;
        RefreshPointRows();
        RefreshImages();
        RefreshHistoryButtons();
    }

    private void Undo()
    {
        if (_chosenTemplate is null || _historyIndex < 0)
        {
            return;
        }

        _chosenTemplate.SetPointList(_history[_historyIndex].Before);
        _historyIndex--;
        RefreshPointRows();
        RefreshImages();
        RefreshHistoryButtons();
    }

    private void Redo()
    {
        if (_chosenTemplate is null || _historyIndex + 1 >= _history.Count)
        {
            return;
        }

        _historyIndex++;
        _chosenTemplate.SetPointList(_history[_historyIndex].After);
        RefreshPointRows();
        RefreshImages();
        RefreshHistoryButtons();
    }

    private void ClearHistory()
    {
        _history.Clear();
        _historyIndex = -1;
        RefreshHistoryButtons();
    }

    private void RefreshHistoryButtons()
    {
        int undoCount = _historyIndex + 1;
        int redoCount = _history.Count - _historyIndex - 1;
        _undoButton.Content = undoCount == 0 ? "撤回" : $"撤回 ({undoCount})";
        _redoButton.Content = redoCount == 0 ? "恢复" : $"恢复 ({redoCount})";
        _undoButton.IsEnabled = undoCount > 0;
        _redoButton.IsEnabled = redoCount > 0;
    }

    private void OnScreenPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_chosenTemplate?.ScreenImage is null || !TryMapImagePoint(args.GetPosition(_screenImage), out PixelPoint point))
        {
            return;
        }

        PointerPoint current = args.GetCurrentPoint(_screenImage);
        if (current.Properties.IsRightButtonPressed)
        {
            _ = ShowPixelColorAsync(point);
            args.Handled = true;
            return;
        }

        if (current.Properties.IsLeftButtonPressed)
        {
            _pointerStart = point;
            args.Pointer.Capture(_screenImage);
            SetCoordinate(point);
            args.Handled = true;
        }
    }

    private void OnScreenPointerMoved(object? sender, PointerEventArgs args)
    {
        if (TryMapImagePoint(args.GetPosition(_screenImage), out PixelPoint point))
        {
            SetCoordinate(point);
        }
    }

    private void OnScreenPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_pointerStart is not PixelPoint start || !TryMapImagePoint(args.GetPosition(_screenImage), out PixelPoint end))
        {
            _pointerStart = null;
            args.Pointer.Capture(null);
            return;
        }

        _pointerStart = null;
        args.Pointer.Capture(null);
        if (Math.Abs(end.X - start.X) >= 3 || Math.Abs(end.Y - start.Y) >= 3)
        {
            ApplyRectangle(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));
        }
        else
        {
            ApplyPointChange(() => _chosenTemplate?.AddPoint(new GeometryPoint(end.X, end.Y)));
        }

        args.Handled = true;
    }

    private void ApplyRectangle(int left, int top, int right, int bottom)
    {
        if (_chosenTemplate is null)
        {
            return;
        }

        ApplyPointChange(() =>
        {
            GeometryPoint[] points = _chosenTemplate.TemplateShape switch
            {
                "rectangle" => [new(left, top), new(right, bottom)],
                "circle" => CirclePoints(left, top, right, bottom),
                "quadrilateral" => [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
                "polygon" => [.. _chosenTemplate.PointList, new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
                "multi_rect" => [.. _chosenTemplate.PointList, new(left, top), new(right, bottom)],
                _ => _chosenTemplate.PointList.ToArray(),
            };
            _chosenTemplate.SetPointList(points);
        });
    }

    private bool TryMapImagePoint(AvaloniaPoint position, out PixelPoint point)
    {
        point = default;
        Mat? image = _chosenTemplate?.ScreenImage;
        if (image is null || image.Empty() || _screenImage.Bounds.Width <= 0 || _screenImage.Bounds.Height <= 0)
        {
            return false;
        }

        double scale = Math.Min(_screenImage.Bounds.Width / image.Width, _screenImage.Bounds.Height / image.Height);
        double renderedWidth = image.Width * scale;
        double renderedHeight = image.Height * scale;
        double offsetX = (_screenImage.Bounds.Width - renderedWidth) / 2;
        double offsetY = (_screenImage.Bounds.Height - renderedHeight) / 2;
        int x = (int)((position.X - offsetX) / scale);
        int y = (int)((position.Y - offsetY) / scale);
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            return false;
        }

        point = new PixelPoint(x, y);
        return true;
    }

    private async Task ShowPixelColorAsync(PixelPoint point)
    {
        if (_chosenTemplate?.ScreenImage is not Mat image)
        {
            return;
        }

        Vec3b bgr = image.At<Vec3b>(point.Y, point.X);
        using Mat pixel = new(1, 1, MatType.CV_8UC3, new Scalar(bgr.Item0, bgr.Item1, bgr.Item2));
        using Mat hsv = new();
        Cv2.CvtColor(pixel, hsv, ColorConversionCodes.BGR2HSV);
        Vec3b hsvValue = hsv.At<Vec3b>(0, 0);
        ContentDialog dialog = new()
        {
            Title = "像素颜色信息",
            Content = $"点击位置: ({point.X}, {point.Y})\nRGB: ({bgr.Item2}, {bgr.Item1}, {bgr.Item0})\nHSV: ({hsvValue.Item0}, {hsvValue.Item1}, {hsvValue.Item2})",
            CloseButtonText = "确定",
        };
        await dialog.ShowAsync().ConfigureAwait(true);
    }

    private void ChooseTemplate(TemplateInfo template)
    {
        DisposeTemplate(_chosenTemplate);
        _chosenTemplate = template;
        ClearHistory();
        RefreshWholeDisplay();
    }

    private void RefreshWholeDisplay()
    {
        bool chosen = _chosenTemplate is not null;
        _loading = true;
        _existingTemplateCombo.IsEnabled = !chosen && _service is not null;
        Required<Control>("CreateButton").IsEnabled = !chosen && _service is not null;
        foreach (Control control in _editingControls)
        {
            control.IsEnabled = chosen;
        }

        _subDirBox.Text = _chosenTemplate?.SubDir ?? string.Empty;
        _templateIdBox.Text = _chosenTemplate?.TemplateId ?? string.Empty;
        _templateNameBox.Text = _chosenTemplate?.TemplateName ?? string.Empty;
        _autoMaskToggle.IsChecked = _chosenTemplate?.AutoMask ?? true;
        _shapeCombo.SelectedItem = ShapeOptions.FirstOrDefault(option => option.Value == _chosenTemplate?.TemplateShape);
        _xPositionBox.Text = string.Empty;
        _yPositionBox.Text = string.Empty;
        _loading = false;
        RefreshPointRows();
        RefreshImages();
        RefreshHistoryButtons();
    }

    private void RefreshPointRows()
    {
        _pointRows.Clear();
        if (_chosenTemplate is null)
        {
            return;
        }

        foreach (GeometryPoint point in _chosenTemplate.PointList)
        {
            _pointRows.Add(new TemplatePointRow($"{point.X}, {point.Y}"));
        }
    }

    private void RefreshImages()
    {
        ClearPreviews();
        if (_chosenTemplate is null)
        {
            return;
        }

        Mat? screen = _chosenTemplate.GetScreenImageToDisplay();
        Mat? raw = _chosenTemplate.GetTemplateRawToDisplay();
        Mat? mask = _chosenTemplate.GetTemplateMaskToDisplay();
        SetImage(_screenImage, screen, disposeImage: true);
        SetImage(_rawPreview, raw, disposeImage: !ReferenceEquals(raw, _chosenTemplate.Raw));
        SetImage(_maskPreview, mask, disposeImage: !ReferenceEquals(mask, _chosenTemplate.Mask));
        SetImage(_mergePreview, SafePreview(_chosenTemplate.GetTemplateMergeToDisplay), disposeImage: true);
        SetImage(_reversePreview, SafePreview(_chosenTemplate.GetTemplateReversedMergeToDisplay), disposeImage: true);
    }

    private static Mat? SafePreview(Func<Mat?> factory)
    {
        try
        {
            return factory();
        }
        catch (OpenCVException)
        {
            return null;
        }
    }

    private void SetImage(Image target, Mat? image, bool disposeImage)
    {
        if (image is null || image.Empty())
        {
            if (disposeImage)
            {
                image?.Dispose();
            }

            target.Source = null;
            return;
        }

        try
        {
            Cv2.ImEncode(".png", image, out byte[] bytes);
            Bitmap bitmap = new(new MemoryStream(bytes));
            _previewBitmaps.Add(bitmap);
            target.Source = bitmap;
        }
        finally
        {
            if (disposeImage)
            {
                image.Dispose();
            }
        }
    }

    private void ClearPreviews()
    {
        _screenImage.Source = null;
        _rawPreview.Source = null;
        _maskPreview.Source = null;
        _mergePreview.Source = null;
        _reversePreview.Source = null;
        foreach (Bitmap bitmap in _previewBitmaps)
        {
            bitmap.Dispose();
        }

        _previewBitmaps.Clear();
    }

    private void SetCoordinate(PixelPoint point)
    {
        _xPositionBox.Text = point.X.ToString();
        _yPositionBox.Text = point.Y.ToString();
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        _statusBar.Message = message;
        _statusBar.Severity = severity;
        _statusBar.IsOpen = true;
    }

    private static GeometryPoint[] CirclePoints(int left, int top, int right, int bottom)
    {
        int centerX = (left + right) / 2;
        int centerY = (top + bottom) / 2;
        int radius = Math.Max(Math.Abs(right - left), Math.Abs(bottom - top)) / 2;
        return [new GeometryPoint(centerX, centerY), new GeometryPoint(centerX + radius, centerY)];
    }

    private static GeometryPoint[] ClonePoints(IEnumerable<GeometryPoint> points) =>
        points.Select(point => new GeometryPoint(point.X, point.Y)).ToArray();

    private static void DisposeTemplate(TemplateInfo? template)
    {
        if (template is null)
        {
            return;
        }

        template.ScreenImage?.Dispose();
        template.ScreenImage = null;
        template.Dispose();
    }

    private static bool TryParsePoint(string? text, out GeometryPoint point)
    {
        string[] values = (text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length >= 2 && int.TryParse(values[0], out int x) && int.TryParse(values[1], out int y))
        {
            point = new GeometryPoint(x, y);
            return true;
        }

        point = default;
        return false;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"模板管理页缺少 {name}。");

    private sealed record TemplateShapeOption(string Label, string Value)
    {
        public override string ToString() => Label;
    }

    private sealed record PointHistoryEntry(GeometryPoint[] Before, GeometryPoint[] After);
}

internal sealed class ZzzTemplateHelperService
{
    private readonly string _templateRoot;
    private readonly OneDragonEnvironment _environment;
    private readonly TemplateLoader _loader;

    public ZzzTemplateHelperService(string runRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRoot);
        _environment = new OneDragonEnvironment(runRoot);
        _loader = new TemplateLoader(_environment);
        _templateRoot = Path.GetFullPath(_environment.GetResourcePath("assets", "template"));
    }

    public IReadOnlyList<TemplateOption> GetTemplates() =>
        _loader.GetAllTemplateInfoFromDisk(needRaw: false, needConfig: true)
            .Select(template =>
            {
                TemplateOption option = new(
                    string.IsNullOrWhiteSpace(template.TemplateName) ? $"{template.SubDir}/{template.TemplateId}" : template.TemplateName,
                    template.SubDir,
                    template.TemplateId);
                template.Dispose();
                return option;
            })
            .ToArray();

    public TemplateInfo Load(string subDir, string templateId) =>
        new(_environment, subDir, templateId);

    public TemplateInfo Create(string subDir, string templateId) =>
        new(_environment, subDir, templateId);

    public TemplateInfo Copy(TemplateInfo source)
    {
        ValidateIdentity(source.SubDir, source.TemplateId);
        string targetId = NextCopyId(source.SubDir, source.TemplateId);
        string targetDirectory = ResolveDirectory(source.SubDir, targetId);
        bool copiedDirectory = Directory.Exists(source.DirectoryPath);
        if (copiedDirectory)
        {
            CopyDirectory(source.DirectoryPath, targetDirectory);
        }

        TemplateInfo copy = new(_environment, source.SubDir, targetId)
        {
            TemplateName = source.TemplateName,
            TemplateShape = source.TemplateShape,
            AutoMask = source.AutoMask,
        };
        copy.SetPointList(source.PointList);
        if (source.ScreenImage is not null)
        {
            copy.ScreenImage = source.ScreenImage.Clone();
        }

        return copy;
    }

    public void SetScreenImage(TemplateInfo template, byte[] encodedImage)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(encodedImage);
        Mat image = Cv2.ImDecode(encodedImage, ImreadModes.Color);
        if (image.Empty())
        {
            image.Dispose();
            throw new InvalidDataException("图片无法读取。");
        }

        template.ScreenImage?.Dispose();
        template.ScreenImage = image;
        template.SetPointList(template.PointList.ToArray());
    }

    public void SaveConfig(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        template.SaveConfig();
    }

    public void SaveRaw(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        using Mat? raw = template.GetTemplateRawByScreenPoint();
        if (raw is null || raw.Empty())
        {
            throw new InvalidOperationException("当前图片和点位无法生成模板原图。");
        }

        template.SaveRaw();
    }

    public void SaveMask(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        using Mat? mask = template.AutoMask ? template.GetTemplateMaskByScreenPoint() : null;
        if (mask is null || mask.Empty())
        {
            throw new InvalidOperationException("当前形状和点位无法生成模板掩码。");
        }

        template.SaveMask();
    }

    public bool Delete(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        string directory = ResolveDirectory(template.SubDir, template.TemplateId);
        if (!Directory.Exists(directory))
        {
            return false;
        }

        Directory.Delete(directory, recursive: true);
        _loader.ClearCache();
        return true;
    }

    public void ValidateIdentity(string subDir, string templateId)
    {
        ValidateSegment(subDir, "画面");
        ValidateSegment(templateId, "模板ID");
        _ = ResolveDirectory(subDir, templateId);
    }

    private string NextCopyId(string subDir, string templateId)
    {
        string candidate = templateId + "_copy";
        int suffix = 2;
        while (Directory.Exists(ResolveDirectory(subDir, candidate)))
        {
            candidate = templateId + $"_copy{suffix++}";
        }

        return candidate;
    }

    private string ResolveDirectory(string subDir, string templateId)
    {
        string directory = Path.GetFullPath(Path.Combine(_templateRoot, subDir, templateId));
        string rootPrefix = _templateRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("模板路径超出 assets/template? ");
        }

        return directory;
    }

    private static void ValidateSegment(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"{label}不是有效的目录名。");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
    }
}

internal sealed record TemplateOption(string Label, string SubDir, string TemplateId)
{
    public override string ToString() => Label;
}
