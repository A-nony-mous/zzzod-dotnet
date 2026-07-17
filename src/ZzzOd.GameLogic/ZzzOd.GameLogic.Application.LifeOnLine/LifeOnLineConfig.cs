using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线应用配置。
/// </summary>
public sealed class LifeOnLineConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "daily_plan_times", ApplyNamingConventions = false)]
	public int DailyPlanTimes { get; set; } = 20;

	[YamlMember(Alias = "predefined_team_idx", ApplyNamingConventions = false)]
	public int PredefinedTeamIndex { get; set; } = -1;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static LifeOnLineConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<LifeOnLineConfig> yamlConfig = new YamlConfig<LifeOnLineConfig>(environment, "life_on_line", null, instanceIndex, new string[2] { "app_config", groupId });
		LifeOnLineConfig current = yamlConfig.Current;
		current.ConfigureRuntime("life_on_line", instanceIndex, groupId);
		return current;
	}
}
