using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>保存枯萎之都挑战配置请求。</summary>
public sealed record ZzzSaveWitheredDomainChallengeConfigRequest(string? OriginalModuleName, string ModuleName, string AutoBattle, string ResoniumPriorityText, string EventPriorityText, IReadOnlyList<string?> TargetAgents, string PathFinding, string GoInOneStepText, string WaypointText, string AvoidText, bool BuyOnlyPriority);
