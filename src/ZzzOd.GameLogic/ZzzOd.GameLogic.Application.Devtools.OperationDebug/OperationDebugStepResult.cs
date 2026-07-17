namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试单步执行结果。
/// </summary>
public sealed record OperationDebugStepResult(bool IsSuccess, bool Completed, string? Status);
