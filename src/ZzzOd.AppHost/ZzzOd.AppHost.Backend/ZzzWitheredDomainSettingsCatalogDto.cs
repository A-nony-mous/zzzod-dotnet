using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>枯萎之都设置页真实目录。</summary>
public sealed record ZzzWitheredDomainSettingsCatalogDto(IReadOnlyList<string> Missions, IReadOnlyList<ZzzWitheredDomainChallengeConfigDto> ChallengeConfigs, IReadOnlyList<string> AutoBattleConfigs, IReadOnlyList<ZzzWitheredDomainOptionDto> AgentOptions, IReadOnlyList<ZzzWitheredDomainOptionDto> PathFindingOptions, IReadOnlyList<string> DefaultGoInOneStep, IReadOnlyList<string> DefaultWaypoint, IReadOnlyList<string> DefaultAvoid, ZzzWitheredDomainRunRecordDto RunRecord, string NewModuleName);
