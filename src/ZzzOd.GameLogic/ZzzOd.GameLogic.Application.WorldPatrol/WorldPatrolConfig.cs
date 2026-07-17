using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地配置。
/// </summary>
public sealed class WorldPatrolConfig : ZApplicationConfig, IApplicationConfig
{
	private int _uiDisappearSeconds = 10;

	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; } = "全配队通用";

	[YamlMember(Alias = "route_list", ApplyNamingConventions = false)]
	public string RouteList { get; set; } = string.Empty;

	[YamlMember(Alias = "ui_disappear_action", ApplyNamingConventions = false)]
	public string UiDisappearAction { get; set; } = "silent_fail";

	[YamlMember(Alias = "ui_disappear_seconds", ApplyNamingConventions = false)]
	public int UiDisappearSeconds
	{
		get
		{
			return _uiDisappearSeconds;
		}
		set
		{
			_uiDisappearSeconds = Math.Min(value, 999);
		}
	}

	[YamlMember(Alias = "route_retry_times", ApplyNamingConventions = false)]
	public int RouteRetryTimes { get; set; } = 1;

	[YamlMember(Alias = "route_retry_action", ApplyNamingConventions = false)]
	public string RouteRetryAction { get; set; } = "skip_on_stuck_again";

	[YamlMember(Alias = "daily_loop_count", ApplyNamingConventions = false)]
	public int DailyLoopCount { get; set; } = 1;

	[YamlMember(Alias = "loop_interval_seconds", ApplyNamingConventions = false)]
	public int LoopIntervalSeconds { get; set; } = 1800;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static WorldPatrolConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<WorldPatrolConfig> yamlConfig = new YamlConfig<WorldPatrolConfig>(environment, "world_patrol", null, instanceIndex, new string[2] { "app_config", groupId });
		WorldPatrolConfig current = yamlConfig.Current;
		current.ConfigureRuntime("world_patrol", instanceIndex, groupId);
		return current;
	}
}
