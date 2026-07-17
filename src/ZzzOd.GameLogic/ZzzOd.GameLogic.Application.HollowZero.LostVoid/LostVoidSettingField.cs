using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地设置字段。
/// </summary>
public sealed record LostVoidSettingField(string Key, string DisplayName, LostVoidSettingType Type, object DefaultValue, IReadOnlyList<ConfigItem>? Options = null);
