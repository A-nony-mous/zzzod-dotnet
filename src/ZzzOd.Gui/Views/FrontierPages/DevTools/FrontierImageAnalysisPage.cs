using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierImageAnalysisPage : UserControl
{
    private const string CreatePipelineText = "[ 新建流水线... ]";
    private readonly IZzzAppBackend _backend;
    private readonly IZzzImageAnalysisService _service;
    private readonly ObservableCollection<ImageAnalysisStepEditor> _steps = [];
    private readonly ObservableCollection<ImageAnalysisParameterEditor> _parameters = [];
    private byte[]? _sourceBytes;
    private byte[]? _processedBytes;
    private bool _showProcessed;
    private bool _loading;
    private string? _activePipeline;

    public FrontierImageAnalysisPage()
    {
        AvaloniaXamlLoader.Load(this);
        _backend = null!;
        _service = null!;
    }

    public FrontierImageAnalysisPage(IZzzAppBackend backend, IZzzImageAnalysisService service)
    {
        _backend = backend;
        _service = service;
        AvaloniaXamlLoader.Load(this);
        Required<ListBox>("StepList").ItemsSource = _steps;
        Required<ItemsControl>("ParameterList").ItemsSource = _parameters;
        Required<FAComboBox>("AddStepCombo").ItemsSource = _service.GetAvailableSteps().Select(item => item.Name).ToArray();
        ReloadPipelines();
    }

    internal IReadOnlyList<string> PipelineSteps => _steps.Select(step => step.Name).ToArray();
    internal string LastStatusText => Required<TextBox>("ResultText").Text ?? string.Empty;
    internal string? LastSavedPath
    {
        get
        {
            if (_activePipeline is null) return null;
            string? runRoot = _backend.GetHealth().Value?.RunRoot;
            return string.IsNullOrWhiteSpace(runRoot) ? null : Path.Combine(runRoot, "assets", "image_analysis_pipelines", $"{_activePipeline}.yml");
        }
    }

    internal void OpenImageForTest(string filePath) => LoadImage(File.ReadAllBytes(filePath));
    internal void LoadScreenshotForTest() => LoadScreenshot();
    internal void ToggleViewForTest() => ToggleView();
    internal void RunPipelineForTest() => RunPipeline();
    internal string SaveAsPipelineForTest(string name) { SavePipeline(name); return LastSavedPath!; }
    internal void RenamePipelineForTest(string name) { if (_activePipeline is not null) { _service.RenamePipeline(_activePipeline, name); _activePipeline = name; ReloadPipelines(); } }
    internal bool DeletePipelineForTest() { if (_activePipeline is null) return false; _service.DeletePipeline(_activePipeline); _activePipeline = null; ReloadPipelines(); return true; }
    internal void AddStepForTest(string name) => AddStep(name);
    internal void SetParameterForTest(string key, object? value) { ImageAnalysisParameterEditor? parameter = _parameters.FirstOrDefault(item => item.Name == key); if (parameter is not null) parameter.SetValue(value); }

    private async void OnOpenImage(object? sender, RoutedEventArgs args)
    {
        TopLevel? top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开图片文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }],
        });
        if (files.Count == 0) return;
        await using Stream stream = await files[0].OpenReadAsync();
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer);
        LoadImage(buffer.ToArray());
    }

    private void OnScreenshot(object? sender, RoutedEventArgs args) => LoadScreenshot();
    private void LoadScreenshot()
    {
        ZzzBackendResult<ZzzScreenshotDto> result = _backend.GetScreenshot();
        if (!result.Success || result.Value is null) { SetResult(result.Error ?? "截图不可用。"); return; }
        LoadImage(result.Value.Bytes);
    }

    private void LoadImage(byte[] bytes)
    {
        _sourceBytes = bytes;
        _processedBytes = null;
        _showProcessed = false;
        ShowImage(bytes);
        UpdateViewLabel();
        SetResult(string.Empty);
    }

    private void OnToggleView(object? sender, RoutedEventArgs args) => ToggleView();
    private void ToggleView()
    {
        if (_sourceBytes is null) return;
        _showProcessed = !_showProcessed && _processedBytes is not null;
        ShowImage(_showProcessed ? _processedBytes! : _sourceBytes);
        UpdateViewLabel();
    }

    private void OnRun(object? sender, RoutedEventArgs args) => RunPipeline();
    private void RunPipeline()
    {
        if (_sourceBytes is null) { SetResult("请先打开一张图片"); return; }
        if (_activePipeline is null) { SetResult("请先选择一个流水线"); return; }
        try
        {
            ImageAnalysisExecutionResult result = _service.Execute(ToPipeline(), _sourceBytes);
            _processedBytes = result.DisplayImage;
            _showProcessed = true;
            ShowImage(_processedBytes);
            UpdateViewLabel();
            List<string> lines = [.. result.AnalysisResults];
            if (lines.Count > 0) lines.Add("====================");
            lines.Add("--- 性能分析 ---");
            lines.AddRange(result.StepTimings.Select(item => $"[{item.StepName}] - {item.Milliseconds:F2} ms"));
            lines.Add("--------------------");
            lines.Add($"总耗时: {result.TotalMilliseconds:F2} ms");
            SetResult(string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception) { SetResult(exception.Message); }
    }

    private async void OnColorChannels(object? sender, RoutedEventArgs args)
    {
        if (_sourceBytes is null) { SetResult("请先打开一张图片"); return; }
        try
        {
            ImageAnalysisColorChannels channels = _service.GetColorChannels(_sourceBytes);
            Required<ItemsControl>("ColorSpaceList").ItemsSource = channels.Spaces.Select(space => new ColorSpaceEditor(space)).ToArray();
            if (TopLevel.GetTopLevel(this) is { } owner)
            {
                await Required<FAContentDialog>("ColorChannelsDialog").ShowAsync(owner);
            }
        }
        catch (Exception exception) { SetResult(exception.Message); }
    }

    private void OnPipelineSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || Required<FAComboBox>("PipelineCombo").SelectedItem is not string name) return;
        if (name == CreatePipelineText) { _activePipeline = null; _steps.Clear(); _parameters.Clear(); return; }
        try
        {
            _activePipeline = name;
            LoadPipeline(_service.LoadPipeline(name));
        }
        catch (Exception exception) { SetResult(exception.Message); }
    }

    private void LoadPipeline(ImageAnalysisPipeline pipeline)
    {
        _steps.Clear();
        foreach (ImageAnalysisStep step in pipeline.Steps) _steps.Add(new ImageAnalysisStepEditor(step, FindDefinition(step.Name)));
        Required<ListBox>("StepList").SelectedIndex = _steps.Count > 0 ? 0 : -1;
    }

    private void OnSavePipeline(object? sender, RoutedEventArgs args)
    {
        if (_activePipeline is null) { _ = PromptAndSave("保存流水线", null); return; }
        SavePipeline(_activePipeline);
    }
    private void OnSaveAsPipeline(object? sender, RoutedEventArgs args) => _ = PromptAndSave("另存为", null);
    private void OnRenamePipeline(object? sender, RoutedEventArgs args) => _ = PromptAndRename();
    private async Task PromptAndSave(string title, string? initial)
    {
        string? name = await PromptName(title, initial);
        if (!string.IsNullOrWhiteSpace(name)) SavePipeline(name);
    }
    private void SavePipeline(string name)
    {
        try { _service.SavePipeline(name, ToPipeline()); _activePipeline = name; ReloadPipelines(); SetResult($"流水线已保存：{name}"); }
        catch (Exception exception) { SetResult(exception.Message); }
    }
    private async Task PromptAndRename()
    {
        if (_activePipeline is null) return;
        string old = _activePipeline;
        string? name = await PromptName("重命名流水线", old);
        if (string.IsNullOrWhiteSpace(name) || name == old) return;
        try { _service.RenamePipeline(old, name); _activePipeline = name; ReloadPipelines(); SetResult($"流水线已重命名：{name}"); }
        catch (Exception exception) { SetResult(exception.Message); }
    }
    private async Task<string?> PromptName(string title, string? initial)
    {
        FAContentDialog dialog = Required<FAContentDialog>("PipelineNameDialog");
        dialog.Title = title;
        TextBox text = Required<TextBox>("PipelineNameText");
        text.Text = initial ?? string.Empty;
        if (TopLevel.GetTopLevel(this) is not { } owner)
        {
            return null;
        }

        return await dialog.ShowAsync(owner) == FAContentDialogResult.Primary ? text.Text?.Trim() : null;
    }
    private async void OnDeletePipeline(object? sender, RoutedEventArgs args)
    {
        if (_activePipeline is null) return;
        FAContentDialog confirm = new() { Title = "删除流水线", Content = $"确定要删除流水线 {_activePipeline} 吗？", PrimaryButtonText = "确定", CloseButtonText = "取消", DefaultButton = FAContentDialogButton.Close };
        if (TopLevel.GetTopLevel(this) is not { } owner
            || await confirm.ShowAsync(owner) != FAContentDialogResult.Primary)
        {
            return;
        }
        try { string deleted = _activePipeline; _service.DeletePipeline(deleted); _activePipeline = null; _steps.Clear(); _parameters.Clear(); ReloadPipelines(); SetResult($"流水线已删除：{deleted}"); }
        catch (Exception exception) { SetResult(exception.Message); }
    }

    private void OnAddStep(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || Required<FAComboBox>("AddStepCombo").SelectedItem is not string name) return;
        AddStep(name);
        Required<FAComboBox>("AddStepCombo").SelectedIndex = -1;
    }
    private void AddStep(string name)
    {
        ImageAnalysisStepDefinition definition = FindDefinition(name);
        Dictionary<string, object?> values = definition.Parameters.ToDictionary(item => item.Name, item => CloneDefault(item.DefaultValue), StringComparer.Ordinal);
        _steps.Add(new ImageAnalysisStepEditor(new ImageAnalysisStep(name, values), definition));
        Required<ListBox>("StepList").SelectedIndex = _steps.Count - 1;
    }
    private void OnDeleteStep(object? sender, RoutedEventArgs args) { int index = Required<ListBox>("StepList").SelectedIndex; if (index < 0) return; _steps.RemoveAt(index); Required<ListBox>("StepList").SelectedIndex = _steps.Count == 0 ? -1 : Math.Max(0, index - 1); }
    private async void OnCopyCode(object? sender, RoutedEventArgs args) { TopLevel? top = TopLevel.GetTopLevel(this); if (top?.Clipboard is null) return; await top.Clipboard.SetTextAsync(GenerateCode()); SetResult("已将方法代码复制到剪贴板"); }
    private void OnMoveUp(object? sender, RoutedEventArgs args) => MoveSelected(-1);
    private void OnMoveDown(object? sender, RoutedEventArgs args) => MoveSelected(1);
    private void MoveSelected(int direction) { ListBox list = Required<ListBox>("StepList"); int index = list.SelectedIndex; int target = index + direction; if (index < 0 || target < 0 || target >= _steps.Count) return; _steps.Move(index, target); list.SelectedIndex = target; }
    private void OnStepSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _parameters.Clear();
        if (Required<ListBox>("StepList").SelectedItem is not ImageAnalysisStepEditor step) { Required<TextBlock>("ParameterTitle").Text = "参数设置"; Required<TextBlock>("ParameterDescription").Text = string.Empty; return; }
        Required<TextBlock>("ParameterTitle").Text = $"{step.Name} - 参数设置";
        Required<TextBlock>("ParameterDescription").Text = step.Definition.Description;
        foreach (ImageAnalysisParameterDefinition definition in step.Definition.Parameters) _parameters.Add(new ImageAnalysisParameterEditor(step, definition, GetOptions(definition, step)));
    }

    private void OnParameterNumberChanged(FANumberBox sender, FANumberBoxValueChangedEventArgs args) { if (sender.DataContext is ImageAnalysisParameterEditor p && !double.IsNaN(args.NewValue)) p.SetNumber(args.NewValue); }
    private void OnParameterBooleanChanged(object? sender, RoutedEventArgs args) { if (sender is ToggleSwitch { DataContext: ImageAnalysisParameterEditor p } toggle) p.SetValue(toggle.IsChecked == true); }
    private void OnParameterChoiceChanged(object? sender, SelectionChangedEventArgs args) { if (sender is FAComboBox { DataContext: ImageAnalysisParameterEditor p } combo && combo.SelectedItem is string value) { p.SetValue(value); if (p.Definition.Kind == ImageAnalysisParameterKind.Screen) RefreshAreaParameter(); } }
    private void OnTuple0Changed(FANumberBox sender, FANumberBoxValueChangedEventArgs args) { if (sender.DataContext is ImageAnalysisParameterEditor p && !double.IsNaN(args.NewValue)) p.SetTuple(0, args.NewValue); }
    private void OnTuple1Changed(FANumberBox sender, FANumberBoxValueChangedEventArgs args) { if (sender.DataContext is ImageAnalysisParameterEditor p && !double.IsNaN(args.NewValue)) p.SetTuple(1, args.NewValue); }
    private void OnTuple2Changed(FANumberBox sender, FANumberBoxValueChangedEventArgs args) { if (sender.DataContext is ImageAnalysisParameterEditor p && !double.IsNaN(args.NewValue)) p.SetTuple(2, args.NewValue); }
    private void RefreshAreaParameter() { if (Required<ListBox>("StepList").SelectedItem is ImageAnalysisStepEditor selected) { int index = Required<ListBox>("StepList").SelectedIndex; Required<ListBox>("StepList").SelectedIndex = -1; Required<ListBox>("StepList").SelectedIndex = index; } }

    private IReadOnlyList<string> GetOptions(ImageAnalysisParameterDefinition definition, ImageAnalysisStepEditor step) => definition.Kind switch
    {
        ImageAnalysisParameterKind.Template => _service.GetTemplateNames(),
        ImageAnalysisParameterKind.Screen => _service.GetScreenNames(),
        ImageAnalysisParameterKind.Area => _service.GetAreaNames(Convert.ToString(step.Step.Parameters.GetValueOrDefault(definition.Parent ?? string.Empty), CultureInfo.InvariantCulture) ?? string.Empty),
        _ => definition.Options ?? [],
    };
    private ImageAnalysisStepDefinition FindDefinition(string name) => _service.GetAvailableSteps().First(item => item.Name == name);
    private ImageAnalysisPipeline ToPipeline() => new(_steps.Select(step => step.Step).ToArray());
    private string GenerateCode() { StringBuilder code = new(); code.AppendLine("pipeline = CvPipeline()"); foreach (ImageAnalysisStepEditor step in _steps) code.AppendLine($"pipeline.steps.append(cv_service.create_step('{step.Name}', {string.Join(", ", step.Step.Parameters.Select(pair => $"{pair.Key}={pair.Value}"))}))"); return code.ToString(); }
    private void ReloadPipelines() { _loading = true; try { FAComboBox combo = Required<FAComboBox>("PipelineCombo"); combo.ItemsSource = _service.GetPipelineNames().Append(CreatePipelineText).ToArray(); combo.SelectedItem = _activePipeline; } finally { _loading = false; } }
    private void ShowImage(byte[] bytes) { Required<Image>("PreviewImage").Source = new Bitmap(new MemoryStream(bytes)); }
    private void UpdateViewLabel() { Required<FACommandBarButton>("ToggleViewButton").Label = _showProcessed ? "处理后" : "原图"; }
    private void SetResult(string text) => Required<TextBox>("ResultText").Text = text;
    private static object? CloneDefault(object? value) => value is int[] array ? array.ToArray() : value;
    private T Required<T>(string name) where T : Control => this.FindControl<T>(name) ?? throw new InvalidOperationException($"图像分析页缺少 {name}。");

    private sealed record ImageAnalysisStepEditor(ImageAnalysisStep Step, ImageAnalysisStepDefinition Definition) { public string Name => Step.Name; }
    private sealed class ImageAnalysisParameterEditor
    {
        private readonly ImageAnalysisStepEditor _step;
        public ImageAnalysisParameterEditor(ImageAnalysisStepEditor step, ImageAnalysisParameterDefinition definition, IReadOnlyList<string> options) { _step = step; Definition = definition; Options = options; }
        public ImageAnalysisParameterDefinition Definition { get; }
        public string Name => Definition.Name;
        public string Label => Definition.Label;
        public string? ToolTip => Definition.ToolTip;
        public double Minimum => Definition.Minimum;
        public double Maximum => Definition.Maximum;
        public double SmallChange => Definition.Kind == ImageAnalysisParameterKind.Integer ? 1 : 0.1;
        public IReadOnlyList<string> Options { get; }
        public bool IsNumber => Definition.Kind is ImageAnalysisParameterKind.Integer or ImageAnalysisParameterKind.Double;
        public bool IsBoolean => Definition.Kind == ImageAnalysisParameterKind.Boolean;
        public bool IsChoice => Definition.Kind is ImageAnalysisParameterKind.Choice or ImageAnalysisParameterKind.Template or ImageAnalysisParameterKind.Screen or ImageAnalysisParameterKind.Area;
        public bool IsTuple => Definition.Kind == ImageAnalysisParameterKind.IntegerTuple;
        public double NumberValue => Convert.ToDouble(Value ?? 0, CultureInfo.InvariantCulture);
        public bool BooleanValue => Convert.ToBoolean(Value ?? false, CultureInfo.InvariantCulture);
        public string? ChoiceValue => Convert.ToString(Value, CultureInfo.InvariantCulture);
        public double Tuple0 => TupleAt(0); public double Tuple1 => TupleAt(1); public double Tuple2 => TupleAt(2);
        private object? Value => _step.Step.Parameters.GetValueOrDefault(Name);
        public void SetNumber(double value) => SetValue(Definition.Kind == ImageAnalysisParameterKind.Integer ? Convert.ToInt32(value) : value);
        public void SetTuple(int index, double value) { int[] tuple = ToTuple(); tuple[index] = Convert.ToInt32(value); SetValue(tuple); }
        public void SetValue(object? value) => _step.Step.Parameters[Name] = value;
        private double TupleAt(int index) => ToTuple()[index];
        private int[] ToTuple() { if (Value is int[] array) return array.ToArray(); if (Value is IEnumerable<object> objects) return objects.Select(Convert.ToInt32).Concat([0, 0, 0]).Take(3).ToArray(); return [0, 0, 0]; }
    }
    private sealed class ColorSpaceEditor
    {
        public ColorSpaceEditor(ImageAnalysisColorSpace space) { Name = space.Name; Channels = space.Channels.Select(channel => new ColorChannelEditor(space.Name, channel)).ToArray(); }
        public string Name { get; }
        public IReadOnlyList<ColorChannelEditor> Channels { get; }
    }
    private sealed class ColorChannelEditor
    {
        public ColorChannelEditor(string space, ImageAnalysisChannel channel) { Title = $"{space} - {channel.Name}"; Image = new Bitmap(new MemoryStream(channel.ImageBytes)); }
        public string Title { get; }
        public Bitmap Image { get; }
    }
}
