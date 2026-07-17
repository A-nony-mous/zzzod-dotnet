using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地应用配置。
/// </summary>
public sealed class LostVoidConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "daily_plan_times", ApplyNamingConventions = false)]
	public int DailyPlanTimes { get; set; } = 5;

	[YamlMember(Alias = "weekly_plan_times", ApplyNamingConventions = false)]
	public int WeeklyPlanTimes { get; set; } = 2;

	[YamlMember(Alias = "extra_task", ApplyNamingConventions = false)]
	public string ExtraTask { get; set; } = "完成悬赏委托";

	[YamlMember(Alias = "mission_name", ApplyNamingConventions = false)]
	public string MissionName { get; set; } = "战线肃清";

	[YamlMember(Alias = "challenge_config", ApplyNamingConventions = false)]
	public string ChallengeConfig { get; set; } = "默认-成就模式";

	/// <summary>是否悬赏委托模式。</summary>
	[YamlIgnore]
	public bool IsBountyCommissionMode => string.Equals(ExtraTask, "完成悬赏委托", StringComparison.Ordinal);

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static LostVoidConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<LostVoidConfig> yamlConfig = new YamlConfig<LostVoidConfig>(environment, "lost_void", null, instanceIndex, new string[2] { "app_config", groupId });
		LostVoidConfig current = yamlConfig.Current;
		current.ConfigureRuntime("lost_void", instanceIndex, groupId);
		return current;
	}
}
