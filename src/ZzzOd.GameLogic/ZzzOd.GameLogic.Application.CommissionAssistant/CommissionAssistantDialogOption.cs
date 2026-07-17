using System.Collections.Generic;
using OneDragon.Core.Configuration;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 对话选项选择策略。
/// </summary>
public sealed record CommissionAssistantDialogOption(string Value)
{
	/// <summary>第一个选项。</summary>
	public static CommissionAssistantDialogOption First { get; } = new CommissionAssistantDialogOption("第一个");

	/// <summary>最后一个选项。</summary>
	public static CommissionAssistantDialogOption Last { get; } = new CommissionAssistantDialogOption("最后一个");

	/// <summary>可选项。</summary>
	public static IReadOnlyList<ConfigItem> Options { get; } = new ConfigItem[2]
	{
		new ConfigItem(First.Value),
		new ConfigItem(Last.Value)
	};
}
