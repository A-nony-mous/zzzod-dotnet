using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 真实战斗助手配置目录。
/// </summary>
/// <param name="AutoBattle">自动战斗配置名称。</param>
/// <param name="Dodge">闪避配置名称。</param>
public sealed record ZzzBattleAssistantConfigCatalogDto(IReadOnlyList<string> AutoBattle, IReadOnlyList<string> Dodge);
