using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Devtools;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.Devtools;

internal sealed class ZzzScreenAreaRow
{
    public string AreaName { get; set; } = string.Empty;
    public bool IdMark { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Text { get; set; } = string.Empty;
    public double LcsPercent { get; set; } = 0.5d;
    public string TemplateSubDir { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public double TemplateMatchThreshold { get; set; } = 0.7d;
    public string ColorRangeText { get; set; } = string.Empty;
    public string GotoListText { get; set; } = string.Empty;
    public string? GamepadKey { get; set; }
}

internal sealed partial class ZzzScreenManagePage : UserControl, IZzzPageLifecycle
{
    private readonly IZzzScreenManageService _service;
    private readonly ObservableCollection<ZzzScreenAreaRow> _areas = [];
    private readonly FAInfoBar _statusBar;
    private readonly FAComboBox _screenSelector;
    private readonly TextBox _screenIdBox;
    private readonly TextBox _screenNameBox;
    private readonly CheckBox _pcAltToggle;
    private readonly Image _preview;
    private readonly TextBox _xPositionBox;
    private readonly TextBox _yPositionBox;
    private readonly ZzzScreenAreaTable _areaTable;
    private ZzzScreenDocument? _current;
    private ZzzScreenAreaRow? _selectedArea;
    private Bitmap? _bitmap;
    private Point? _dragStart;
    private bool _loading;

    public ZzzScreenManagePage()
    {
        throw new InvalidOperationException("ZzzScreenManagePage 必须通过页面工厂提供真实画面服务。");
    }

    public ZzzScreenManagePage(IZzzScreenManageService service)
    {
        _service = service;
        AvaloniaXamlLoader.Load(this);
        _statusBar = Required<FAInfoBar>("StatusBar");
        _screenSelector = Required<FAComboBox>("ScreenSelector");
        _screenIdBox = Required<TextBox>("ScreenIdBox");
        _screenNameBox = Required<TextBox>("ScreenNameBox");
        _pcAltToggle = Required<CheckBox>("PcAltToggle");
        _preview = Required<Image>("ScreenPreview");
        _xPositionBox = Required<TextBox>("XPositionBox");
        _yPositionBox = Required<TextBox>("YPositionBox");
        _areaTable = Required<ZzzScreenAreaTable>("AreaTable");
        _areaTable.DataContext = _areas;
        _areaTable.RowSelected += OnAreaSelected;
        SetEditorEnabled(false);
    }

    public void OnPageShown() => ReloadScreenNames();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
        _areaTable.RowSelected -= OnAreaSelected;
        ReplaceBitmap(null);
    }

    private void ReloadScreenNames()
    {
        try
        {
            _loading = true;
            _screenSelector.ItemsSource = _service.ListScreenNames();
            ShowStatus("画面配置已加载。", FAInfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, FAInfoBarSeverity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnScreenSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _screenSelector.SelectedItem is not string screenName)
        {
            return;
        }

        try
        {
            ApplyDocument(_service.LoadScreen(screenName));
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, FAInfoBarSeverity.Error);
        }
    }

    private void OnMergeClicked(object? sender, RoutedEventArgs args)
    {
        RunAction(() =>
        {
            _service.RebuildMergedConfig();
            ReloadScreenNames();
        }, "合并配置已更新。");
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs args)
    {
        ApplyDocument(new ZzzScreenDocument(string.Empty, string.Empty, string.Empty, string.Empty, false, []));
        ShowStatus("已新建画面。", FAInfoBarSeverity.Informational);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is null)
        {
            return;
        }

        RunAction(() =>
        {
            ZzzScreenDocument document = BuildDocument();
            _service.SaveScreen(document);
            ApplyDocument(_service.LoadScreen(document.ScreenName));
            ReloadScreenNames();
        }, "画面已保存。");
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is null || string.IsNullOrWhiteSpace(_current.OldScreenId))
        {
            return;
        }

        FAContentDialog dialog = new()
        {
            Title = "删除画面",
            Content = _screenNameBox.Text,
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Close,
        };
        if (await dialog.ShowAsync(TopLevel.GetTopLevel(this)).ConfigureAwait(true) != FAContentDialogResult.Primary)
        {
            return;
        }

