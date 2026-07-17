using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 剧情处理策略。
/// </summary>
public sealed record CommissionAssistantStoryMode(string Value)
{
	/// <summary>自动点击。</summary>
	public static CommissionAssistantStoryMode Click { get; } = new CommissionAssistantStoryMode("自动点击");

	/// <summary>等待剧情自动播放。</summary>
	public static CommissionAssistantStoryMode Auto { get; } = new CommissionAssistantStoryMode("等待剧情自动播放");

	/// <summary>跳过剧情。</summary>
	public static CommissionAssistantStoryMode Skip { get; } = new CommissionAssistantStoryMode("跳过剧情");

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[3]
	{
		new ConfigItem(Click.Value),
		new ConfigItem(Auto.Value),
		new ConfigItem(Skip.Value)
	};
}
