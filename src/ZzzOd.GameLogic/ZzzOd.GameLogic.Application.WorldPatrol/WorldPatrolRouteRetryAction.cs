using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 卡住后的路线重试动作。
/// </summary>
public static class WorldPatrolRouteRetryAction
{
	/// <summary>再次卡住时跳过。</summary>
	public const string SkipOnStuckAgain = "skip_on_stuck_again";

	/// <summary>再次卡住时继续重试。</summary>
	public const string RetryOnStuckAgain = "retry_on_stuck_again";

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[2]
	{
		new ConfigItem("再次卡住时跳过", "skip_on_stuck_again"),
		new ConfigItem("再次卡住时重试", "retry_on_stuck_again")
	};
}
