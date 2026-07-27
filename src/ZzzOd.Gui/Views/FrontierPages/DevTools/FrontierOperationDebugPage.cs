using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.PageModels.Devtools;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed partial class FrontierOperationDebugPage : UserControl, IZzzPageLifecycle
{
    private readonly FAInfoBar _actionBar;
    private readonly FAComboBox _operationTemplateCombo;
    private readonly FACommandBarButton _deleteTemplateButton;
    private readonly ZzzOperationDebugSettingsViewModel _viewModel;

    public FrontierOperationDebugPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _viewModel = new ZzzOperationDebugSettingsViewModel(backend, ShowError);
        AvaloniaXamlLoader.Load(this);

        _actionBar = Required<FAInfoBar>("ActionBar");
        _operationTemplateCombo = Required<FAComboBox>("OperationTemplateCombo");
        _deleteTemplateButton = Required<FACommandBarButton>("DeleteTemplateButton");

        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.OperationDebug,
            runIntent: runIntent,
            fixedGroupId: OperationDebugConstants.DefaultGroupId);
        Required<ContentControl>("RunPanelHost").Content = RunPanel;
        DataContext = _viewModel;
    }

    internal ZzzRunPanel RunPanel { get; }

    internal IReadOnlyList<string> OperationTemplates => _viewModel.OperationTemplates;

    internal string SelectedOperationTemplate => ReadOperationTemplateInput();

    internal bool RepeatEnabled => _viewModel.RepeatEnabled;

    internal string? ControlMethod => _viewModel.SelectedControlMethod?.Value;

    internal int? ActiveInstanceIndex => _viewModel.ActiveInstanceIndex;

    public void OnPageShown()
    {
        Reload();
        RunPanel.OnPageShown();
    }

    public void OnPageHidden() => RunPanel.OnPageHidden();

    public void OnPageLeave() => RunPanel.OnPageLeave();

    public void DisposePage()
    {
        _viewModel.DisposePage();
        RunPanel.DisposePage();
    }

    internal void ReloadForTest() => Reload();

    internal bool DeleteTemplateForTest(string templateName) => DeleteTemplate(templateName);

    internal bool SaveOperationTemplateForTest(string templateName) => _viewModel.SaveOperationTemplate(templateName);

    internal bool SaveRepeatForTest(bool value) => _viewModel.SaveRepeat(value);

    internal bool SaveControlMethodForTest(string value) => _viewModel.SaveControlMethod(value);

    private void Reload()
    {
        _viewModel.OnPageShown();
        _operationTemplateCombo.Text = _viewModel.OperationTemplate;
        _deleteTemplateButton.IsEnabled = _viewModel.ValuesAvailable
            && !string.IsNullOrWhiteSpace(_viewModel.OperationTemplate);
    }

    private void OnOperationTemplateLostFocus(object? sender, RoutedEventArgs args)
    {
        string templateName = ReadOperationTemplateInput();
        _deleteTemplateButton.IsEnabled = _viewModel.ValuesAvailable && !string.IsNullOrWhiteSpace(templateName);
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            _viewModel.OperationTemplate = templateName;
        }
    }

    private void OnDeleteTemplateClicked(object? sender, RoutedEventArgs args) =>
        DeleteTemplate(ReadOperationTemplateInput());

    private bool DeleteTemplate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || string.IsNullOrWhiteSpace(_viewModel.RunRoot))
        {
            return false;
        }

        try
        {
            string root = Path.GetFullPath(Path.Combine(_viewModel.RunRoot, "config", "auto_battle_operation"));
            string relative = templateName.Replace('/', Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(root, relative + ".yml"));
            string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("指令配置路径超出允许目录。");
            }

            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            Reload();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(exception.Message);
            return false;
        }
    }

    private string ReadOperationTemplateInput() =>
        (_operationTemplateCombo.Text ?? _operationTemplateCombo.SelectedItem as string ?? string.Empty).Trim();

    private void ShowError(string? message)
    {
        _actionBar.Title = "指令调试不可用";
        _actionBar.Message = message ?? string.Empty;
        _actionBar.Severity = FAInfoBarSeverity.Error;
        _actionBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"指令调试页面缺少 {name}。");
}
