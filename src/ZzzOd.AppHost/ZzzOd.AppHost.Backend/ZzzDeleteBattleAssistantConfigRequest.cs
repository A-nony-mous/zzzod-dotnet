namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 删除战斗助手配置请求。
/// </summary>
/// <param name="Kind">目录类型。</param>
/// <param name="Name">配置名称。</param>
public sealed record ZzzDeleteBattleAssistantConfigRequest(ZzzBattleAssistantConfigKind Kind, string Name);
