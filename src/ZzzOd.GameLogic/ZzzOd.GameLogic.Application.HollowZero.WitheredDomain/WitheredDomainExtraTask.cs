using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 额外刷取任务。
/// </summary>
public static class WitheredDomainExtraTask
{
	/// <summary>不进行。</summary>
	public const string None = "不进行";

	/// <summary>刷满业绩点。</summary>
	public const string EvaPoint = "刷满业绩点";

	/// <summary>刷满周期奖励。</summary>
	public const string PeriodReward = "刷满周期奖励";

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem("不进行", "不进行"),
		new ConfigItem("刷满业绩点", "刷满业绩点"),
		new ConfigItem("刷满周期奖励", "刷满周期奖励")
	};
}
