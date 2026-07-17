namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 停止运行结果。
/// </summary>
/// <param name="Stopped">是否成功发出停止信号。</param>
/// <param name="Source">被停止任务的来源。</param>
/// <param name="Error">错误信息。</param>
public sealed record StopRunResult(bool Stopped, string? Source = null, string? Error = null);
