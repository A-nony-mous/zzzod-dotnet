using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观设置字段。
/// </summary>
public sealed record SuibianTempleSettingField(string Key, string DisplayName, SuibianTempleSettingType Type, object DefaultValue, IReadOnlyList<ConfigItem>? Options = null);
