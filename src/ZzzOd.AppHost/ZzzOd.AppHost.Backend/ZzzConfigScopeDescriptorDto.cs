using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 配置 scope 描述。
/// </summary>
/// <param name="Scope">scope 名称。</param>
/// <param name="Title">显示名。</param>
/// <param name="InstanceBound">是否绑定实例。</param>
/// <param name="GroupBound">是否绑定应用组。</param>
/// <param name="Writable">是否可写。</param>
/// <param name="Settings">配置项。</param>
public sealed record ZzzConfigScopeDescriptorDto(string Scope, string Title, bool InstanceBound, bool GroupBound, bool Writable, IReadOnlyList<ZzzConfigSettingDescriptorDto> Settings);
