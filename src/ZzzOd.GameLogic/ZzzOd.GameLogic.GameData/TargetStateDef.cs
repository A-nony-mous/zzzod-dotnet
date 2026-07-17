using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

public sealed class TargetStateDef
{
	public required string StateName { get; init; }

	public required TargetCheckWay CheckWay { get; init; }

	public IReadOnlyDictionary<string, object> CheckParams { get; init; } = new Dictionary<string, object>();

	public bool ClearOnMiss { get; init; }
}
