using System.Globalization;

namespace ZzzOd.Gui.Services.Config;

internal sealed record ZzzConfigField(
    string Key,
    Type ClrType,
    object? DefaultValue,
    Func<object?, object?>? FromConfig = null,
    Func<object?, object?>? ToConfig = null)
{
    public string PropertyName { get; } = ToPropertyName(Key);

    public object? Read(object? value)
    {
        object? converted = FromConfig is null ? value : FromConfig(value);
        return ConvertToClrType(converted, DefaultValue);
    }

    public object? Write(object? value) => ToConfig is null ? value : ToConfig(value);

    private object? ConvertToClrType(object? value, object? fallback)
    {
        if (value is null)
        {
            return AllowsNull(ClrType) ? null : fallback;
        }

        Type targetType = Nullable.GetUnderlyingType(ClrType) ?? ClrType;
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            if (targetType.IsEnum)
            {
                return value is string text
                    ? Enum.Parse(targetType, text, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            return fallback;
        }
    }

    private static bool AllowsNull(Type type) =>
        !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static string ToPropertyName(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string[] parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
