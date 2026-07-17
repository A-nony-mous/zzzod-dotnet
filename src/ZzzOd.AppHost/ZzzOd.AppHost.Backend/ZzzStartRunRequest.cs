namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 启动运行请求。
/// </summary>
/// <param name="AppId">应用编号。</param>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="GroupId">应用组编号。</param>
public sealed record ZzzStartRunRequest(string AppId, int? InstanceIndex = null, string? GroupId = null);
