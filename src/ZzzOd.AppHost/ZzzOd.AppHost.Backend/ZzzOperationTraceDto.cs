using System;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// GUI 可消费的 Operation 节点流转事件。
/// </summary>
public sealed record ZzzOperationTraceDto(
	string AppId,
	int InstanceIndex,
	string Operation,
	string? CurrentNode,
	string? PreviousNode,
	string? NextNode,
	int RetryCount,
	string? ResultKind,
	string? Status,
	string? ExceptionType,
	string? ExceptionMessage,
	DateTimeOffset Timestamp);
