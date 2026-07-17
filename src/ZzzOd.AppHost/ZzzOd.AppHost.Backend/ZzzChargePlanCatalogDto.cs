using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 体力计划手册目录。
/// </summary>
public sealed record ZzzChargePlanCatalogDto(IReadOnlyList<ZzzChargePlanCategoryDto> Categories, IReadOnlyList<ZzzChargePlanTeamDto> Teams, IReadOnlyList<string> AutoBattleConfigs);
