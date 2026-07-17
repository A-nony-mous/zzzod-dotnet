namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 实例摘要。
/// </summary>
/// <param name="Index">实例编号。</param>
/// <param name="Name">实例显示名称。</param>
/// <param name="Active">是否当前实例。</param>
/// <param name="ConfigDirectory">实例配置目录。</param>
/// <param name="ActiveInOneDragon">是否参与一条龙。</param>
public sealed record ZzzInstanceDto(int Index, string Name, bool Active, string ConfigDirectory, bool ActiveInOneDragon = true);
