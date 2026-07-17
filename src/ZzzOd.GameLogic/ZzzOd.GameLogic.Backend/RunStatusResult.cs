namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 当前运行状态。
/// </summary>
/// <param name="State">状态值。</param>
/// <param name="Source">来源。</param>
/// <param name="App">应用或操作名。</param>
/// <param name="StartedAt">开始时间。</param>
/// <param name="DurationSeconds">运行秒数。</param>
/// <param name="CurrentNode">当前节点。</param>
/// <param name="RetryCount">重试次数。</param>
/// <param name="LastStatus">最后状态文本。</param>
/// <param name="FailedNode">失败节点。</param>
public sealed record RunStatusResult(string State, string? Source = null, string? App = null, string? StartedAt = null, double? DurationSeconds = null, string? CurrentNode = null, int? RetryCount = null, string? LastStatus = null, string? FailedNode = null);
