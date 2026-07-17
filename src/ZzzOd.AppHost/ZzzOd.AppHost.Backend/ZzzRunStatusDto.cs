namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 运行状态。
/// </summary>
/// <param name="State">状态。</param>
/// <param name="AppId">应用编号。</param>
/// <param name="AppName">应用名称。</param>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="GroupId">应用组编号。</param>
/// <param name="StartedAt">开始时间。</param>
/// <param name="FinishedAt">结束时间。</param>
/// <param name="DurationSeconds">运行秒数。</param>
/// <param name="LastStatus">最后状态文本。</param>
/// <param name="Error">错误文本。</param>
public sealed record ZzzRunStatusDto(ZzzRunState State, string? AppId = null, string? AppName = null, int? InstanceIndex = null, string? GroupId = null, string? StartedAt = null, string? FinishedAt = null, double? DurationSeconds = null, string? LastStatus = null, string? Error = null);
