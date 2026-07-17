using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 预备编队代理人头像模板匹配。
/// </summary>
public static class AgentTemplateMatcher
{
	private const string TemplateSubDir = "predefined_team";

	private const double KnnDistancePercent = 0.5;

	/// <summary>
	/// 在指定区域内匹配队伍代理人头像模板。
	/// </summary>
	public static IReadOnlyList<MatchResult> MatchTeamAgentTemplate(ZContext context, Mat screen, OneDragon.Core.Abstractions.Geometry.Rect rect, IReadOnlyCollection<string>? agentIdFilter = null)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		ArgumentNullException.ThrowIfNull(screen, "screen");
		HashSet<string> hashSet = agentIdFilter?.ToHashSet<string>(StringComparer.Ordinal);
		using Mat source = CvImageUtils.Crop(screen, rect);
		List<MatchResult> list = new List<MatchResult>();
		foreach (AgentEnum value2 in AgentEnum.Values)
		{
			Agent value = value2.Value;
			if (hashSet != null && !hashSet.Contains(value.AgentId))
			{
				continue;
			}
			foreach (string templateId in value.TemplateIdList)
			{
				MatchResult matchResult = context.TemplateMatcher.MatchOneByFeature(source, "predefined_team", "avatar_" + templateId, null, 0.5);
				if (matchResult != null)
				{
					list.Add(new MatchResult(matchResult.Confidence, matchResult.X + rect.X1, matchResult.Y + rect.Y1, matchResult.Width, matchResult.Height, matchResult.TemplateScale, value));
				}
			}
		}
		return list;
	}
}
