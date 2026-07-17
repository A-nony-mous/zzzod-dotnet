using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像带主题。
/// </summary>
public static class RandomPlayVideoThemes
{
	/// <summary>BaselineParity 侧主题顺序。</summary>
	public static IReadOnlyList<string> All { get; } = new string[16]
	{
		"纪实", "怀旧", "冒险", "幻想", "喜剧", "动作", "惊悚", "悬疑", "访谈", "都市",
		"时尚", "灾难", "悲剧", "亲情", "广告", "爱情"
	};
}
