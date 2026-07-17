using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都设置字段。
/// </summary>
public sealed record WitheredDomainSettingField(string Key, string DisplayName, WitheredDomainSettingType Type, object DefaultValue, IReadOnlyList<ConfigItem>? Options = null);
