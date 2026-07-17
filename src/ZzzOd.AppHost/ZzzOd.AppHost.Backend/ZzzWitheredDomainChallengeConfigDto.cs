using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>枯萎之都挑战配置。</summary>
public sealed record ZzzWitheredDomainChallengeConfigDto(string ModuleName, bool IsSample, string AutoBattle, IReadOnlyList<string> ResoniumPriority, IReadOnlyList<string> EventPriority, IReadOnlyList<string?> TargetAgents, string PathFinding, IReadOnlyList<string> GoInOneStep, IReadOnlyList<string> Waypoint, IReadOnlyList<string> Avoid, bool BuyOnlyPriority, string? ValidationError = null);