        RunAction(() =>
        {
            _service.DeleteScreen(_current.OldScreenId);
            CancelEdit();
            ReloadScreenNames();
        }, "画面已删除。");
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs args) => CancelEdit();

    private void CancelEdit()
    {
        _current = null;
        _areas.Clear();
        _selectedArea = null;
        _screenSelector.SelectedIndex = -1;
        _screenIdBox.Text = string.Empty;
        _screenNameBox.Text = string.Empty;
        _pcAltToggle.IsChecked = false;
        _xPositionBox.Text = string.Empty;
        _yPositionBox.Text = string.Empty;
        ReplaceBitmap(null);
        SetEditorEnabled(false);
    }

    private async void OnChooseImageClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择图片",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        }).ConfigureAwait(true);
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            RunAction(() => ShowImage(_service.ReadImage(path)), "图片已加载。");
        }
    }

    private void OnScreenshotClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is null)
        {
            OnCreateClicked(sender, args);
        }

        RunAction(() => ShowImage(_service.CaptureScreenshot()), "截图已加载。");
    }

    private async void OnImportTemplateClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择模板配置文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("YML") { Patterns = ["*.yml"] }],
        }).ConfigureAwait(true);
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        RunAction(() =>
        {
            ZzzImportedTemplateArea area = _service.ImportTemplateArea(path);
            _areas.Add(new ZzzScreenAreaRow
            {
                AreaName = area.AreaName,
                X1 = area.X1,
                Y1 = area.Y1,
                X2 = area.X2,
                Y2 = area.Y2,
                TemplateSubDir = area.TemplateSubDir,
                TemplateId = area.TemplateId,
            });
        }, "模板区域已导入。");
    }

    private void OnScreenInfoChanged(object? sender, TextChangedEventArgs args)
    {
    }

    private void OnPcAltChanged(object? sender, RoutedEventArgs args)
    {
    }

    private void OnAddAreaClicked(object? sender, RoutedEventArgs args)
    {
        if (_current is not null)
        {
            ZzzScreenAreaRow row = new();
            _areas.Add(row);
            _selectedArea = row;
        }
    }

    private void OnDeleteAreaClicked(object? sender, RoutedEventArgs args)
    {
        if (_selectedArea is not null)
        {
            _areas.Remove(_selectedArea);
            _selectedArea = null;
        }
    }

    private void OnPopupTableClicked(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        ZzzScreenAreaTable table = new() { DataContext = _areas };
        table.RowSelected += OnAreaSelected;
        Window window = new()
        {
            Title = "区域表格编辑",
            Width = 1200,
            Height = 600,
            Content = table,
        };
        window.Closed += (_, _) => table.RowSelected -= OnAreaSelected;
        window.Show(owner);
    }

    private void OnAreaSelected(object? sender, ZzzScreenAreaRow row) => _selectedArea = row;

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        Point? point = GetImagePoint(args.GetPosition(_preview));
        if (point is null)
        {
            return;
        }

        _dragStart = point;
        _xPositionBox.Text = ((int)point.Value.X).ToString();
        _yPositionBox.Text = ((int)point.Value.Y).ToString();
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        Point? end = GetImagePoint(args.GetPosition(_preview));
        if (_dragStart is not { } start || end is null || _selectedArea is null)
        {
            _dragStart = null;
            return;
        }

        _selectedArea.X1 = Math.Min(start.X, end.Value.X);
        _selectedArea.Y1 = Math.Min(start.Y, end.Value.Y);
        _selectedArea.X2 = Math.Max(start.X, end.Value.X);
        _selectedArea.Y2 = Math.Max(start.Y, end.Value.Y);
        _areaTable.DataContext = null;
        _areaTable.DataContext = _areas;
        _dragStart = null;
    }

    private Point? GetImagePoint(Point controlPoint)
    {
        if (_bitmap is null || _preview.Bounds.Width <= 0 || _preview.Bounds.Height <= 0)
        {
            return null;
        }

        double scale = Math.Min(_preview.Bounds.Width / _bitmap.PixelSize.Width, _preview.Bounds.Height / _bitmap.PixelSize.Height);
        double renderedWidth = _bitmap.PixelSize.Width * scale;
        double renderedHeight = _bitmap.PixelSize.Height * scale;
        double left = (_preview.Bounds.Width - renderedWidth) / 2d;
        double top = (_preview.Bounds.Height - renderedHeight) / 2d;
        if (controlPoint.X < left || controlPoint.Y < top || controlPoint.X > left + renderedWidth || controlPoint.Y > top + renderedHeight)
        {
            return null;
        }

        return new Point((controlPoint.X - left) / scale, (controlPoint.Y - top) / scale);
    }

    private void ApplyDocument(ZzzScreenDocument document)
    {
        _loading = true;
        try
        {
            _current = document;
            _screenIdBox.Text = document.ScreenId;
            _screenNameBox.Text = document.ScreenName;
            _pcAltToggle.IsChecked = document.PcAlt;
            _areas.Clear();
            foreach (ZzzScreenAreaDocument area in document.Areas)
            {
                _areas.Add(ToRow(area));
            }

            _selectedArea = null;
            SetEditorEnabled(true);
        }
        finally
        {
            _loading = false;
        }
    }

    private ZzzScreenDocument BuildDocument() => new(
        _current?.OldScreenId ?? string.Empty,
        _screenIdBox.Text?.Trim() ?? string.Empty,
        _screenNameBox.Text?.Trim() ?? string.Empty,
        _current?.AppId ?? string.Empty,
        _pcAltToggle.IsChecked == true,
        _areas.Select(ToDocument).ToArray());

    private static ZzzScreenAreaRow ToRow(ZzzScreenAreaDocument area) => new()
    {
        AreaName = area.AreaName,
        IdMark = area.IdMark,
        X1 = area.X1,
        Y1 = area.Y1,
        X2 = area.X2,
        Y2 = area.Y2,
        Text = area.Text,
        LcsPercent = area.LcsPercent,
        TemplateSubDir = area.TemplateSubDir,
        TemplateId = area.TemplateId,
        TemplateMatchThreshold = area.TemplateMatchThreshold,
        ColorRangeText = area.ColorRange is null ? string.Empty : JsonSerializer.Serialize(area.ColorRange),
        GotoListText = string.Join(',', area.GotoList),
        GamepadKey = area.GamepadKey,
    };

    private static ZzzScreenAreaDocument ToDocument(ZzzScreenAreaRow area) => new(
        area.AreaName.Trim(),
        area.IdMark,
        checked((int)area.X1),
        checked((int)area.Y1),
        checked((int)area.X2),
        checked((int)area.Y2),
        area.Text.Trim(),
        area.LcsPercent,
        area.TemplateSubDir.Trim(),
        area.TemplateId.Trim(),
        area.TemplateMatchThreshold,
        ParseColorRange(area.ColorRangeText),
        area.GotoListText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        string.IsNullOrWhiteSpace(area.GamepadKey) ? null : area.GamepadKey.Trim());

    private static IReadOnlyList<IReadOnlyList<int>>? ParseColorRange(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        int[][]? ranges = JsonSerializer.Deserialize<int[][]>(text);
        if (ranges is not { Length: 2 } || ranges.Any(range => range.Length != 3))
        {
            throw new FormatException("颜色范围需要 [[r,g,b],[r,g,b]]?");
        }

        return ranges;
    }

    private void ShowImage(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        ReplaceBitmap(new Bitmap(stream));
    }

    private void ReplaceBitmap(Bitmap? bitmap)
    {
        Bitmap? old = _bitmap;
        _bitmap = bitmap;
        _preview.Source = bitmap;
        old?.Dispose();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _screenIdBox.IsEnabled = enabled;
        _screenNameBox.IsEnabled = enabled;
        _pcAltToggle.IsEnabled = enabled;
        _areaTable.IsEnabled = enabled;
        _preview.IsEnabled = enabled;
    }

    private void RunAction(Action action, string success)
    {
        try
        {
            action();
            ShowStatus(success, FAInfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(exception.Message, FAInfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, FAInfoBarSeverity severity)
    {
        _statusBar.Title = severity == FAInfoBarSeverity.Error ? "画面管理错误" : string.Empty;
        _statusBar.Message = message;
        _statusBar.Severity = severity;
        _statusBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"画面管理页缺少 {name}。");
}
