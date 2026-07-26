using System.Collections.Immutable;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// 同一时刻采集的 Overlay 运行期数据快照。
/// </summary>
public sealed record ZzzOverlaySnapshotDto(
	DateTimeOffset Timestamp,
	bool Enabled,
	ZzzOverlayFrameDto? VisionFrame,
	ZzzOverlayRunStateDto? State,
	ImmutableArray<ZzzOverlayOperationDto> Operations,
	ImmutableArray<ZzzOverlayDecisionDto> Decisions,
	ImmutableArray<ZzzOverlayTimelineItemDto> Timeline,
	ImmutableArray<ZzzOverlayPerformanceSampleDto> Performance,
	ImmutableArray<ZzzOverlayLogEntryDto> Logs)
{
	/// <summary>
	/// 根据旧接口结果创建兼容快照。
	/// </summary>
	public static ZzzOverlaySnapshotDto FromLegacy(
		ZzzOverlayStatusDto status,
		ZzzOverlayFrameDto? frame,
		IReadOnlyList<ZzzOverlayPerformanceSampleDto> performance)
	{
		ArgumentNullException.ThrowIfNull(status);
		ArgumentNullException.ThrowIfNull(performance);
		ZzzOverlayFrameDto? zzzOverlayFrameDto = frame is null
			? null
			: new ZzzOverlayFrameDto(frame.Timestamp, frame.Items.ToImmutableArray());
		return new ZzzOverlaySnapshotDto(
			DateTimeOffset.UtcNow,
			status.Enabled,
			zzzOverlayFrameDto,
			null,
			ImmutableArray<ZzzOverlayOperationDto>.Empty,
			ImmutableArray<ZzzOverlayDecisionDto>.Empty,
			ImmutableArray<ZzzOverlayTimelineItemDto>.Empty,
			performance.ToImmutableArray(),
			ImmutableArray<ZzzOverlayLogEntryDto>.Empty);
	}
}

/// <summary>
/// Overlay 状态面板的数据。
/// </summary>
public sealed record ZzzOverlayRunStateDto(
	string RunState,
	string? CurrentAppId,
	string? CurrentApp,
	string? CurrentNode,
	string? PreviousNode,
	int? NodeRetry,
	string? CurrentGroupId,
	int? CurrentInstanceIndex,
	DateTimeOffset UpdatedAt,
	ZzzOverlayAutoBattleStateDto? AutoBattle = null);

/// <summary>
/// 自动战斗状态面板的数据。
/// </summary>
public sealed record ZzzOverlayAutoBattleStateDto(
	bool IsRunning,
	string? FrontAgentName,
	bool? FrontSpecialReady,
	bool? FrontUltimateReady,
	string? LatestDodgeState,
	bool? ChainReady,
	string? LatestQuickAssistAgent,
	double? DistanceMeters,
	string? CurrentTrigger = null,
	string? CurrentExpression = null,
	double? CurrentDurationSeconds = null,
	IReadOnlyList<ZzzOverlayBattleStateRowDto>? StateRows = null);

/// <summary>
/// battle 面板的单条状态行。
/// </summary>
/// <param name="StateName">状态名。</param>
/// <param name="SecondsSinceTrigger">距上次触发的秒数。</param>
/// <param name="Value">状态值。</param>
public sealed record ZzzOverlayBattleStateRowDto(
	string StateName,
	double SecondsSinceTrigger,
	int? Value);

/// <summary>
/// Operation 轨迹的数据传输对象。
/// </summary>
public sealed record ZzzOverlayOperationDto(
	string AppId,
	string Operation,
	string? CurrentNode,
	string? PreviousNode,
	string? NextNode,
	int RetryCount,
	string? ResultKind,
	string? Status,
	DateTimeOffset CreatedAt);

/// <summary>
/// 决策轨迹的数据传输对象。
/// </summary>
public sealed record ZzzOverlayDecisionDto(
	string Source,
	string Trigger,
	string Expression,
	string Operation,
	string Status,
	DateTimeOffset CreatedAt,
	ImmutableDictionary<string, string> Metadata);

/// <summary>
/// 时间轴项的数据传输对象。
/// </summary>
public sealed record ZzzOverlayTimelineItemDto(
	string Category,
	string Title,
	string Detail,
	string Level,
	DateTimeOffset CreatedAt,
	ImmutableDictionary<string, string> Metadata);

/// <summary>
/// 日志面板项的数据传输对象。
/// </summary>
public sealed record ZzzOverlayLogEntryDto(
	DateTimeOffset Timestamp,
	string Level,
	string Category,
	string Message,
	string? Exception);
