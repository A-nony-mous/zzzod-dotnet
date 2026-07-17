using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地设置字段。
/// </summary>
public sealed record WorldPatrolSettingField(string Key, string DisplayName, WorldPatrolSettingType Type, object DefaultValue, IReadOnlyList<ConfigItem>? Options = null);
