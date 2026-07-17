using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>运行记录。</summary>
public sealed record ZzzWorldPatrolRunRecordDto(int InstanceIndex, IReadOnlyList<string> Finished, int CompletedRounds, int RoutesPerRound);
