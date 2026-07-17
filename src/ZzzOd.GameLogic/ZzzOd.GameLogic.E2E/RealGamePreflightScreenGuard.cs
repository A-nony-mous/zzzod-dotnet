using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// Guards real-game preflight from advancing unsafe account-state screens.
/// </summary>
public static class RealGamePreflightScreenGuard
{
	/// <summary>
	/// Evaluates a preflight screen snapshot.
	/// </summary>
	public static RealGamePreflightGuardResult Evaluate(RealGamePreflightScreenState state)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (!string.IsNullOrWhiteSpace(state.WorldScreenName))
		{
			return RealGamePreflightGuardResult.Allow();
		}
		if (!string.IsNullOrWhiteSpace(state.ActiveScreenName))
		{
			return RealGamePreflightGuardResult.Allow();
		}
		IReadOnlyList<string> readOnlyList = state.OcrTexts.Where((string value) => !string.IsNullOrWhiteSpace(value)).ToArray();
		if (IsIntelBoardPage(readOnlyList))
		{
			return RealGamePreflightGuardResult.Allow();
		}
		bool flag = readOnlyList.Any((string a) => string.Equals(a, "安东", StringComparison.Ordinal));
		bool flag2 = readOnlyList.Any((string a) => string.Equals(a, "绳匠", StringComparison.Ordinal));
		bool flag3 = readOnlyList.Any(delegate(string text3)
		{
			int length = text3.Length;
			return length > 0 && length <= 4 && !string.Equals(text3, "绳匠", StringComparison.Ordinal);
		});
		bool flag4 = readOnlyList.Any(IsDialogueLine);
		if (flag || (flag2 && flag3) || (flag3 && flag4))
		{
			string text = string.Join(", ", readOnlyList.Where((string text3) => string.Equals(text3, "安东", StringComparison.Ordinal) || string.Equals(text3, "绳匠", StringComparison.Ordinal) || IsDialogueLine(text3)).Take(6));
			string text2 = string.Join(", ", readOnlyList.Take(12));
			return RealGamePreflightGuardResult.Block("real-game preflight blocked by suspected NPC dialogue OCR; matched=" + text + "; preview=" + text2);
		}
		return RealGamePreflightGuardResult.Allow();
	}

	private static bool IsDialogueLine(string text)
	{
		return text.Length >= 8 && (text.Contains('！', StringComparison.Ordinal) || text.Contains('？', StringComparison.Ordinal) || text.Contains('。', StringComparison.Ordinal) || text.Contains('，', StringComparison.Ordinal));
	}

	private static bool IsIntelBoardPage(IReadOnlyList<string> ocrTexts)
	{
		bool flag = ocrTexts.Any((string text) => text.Contains("接取委托", StringComparison.Ordinal));
		bool flag2 = ocrTexts.Any((string text) => text.Contains("可接取", StringComparison.Ordinal));
		bool flag3 = ocrTexts.Any((string text) => text.Contains("达成委托", StringComparison.Ordinal));
		bool flag4 = ocrTexts.Any((string text) => text.Contains("委托管理", StringComparison.Ordinal));
		bool flag5 = ocrTexts.Any((string text) => text.Contains("周期内可获取", StringComparison.Ordinal));
		bool flag6 = ocrTexts.Any((string text) => text.Contains("委托进行中", StringComparison.Ordinal));
		bool flag7 = ocrTexts.Any((string text) => string.Equals(text, "前往", StringComparison.Ordinal) || string.Equals(text, "放弃委托", StringComparison.Ordinal));
		return (flag && (flag2 || flag3)) || (flag4 && flag5) || (flag6 && flag7);
	}
}
