using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>迷失之地挑战配置。</summary>
/// <param name="ModuleName">配置名称。</param>
/// <param name="IsSample">是否只读 sample。</param>
/// <param name="Exists">用户文件是否已存在。</param>
/// <param name="PredefinedTeamIndex">预备编队下标。</param>
/// <param name="ChooseTeamByPriority">是否优先当期 UP。</param>
/// <param name="ManuallyChooseAgent">是否手动选择代理人。</param>
/// <param name="TeamInfo">手动代理人 id。</param>
/// <param name="AutoBattle">自动战斗配置。</param>
/// <param name="ChaseNewMode">是否追新。</param>
/// <param name="InvestigationStrategy">调查战略。</param>
/// <param name="PeriodBuffNo">周期增益位置。</param>
/// <param name="StoreGold">是否使用金币购买。</param>
/// <param name="StoreBlood">是否使用血量购买。</param>
/// <param name="StoreBloodMin">购买最低血量。</param>
/// <param name="ArtifactPriorityNew">是否优先 NEW 藏品。</param>
/// <param name="BuyOnlyPriority1">只购买第一优先级的刷新次数。</param>
/// <param name="BuyOnlyPriority2">只购买第二优先级的刷新次数。</param>
/// <param name="ArtifactPriority">藏品第一优先级。</param>
/// <param name="ArtifactPriority2">藏品第二优先级。</param>
/// <param name="RegionTypePriority">区域类型优先级。</param>
public sealed record ZzzLostVoidChallengeConfigDto(string ModuleName, bool IsSample, bool Exists, int PredefinedTeamIndex, bool ChooseTeamByPriority, bool ManuallyChooseAgent, IReadOnlyList<string> TeamInfo, string AutoBattle, bool ChaseNewMode, string InvestigationStrategy, string PeriodBuffNo, bool StoreGold, bool StoreBlood, int StoreBloodMin, bool ArtifactPriorityNew, int BuyOnlyPriority1, int BuyOnlyPriority2, IReadOnlyList<string> ArtifactPriority, IReadOnlyList<string> ArtifactPriority2, IReadOnlyList<string> RegionTypePriority);
