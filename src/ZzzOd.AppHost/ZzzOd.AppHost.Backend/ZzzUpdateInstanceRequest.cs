namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 实例元数据更新请求。
/// </summary>
/// <param name="Index">实例编号。</param>
/// <param name="Name">实例显示名。</param>
/// <param name="ActiveInOneDragon">是否参与一条龙。</param>
public sealed record ZzzUpdateInstanceRequest(int Index, string? Name = null, bool? ActiveInOneDragon = null);
