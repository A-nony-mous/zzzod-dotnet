using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都配置。
/// </summary>
public sealed class WitheredDomainConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "mission_name", ApplyNamingConventions = false)]
	public string MissionName { get; set; } = "旧都列车-内部";

	[YamlMember(Alias = "challenge_config", ApplyNamingConventions = false)]
	public string ChallengeConfig { get; set; } = "默认-专属空洞-艾莲";

	[YamlMember(Alias = "weekly_plan_times", ApplyNamingConventions = false)]
	public int WeeklyPlanTimes { get; set; } = 2;

	[YamlMember(Alias = "daily_plan_times", ApplyNamingConventions = false)]
	public int DailyPlanTimes { get; set; } = 99;

	[YamlMember(Alias = "extra_task", ApplyNamingConventions = false)]
	public string ExtraTask { get; set; } = "刷满周期奖励";

	[YamlMember(Alias = "extra_exit", ApplyNamingConventions = false)]
	public string ExtraExit { get; set; } = "通关";

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static WitheredDomainConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<WitheredDomainConfig> yamlConfig = new YamlConfig<WitheredDomainConfig>(environment, "withered_domain", null, instanceIndex, new string[2] { "app_config", groupId });
		WitheredDomainConfig current = yamlConfig.Current;
		current.ConfigureRuntime("withered_domain", instanceIndex, groupId);
		return current;
	}
}
