using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手设置字段。
/// </summary>
public sealed record CommissionAssistantSettingField(string Key, string DisplayName, CommissionAssistantSettingType Type, object DefaultValue, string Description, IReadOnlyList<ConfigItem>? Options = null);
