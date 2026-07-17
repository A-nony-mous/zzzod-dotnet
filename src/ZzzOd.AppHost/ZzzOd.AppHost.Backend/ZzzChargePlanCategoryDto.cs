using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 体力计划分类。
/// </summary>
public sealed record ZzzChargePlanCategoryDto(string Label, string Value, IReadOnlyList<ZzzChargePlanMissionTypeDto> MissionTypes);
