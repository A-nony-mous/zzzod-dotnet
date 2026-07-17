using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

public sealed class DetectionTask
{
	public required string TaskId { get; init; }

	public required string PipelineName { get; init; }

	public IReadOnlyList<TargetStateDef> StateDefinitions { get; init; } = Array.Empty<TargetStateDef>();

	public bool Enabled { get; init; } = true;

	public double Interval { get; init; } = 1.0;

	public bool IsAsync { get; init; }

	public IReadOnlyDictionary<string, object> DynamicIntervalConfig { get; init; } = new Dictionary<string, object>();
}
