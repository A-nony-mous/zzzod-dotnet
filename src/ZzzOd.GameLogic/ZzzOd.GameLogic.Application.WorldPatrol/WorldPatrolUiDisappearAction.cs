using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// UI 消失后的处理动作。
/// </summary>
public static class WorldPatrolUiDisappearAction
{
	/// <summary>静默失败。</summary>
	public const string SilentFail = "silent_fail";

	/// <summary>重启并跳过路线。</summary>
	public const string RestartAndSkip = "restart_and_skip";

	/// <summary>重启并重试路线。</summary>
	public const string RestartAndRetry = "restart_and_retry";

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("静默失败", "silent_fail"),
		new ConfigItem("重启并跳过", "restart_and_skip"),
		new ConfigItem("重启并重试", "restart_and_retry")
	};
}
