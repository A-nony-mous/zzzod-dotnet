using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using OneDragon.Core.Runtime;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;

using ZzzOd.Gui.PageModels.Devtools;

namespace ZzzOd.Gui.Views.FrontierPages.DevTools;

internal sealed record ZzzOperationDebugOption(string Label, string Value);

internal sealed partial class FrontierOperationDebugPage : UserControl, IZzzPageLifecycle
{
    private const string OperationScope = "operation-debug";
    private const string BattleAssistantScope = "battle-assistant";

    private static readonly ZzzOperationDebugOption[] ControlMethods =
    [
        new("键鼠", BattleAssistantConfig.ControlMethodKeyboard),
        new("Xbox", BattleAssistantConfig.ControlMethodXbox),
        new("DS4", BattleAssistantConfig.ControlMethodDs4),
    ];

    private readonly IZzzAppBackend _backend;
    private readonly FAInfoBar _actionBar;
    private readonly FAComboBox _operationTemplateCombo;
    private readonly FACommandBarButton _deleteTemplateButton;
    private readonly ToggleSwitch _repeatToggle;
    private readonly FAComboBox _controlMethodCombo;
    private bool _loading;
    private int? _activeInstanceIndex;
    private string? _runRoot;
    private IReadOnlyList<string> _operationTemplates = [];

    public FrontierOperationDebugPage(IZzzAppBackend backend, ZzzGuiRunIntentService runIntent)
    {
        _backend = backend;
        AvaloniaXamlLoader.Load(this);

        _actionBar = Required<FAInfoBar>("ActionBar");
        _operationTemplateCombo = Required<FAComboBox>("OperationTemplateCombo");
        _deleteTemplateButton = Required<FACommandBarButton>("DeleteTemplateButton");
        _repeatToggle = Required<ToggleSwitch>("RepeatToggle");
        _controlMethodCombo = Required<FAComboBox>("ControlMethodCombo");
        _controlMethodCombo.ItemsSource = ControlMethods;

        RunPanel = new ZzzRunPanel(
            backend,
            ZzzApplicationIds.OperationDebug,
            runIntent: runIntent,
            fixedGroupId: OperationDebugConstants.DefaultGroupId);
        Required<ContentControl>("RunPanelHost").Content = RunPanel;
    }

    internal ZzzRunPanel RunPanel { get; }

    internal IReadOnlyList<string> OperationTemplates => _operationTemplates;

    internal string SelectedOperationTemplate => ReadOperationTemplateInput();

    internal bool RepeatEnabled => _repeatToggle.IsChecked == true;

    internal string? ControlMethod => (_controlMethodCombo.SelectedItem as ZzzOperationDebugOption)?.Value;

    internal int? ActiveInstanceIndex => _activeInstanceIndex;

    public void OnPageShown()
    {
        Reload();
        RunPanel.OnPageShown();
    }

    public void OnPageHidden() => RunPanel.OnPageHidden();

    public void OnPageLeave() => RunPanel.OnPageLeave();

    public void DisposePage() => RunPanel.DisposePage();

    internal void ReloadForTest() => Reload();

    internal bool DeleteTemplateForTest(string templateName) => DeleteTemplate(templateName);

    internal bool SaveOperationTemplateForTest(string templateName) =>
        SaveValue(OperationScope, "operation_template", templateName, OperationDebugConstants.DefaultGroupId);

    internal bool SaveRepeatForTest(bool value) =>
        SaveValue(OperationScope, "repeat_enabled", value, OperationDebugConstants.DefaultGroupId);

    internal bool SaveControlMethodForTest(string value) =>
        SaveValue(BattleAssistantScope, "control_method", value);

