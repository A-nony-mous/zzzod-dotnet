using System.Globalization;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Services.Config;

public sealed class ZzzBackendConfigBinding<T> : IZzzConfigBinding<T>
{
    private readonly IZzzAppBackend _backend;
    private readonly string _scope;
    private readonly string _key;
    private readonly T _fallback;
    private readonly int? _instanceIndex;
    private readonly string? _groupId;

    public ZzzBackendConfigBinding(IZzzAppBackend backend, string scope, string key, T fallback, int? instanceIndex = null, string? groupId = null)
    {
        _backend = backend;
        _scope = scope;
        _key = key;
        _fallback = fallback;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public T Read()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(_scope, _instanceIndex, _groupId);
        if (!result.Success
            || result.Value is null
            || !result.Value.Values.TryGetValue(_key, out object? value))
        {
            return _fallback;
        }

        return ConvertValue(value, _fallback);
    }

    public void Save(T value)
    {
        _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            _scope,
            new Dictionary<string, object?> { [_key] = value },
            _instanceIndex,
            _groupId));
    }

    private static T ConvertValue(object? value, T fallback)
    {
        if (value is T typed)
        {
            return typed;
        }

        if (value is null)
        {
            return fallback;
        }

        try
        {
            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            object converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return (T)converted;
        }
        catch (InvalidCastException)
        {
            return fallback;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
