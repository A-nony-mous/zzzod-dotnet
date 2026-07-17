using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>挑战配置编辑器目录。</summary>
/// <param name="Configs">配置文件目录。</param>
/// <param name="Teams">预备编队目录。</param>
/// <param name="AutoBattleConfigs">自动战斗配置目录。</param>
/// <param name="Agents">代理人目录。</param>
/// <param name="InvestigationStrategies">调查战略目录。</param>
public sealed record ZzzLostVoidChallengeCatalogDto(IReadOnlyList<ZzzLostVoidChallengeSummaryDto> Configs, IReadOnlyList<ZzzLostVoidTeamDto> Teams, IReadOnlyList<string> AutoBattleConfigs, IReadOnlyList<ZzzLostVoidOptionDto> Agents, IReadOnlyList<string> InvestigationStrategies);