    private void Reload()
    {
        ZzzBackendResult<ZzzInstanceDto> instance = _backend.GetCurrentInstance();
        if (!instance.Success || instance.Value is null)
        {
            ShowError(instance.Error ?? "当前实例不可用。");
            return;
        }

        ZzzBackendResult<ZzzHealthDto> health = _backend.GetHealth();
        if (!health.Success || health.Value is null || string.IsNullOrWhiteSpace(health.Value.RunRoot))
        {
			ShowError(health.Error ?? "运行根目录不可用。");
            return;
        }

        int instanceIndex = instance.Value.Index;
        ZzzBackendResult<ZzzConfigScopeValuesDto> operation = _backend.GetConfigScope(
            OperationScope,
            instanceIndex,
            OperationDebugConstants.DefaultGroupId);
        ZzzBackendResult<ZzzConfigScopeValuesDto> battleAssistant = _backend.GetConfigScope(
            BattleAssistantScope,
            instanceIndex);
        if (!operation.Success || operation.Value is null)
        {
            ShowError(operation.Error ?? "指令调试配置读取失败。");
            return;
        }

        if (!battleAssistant.Success || battleAssistant.Value is null)
        {
            ShowError(battleAssistant.Error ?? "操作方式读取失败。");
            return;
        }

        try
        {
            string runRoot = Path.GetFullPath(health.Value.RunRoot);
            IReadOnlyList<string> templates = new OperationTemplateConfigProvider(new OneDragonEnvironment(runRoot))
                .GetOperationTemplateConfigList()
                .Select(item => Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? item.Label)
                .ToArray();
            string selectedTemplate = RequiredString(operation.Value.Values, "operation_template");
            bool repeatEnabled = RequiredBool(operation.Value.Values, "repeat_enabled");
            string controlMethod = RequiredString(battleAssistant.Value.Values, "control_method");

            _loading = true;
            try
            {
                _activeInstanceIndex = instanceIndex;
                _runRoot = runRoot;
                _operationTemplates = templates;
                _operationTemplateCombo.ItemsSource = templates;
                _operationTemplateCombo.SelectedItem = templates.FirstOrDefault(item =>
                    string.Equals(item, selectedTemplate, StringComparison.Ordinal));
                _operationTemplateCombo.Text = selectedTemplate;
                _repeatToggle.IsChecked = repeatEnabled;
                _controlMethodCombo.SelectedItem = ControlMethods.FirstOrDefault(item =>
                    string.Equals(item.Value, controlMethod, StringComparison.Ordinal));
                _deleteTemplateButton.IsEnabled = !string.IsNullOrWhiteSpace(selectedTemplate);
                _actionBar.IsOpen = false;
            }
            finally
            {
                _loading = false;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(exception.Message);
        }
    }

    private void OnOperationTemplateChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_loading || _operationTemplateCombo.SelectedItem is not string templateName)
        {
            return;
        }

        _operationTemplateCombo.Text = templateName;
        _deleteTemplateButton.IsEnabled = true;
        SaveValue(OperationScope, "operation_template", templateName, OperationDebugConstants.DefaultGroupId);
    }

    private void OnOperationTemplateLostFocus(object? sender, RoutedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        string templateName = ReadOperationTemplateInput();
        _deleteTemplateButton.IsEnabled = !string.IsNullOrWhiteSpace(templateName);
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            SaveValue(OperationScope, "operation_template", templateName, OperationDebugConstants.DefaultGroupId);
        }
    }

    private void OnRepeatChanged(object? sender, RoutedEventArgs args)
    {
        if (!_loading && _repeatToggle.IsChecked is bool value)
        {
            SaveValue(OperationScope, "repeat_enabled", value, OperationDebugConstants.DefaultGroupId);
        }
    }

    private void OnControlMethodChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _controlMethodCombo.SelectedItem is ZzzOperationDebugOption option)
        {
            SaveValue(BattleAssistantScope, "control_method", option.Value);
        }
    }

    private void OnDeleteTemplateClicked(object? sender, RoutedEventArgs args) =>
        DeleteTemplate(ReadOperationTemplateInput());

    private bool DeleteTemplate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || string.IsNullOrWhiteSpace(_runRoot))
        {
            return false;
        }

        try
        {
            string root = Path.GetFullPath(Path.Combine(_runRoot, "config", "auto_battle_operation"));
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

    private bool SaveValue(string scope, string key, object value, string? groupId = null)
    {
        if (_activeInstanceIndex is not int instanceIndex)
        {
            ShowError("当前实例不可用。");
            return false;
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            scope,
            new Dictionary<string, object?> { [key] = value },
            instanceIndex,
            groupId));
        if (result.Success)
        {
            _actionBar.IsOpen = false;
            return true;
        }

        ShowError(result.Error ?? (key + " 保存失败。"));
        return false;
    }

    private string ReadOperationTemplateInput() =>
        (_operationTemplateCombo.Text ?? _operationTemplateCombo.SelectedItem as string ?? string.Empty).Trim();

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("配置缺少 " + key + "。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool RequiredBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
			throw new InvalidOperationException("配置缺少 " + key + "。");
        }

        return value is bool flag ? flag : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private void ShowError(string message)
    {
		_actionBar.Title = "指令调试不可用";
        _actionBar.Message = message;
        _actionBar.Severity = FAInfoBarSeverity.Error;
        _actionBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"指令调试页面缺少 {name}。");
}
