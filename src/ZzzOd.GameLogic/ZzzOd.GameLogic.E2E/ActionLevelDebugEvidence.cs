using System;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 单动作实机调试 evidence。
/// </summary>
public sealed class ActionLevelDebugEvidence
{
	/// <summary>文件名前缀。</summary>
	public string FileStem { get; set; } = string.Empty;

	/// <summary>应用 id。</summary>
	public string AppId { get; set; } = string.Empty;

	/// <summary>操作名称。</summary>
	public string OperationName { get; set; } = string.Empty;

	/// <summary>节点名称。</summary>
	public string NodeName { get; set; } = string.Empty;

	/// <summary>.NET 方法名。</summary>
	public string DotNetMethod { get; set; } = string.Empty;

	/// <summary>基准业务一致性要求。</summary>
	public string BaselineParityRequirement { get; set; } = string.Empty;

	/// <summary>动作前截图路径。</summary>
	public string? BeforeScreenshotPath { get; set; }

	/// <summary>动作前识别摘要。</summary>
	public object? BeforeRecognitionSummary { get; set; }

	/// <summary>动作类型。</summary>
	public string ActionKind { get; set; } = string.Empty;

	/// <summary>动作目标。</summary>
	public string ActionTarget { get; set; } = string.Empty;

	/// <summary>动作目标明细。</summary>
	public object? ActionTargetDetails { get; set; }

	/// <summary>预期下一状态。</summary>
	public string ExpectedNextState { get; set; } = string.Empty;

	/// <summary>动作后截图路径。</summary>
	public string? AfterScreenshotPath { get; set; }

	/// <summary>动作后识别摘要。</summary>
	public object? AfterRecognitionSummary { get; set; }

	/// <summary>状态转换结果。</summary>
	public string TransitionResult { get; set; } = string.Empty;

	/// <summary>失败原因。</summary>
	public string? FailureReason { get; set; }

	/// <summary>是否因疑似循环停止重试。</summary>
	public bool RetryStoppedBecauseOfSuspectedLoop { get; set; }

	/// <summary>fallback 原因。</summary>
	public string? FallbackReason { get; set; }

	/// <summary>创建时间。</summary>
	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
