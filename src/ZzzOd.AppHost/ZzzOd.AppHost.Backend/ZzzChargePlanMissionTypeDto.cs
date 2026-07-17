using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 体力计划任务类型。
/// </summary>
public sealed record ZzzChargePlanMissionTypeDto(string Label, string Value, IReadOnlyList<ZzzChargePlanMissionDto> Missions);
