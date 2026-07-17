using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地设置元数据。
/// </summary>
public static class WorldPatrolSettings
{
	/// <summary>BaselineParity 设置提供器类型。</summary>
	public const string SettingType = "INTERFACE";

	/// <summary>字段列表。</summary>
	public static IReadOnlyList<WorldPatrolSettingField> Fields { get; } = new WorldPatrolSettingField[8]
	{
		new WorldPatrolSettingField("auto_battle", "自动战斗配置", WorldPatrolSettingType.String, "全配队通用"),
		new WorldPatrolSettingField("route_list", "路线列表", WorldPatrolSettingType.String, string.Empty),
		new WorldPatrolSettingField("ui_disappear_action", "UI消失处理", WorldPatrolSettingType.Enum, "silent_fail", WorldPatrolUiDisappearAction.Options),
		new WorldPatrolSettingField("ui_disappear_seconds", "UI消失秒数", WorldPatrolSettingType.Integer, 10),
		new WorldPatrolSettingField("route_retry_times", "路线重试次数", WorldPatrolSettingType.Integer, 1),
		new WorldPatrolSettingField("route_retry_action", "路线重试处理", WorldPatrolSettingType.Enum, "skip_on_stuck_again", WorldPatrolRouteRetryAction.Options),
		new WorldPatrolSettingField("daily_loop_count", "每日循环轮数", WorldPatrolSettingType.Integer, 1),
		new WorldPatrolSettingField("loop_interval_seconds", "循环间隔秒数", WorldPatrolSettingType.Integer, 1800)
	};
}
