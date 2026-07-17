using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 配置 scope 当前值。
/// </summary>
/// <param name="Descriptor">scope 描述。</param>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="GroupId">应用组编号。</param>
/// <param name="Values">当前值。</param>
public sealed record ZzzConfigScopeValuesDto(ZzzConfigScopeDescriptorDto Descriptor, int? InstanceIndex, string? GroupId, IReadOnlyDictionary<string, object?> Values);
