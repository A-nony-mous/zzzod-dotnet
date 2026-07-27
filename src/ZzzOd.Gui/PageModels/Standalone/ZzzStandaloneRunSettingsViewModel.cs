using System.Collections;
using System.Text.Json;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.Views.FrontierPages.Standalone;

internal sealed class ZzzStandaloneRunSettingsViewModel : ZzzConfigSectionViewModel
{
    private static readonly ZzzConfigField AppListField = new(
        "app_list",
        typeof(List<string>),
        new List<string>(),
        FromConfig: NormalizeAppList);
    private static readonly ZzzConfigField ActiveAppIdField = new("active_app_id", typeof(string), string.Empty);
    private static readonly IReadOnlyList<ZzzConfigField> FieldList = [AppListField, ActiveAppIdField];

    private readonly IZzzAppBackend _backend;
    private IReadOnlyList<string> _appIds = [];
    private string? _selectedAppId;
    private bool _activeAppIdConfigured;

    public ZzzStandaloneRunSettingsViewModel(IZzzAppBackend backend, Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _backend = backend;
    }

    protected override string ScopeName => "standalone-app";

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    public IReadOnlyList<string> AppIds => _appIds;

    public string? SelectedAppId
    {
        get => _selectedAppId;
        private set => SetProperty(ref _selectedAppId, value);
    }

    public string? NormalizeSelection(IEnumerable<string> availableAppIds)
    {
        string[] available = availableAppIds.ToArray();
        string? selected = available.Contains(SelectedAppId, StringComparer.Ordinal)
            ? SelectedAppId
            : available.FirstOrDefault();
        bool requiresSave = !_activeAppIdConfigured
            || !string.Equals(SelectedAppId ?? string.Empty, selected ?? string.Empty, StringComparison.Ordinal);
        SelectedAppId = selected;
        if (requiresSave)
        {
            SaveValue(ActiveAppIdField, selected ?? string.Empty);
        }

        return selected;
    }

    public bool SaveActiveSelection(string? appId)
    {
        SelectedAppId = appId;
        _activeAppIdConfigured = true;
        return SaveValue(ActiveAppIdField, appId ?? string.Empty);
    }

    public bool SaveConfiguration(IReadOnlyList<string> appIds, string? activeAppId)
    {
        _appIds = appIds.ToArray();
        SelectedAppId = activeAppId;
        _activeAppIdConfigured = true;
        OnPropertyChanged(nameof(AppIds));
        return SaveValue(AppListField, _appIds);
    }

    protected override void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
        _appIds = GetValue<List<string>>(AppListField).ToArray();
        SelectedAppId = GetValue<string>(ActiveAppIdField);
        _activeAppIdConfigured = values.Values.ContainsKey(ActiveAppIdField.Key);
        OnPropertyChanged(nameof(AppIds));
    }

    protected override ZzzBackendResult<ZzzConfigScopeValuesDto> SaveFieldCore(
        ZzzConfigField field,
        object? value)
    {
        if (ReferenceEquals(field, AppListField))
        {
            return _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
                ScopeName,
                new Dictionary<string, object?>
                {
                    [AppListField.Key] = _appIds.ToList(),
                    [ActiveAppIdField.Key] = SelectedAppId ?? string.Empty,
                }));
        }

        return base.SaveFieldCore(field, value);
    }

    private static object NormalizeAppList(object? value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } json)
        {
            return json.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        if (value is IEnumerable items)
        {
            return items.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        return new List<string>();
    }
}
