using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 配置项描述。
/// </summary>
/// <param name="Key">YAML key。</param>
/// <param name="Title">显示名。</param>
/// <param name="ValueType">值类型。</param>
/// <param name="Writable">是否可写。</param>
/// <param name="DefaultValue">默认值。</param>
/// <param name="AllowedValues">允许值。</param>
public sealed record ZzzConfigSettingDescriptorDto(string Key, string Title, ZzzConfigValueType ValueType, bool Writable, object? DefaultValue, IReadOnlyList<object>? AllowedValues = null);
