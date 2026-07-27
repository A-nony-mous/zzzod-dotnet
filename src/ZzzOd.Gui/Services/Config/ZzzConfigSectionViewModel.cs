using System.Runtime.CompilerServices;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Architecture;

namespace ZzzOd.Gui.Services.Config;

internal abstract class ZzzConfigSectionViewModel : ZzzPageViewModel
{
    private readonly IZzzAppBackend _backend;
    private readonly Action<string?>? _errorReporter;
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private string? _lastError;
    private bool _loading;

    protected ZzzConfigSectionViewModel(
        IZzzAppBackend backend,
        Action<string?>? errorReporter = null)
    {
        _backend = backend;
        _errorReporter = errorReporter;
    }

    protected abstract string ScopeName { get; }

    protected abstract IReadOnlyList<ZzzConfigField> Fields { get; }

    protected virtual int? InstanceIndex => null;

    protected virtual string? GroupId => null;

    protected virtual void OnScopeLoaded(ZzzConfigScopeValuesDto values)
    {
    }

    protected virtual void OnFieldSaved(ZzzConfigField field, ZzzConfigScopeValuesDto values)
    {
    }

    protected virtual ZzzBackendResult<ZzzConfigScopeValuesDto> SaveFieldCore(
        ZzzConfigField field,
        object? value) =>
        _backend.SaveConfigScope(
            new ZzzSaveConfigScopeRequest(
                ScopeName,
                new Dictionary<string, object?> { [field.Key] = field.Write(value) },
                InstanceIndex,
                GroupId));

    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    protected bool IsLoading => _loading;

    public override void OnPageShown()
    {
        base.OnPageShown();
        LoadScope();
    }

    protected T GetValue<T>(ZzzConfigField field)
    {
        if (_values.TryGetValue(field.Key, out object? value) && value is T typed)
        {
            return typed;
        }

        object? fallback = field.Read(field.DefaultValue);
        return fallback is T fallbackValue ? fallbackValue : default!;
    }

    protected bool SetValue<T>(
        ZzzConfigField field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        object? current = _values.GetValueOrDefault(field.Key, field.Read(field.DefaultValue));
        if (Equals(current, value))
        {
            return false;
        }

        _values[field.Key] = value;
        OnPropertyChanged(propertyName);
        if (!_loading)
        {
            SaveField(field, value);
        }

        return true;
    }

    protected bool SaveValue(ZzzConfigField field, object? value) => SaveField(field, value);

    protected bool ApplyScopeResult(
        ZzzBackendResult<ZzzConfigScopeValuesDto> result,
        string fallbackError)
    {
        if (!result.Success || result.Value is null)
        {
            ReportError(result.Error ?? fallbackError);
            return false;
        }

        try
        {
            ApplyScopeValues(result.Value);
            ReportError(null);
            return true;
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
            return false;
        }
    }

    protected void ApplyScopeValues(ZzzConfigScopeValuesDto values)
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            foreach (ZzzConfigField field in Fields)
            {
                object? raw = values.Values.TryGetValue(field.Key, out object? value)
                    ? value
                    : field.DefaultValue;
                SetLoadedValue(field, field.Read(raw));
            }

            OnScopeLoaded(values);
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void LoadScope()
    {
        try
        {
            ValidateFields();
            ZzzBackendResult<ZzzConfigScopeValuesDto> result =
                _backend.GetConfigScope(ScopeName, InstanceIndex, GroupId);
            ApplyScopeResult(result, $"{ScopeName} 配置读取失败。");
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
    }

    private bool SaveField(ZzzConfigField field, object? value)
    {
        try
        {
            ZzzBackendResult<ZzzConfigScopeValuesDto> result = SaveFieldCore(field, value);
            if (result.Success && result.Value is not null)
            {
                OnFieldSaved(field, result.Value);
            }

            ReportError(result.Success ? null : result.Error ?? $"{field.Key} 保存失败。");
            return result.Success;
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
            return false;
        }
    }

    private void SetLoadedValue(ZzzConfigField field, object? value)
    {
        object? current = _values.GetValueOrDefault(field.Key, field.Read(field.DefaultValue));
        if (Equals(current, value))
        {
            _values[field.Key] = value;
            return;
        }

        _values[field.Key] = value;
        OnPropertyChanged(field.PropertyName);
    }

    private void ValidateFields()
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (ZzzConfigField field in Fields)
        {
            if (!keys.Add(field.Key))
            {
                throw new InvalidOperationException($"配置字段重复: {ScopeName}.{field.Key}");
            }
        }
    }

    protected void ReportError(string? error)
    {
        LastError = error;
        try
        {
            _errorReporter?.Invoke(error);
        }
        catch
        {
            // 错误展示回调不能破坏页面生命周期或配置保存路径。
        }
    }
}
