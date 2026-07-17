using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using OneDragon.Core.Ocr;
using ZzzOd.GameLogic.HollowZero;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public static class LostVoidPriorityUpdater
{
	public static string? ExtractPriorityCategoryFromText(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		Match match = PriorityCategoryRegex.Match(text);
		if (!match.Success)
		{
			return null;
		}
		string text2 = match.Groups[1].Value.Trim();
		return (text2.Length == 0) ? null : text2;
	}

	public static IReadOnlyList<string> ExtractDynamicPriorities(IReadOnlyList<LostVoidPriorityTextBlock> blocks)
	{
		List<LostVoidPriorityTextBlock> source = blocks.OrderBy((LostVoidPriorityTextBlock block) => block.Rect.Y1).ToList();
		List<LostVoidPriorityTextBlock> list = source.Where((LostVoidPriorityTextBlock block) => block.Text.Contains("等级1", StringComparison.Ordinal) || block.Text.Contains("等级2", StringComparison.Ordinal)).ToList();
		List<string> list2 = new List<string>();
		foreach (LostVoidPriorityTextBlock levelBlock in list)
		{
			string text = ExtractPriorityCategoryFromText((from block in source
				where (object)block != levelBlock
				where block.Rect.Y2 <= levelBlock.Rect.Y1
				where Math.Max(0, Math.Min(block.Rect.X2, levelBlock.Rect.X2) - Math.Max(block.Rect.X1, levelBlock.Rect.X1)) > 0
				select block).MinBy((LostVoidPriorityTextBlock block) => levelBlock.Rect.Y1 - block.Rect.Y2)?.Text);
			if (!string.IsNullOrWhiteSpace(text) && !list2.Contains<string>(text, StringComparer.Ordinal))
			{
				list2.Add(text);
			}
		}
		return list2;
	}

	public static void AppendDynamicPriorities(LostVoidContext context, IReadOnlyList<string> newPriorities)
	{
		foreach (string newPriority in newPriorities)
		{
			if (!context.DynamicPriorityList.Contains<string>(newPriority, StringComparer.Ordinal))
			{
				context.DynamicPriorityList.Add(newPriority);
			}
		}
	}

	public static IReadOnlyList<LostVoidPriorityTextBlock> FromOcrResults(IReadOnlyList<OcrMatchResult> results)
	{
		return results.Select((OcrMatchResult result) => new LostVoidPriorityTextBlock(result.Text, result.Rect)).ToArray();
	}

	/// <remarks>
	/// Pattern:<br />
	/// <code>[\\[【]\\s*([^:\\]】：]+)\\s*[:：]</code><br />
	/// Options:<br />
	/// <code>RegexOptions.Compiled</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match a character in the set [[\u3010].<br />
	/// ○ Match a whitespace character greedily any number of times.<br />
	/// ○ 1st capture group.<br />
	///     ○ Match a character in the set [^:]\u3011\uFF1A] greedily at least once.<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match a character in the set [:\uFF1A].<br />
	/// </code>
	/// </remarks>
	private static readonly Regex PriorityCategoryRegex = new("[\\[【]\\s*([^:\\]】：]+)\\s*[:：]", RegexOptions.Compiled);
}
