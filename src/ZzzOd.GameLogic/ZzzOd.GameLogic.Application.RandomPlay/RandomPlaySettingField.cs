using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业设置字段。
/// </summary>
public sealed record RandomPlaySettingField(string Key, string DisplayName, RandomPlaySettingType Type, object DefaultValue, string Description, IReadOnlyList<ConfigItem>? Options = null);
