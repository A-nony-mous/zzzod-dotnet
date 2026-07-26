using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OneDragon.Core.Ocr;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidInteractService
{
	private sealed record LostVoidInteractCandidate(string MatchText, Func<LostVoidInteractTarget> ToTarget)
	{
		public static LostVoidInteractCandidate Entry(string text, string icon)
		{
			return new LostVoidInteractCandidate(text, () => new LostVoidInteractTarget(text, icon, isAgent: false, isNpc: false, isEntry: true));
		}

		public static LostVoidInteractCandidate Npc(string text)
		{
			return new LostVoidInteractCandidate(text, () => new LostVoidInteractTarget(text, "感叹号", isAgent: false, isNpc: true));
		}

		public static LostVoidInteractCandidate Agent(string text)
		{
			return new LostVoidInteractCandidate(text, () => new LostVoidInteractTarget(text, "感叹号", isAgent: true));
		}

		public static LostVoidInteractCandidate Boss(string text)
		{
			return new LostVoidInteractCandidate(text, () => new LostVoidInteractTarget(text, "战斗-终结之役", isAgent: false, isNpc: false, isEntry: true));
		}
	}

	private static readonly Regex ArtifactNameRegex = new Regex("\\[(.+?)\\](.+)$", RegexOptions.Compiled);

	private static readonly Regex ConfirmCountRegex = new Regex("(\\d+)\\s*/\\s*(\\d+)", RegexOptions.Compiled);

	public static LostVoidInteractService Instance { get; } = new LostVoidInteractService();

	private static IReadOnlyList<string> NpcNames { get; } = new string[6] { "玛琳", "奥菲莉亚", "蕾", "神出鬼没的研究员", "阿援", "乖仔" };

	private static IReadOnlyList<(string RuleId, string[] Phrases)> ExactTitleRules { get; } = new(string, string[])[8]
	{
		("GEAR_GAIN", new string[1] { "获得武备" }),
		("GEAR_UPGRADE", new string[1] { "武备已升级" }),
		("ARTIFACT_GAIN", new string[1] { "获得战利品" }),
		("GEAR_BRANCH", new string[1] { "请选择战术棱镜方案强化的方向" }),
		("CHOOSE_2", new string[3] { "请选择2项", "请选择2枚鸣徽", "请选择两枚鸣徽" }),
		("CHOOSE_1_GEAR", new string[1] { "请选择1个武备" }),
		("CHOOSE_1_CARD", new string[1] { "请选择1张卡牌" }),
		("CHOOSE_1", new string[3] { "请选择1项", "请选择1枚鸣徽", "请选择一枚鸣徽" })
	};

	private static IReadOnlyList<string> FuzzyTitlePhrases { get; } = ExactTitleRules.SelectMany<(string, string[]), string>(((string RuleId, string[] Phrases) item) => item.Phrases).ToArray();

	private static IReadOnlyDictionary<string, string> FuzzyTitlePhraseToRule { get; } = ExactTitleRules.SelectMany<(string, string[]), (string, string)>(((string RuleId, string[] Phrases) item) => item.Phrases.Select((string phrase) => (phrase: phrase, RuleId: item.RuleId))).ToDictionary<(string, string), string, string>(((string phrase, string RuleId) item) => item.phrase, ((string phrase, string RuleId) item) => item.RuleId, StringComparer.Ordinal);

	private LostVoidInteractService()
	{
	}

	public LostVoidInteractTarget? MatchInteractTarget(string? ocrText, Func<string, string>? gameTextResolver = null)
	{
		string text = (ocrText ?? string.Empty).Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal).Trim();
		if (text.Length == 0)
		{
			return null;
		}
		List<LostVoidInteractCandidate> list = new List<LostVoidInteractCandidate>();
		list.AddRange(LostVoidRegionType.All.Select((string region) => LostVoidInteractCandidate.Entry(region, region)));
		list.AddRange(NpcNames.Select((string name) => LostVoidInteractCandidate.Npc(name)));
		list.AddRange(AgentEnum.Values.Select((AgentEnum agent) => LostVoidInteractCandidate.Agent(agent.Value.AgentName)));
		list.AddRange(LostVoidBoss.All.Select((string name) => LostVoidInteractCandidate.Boss(name)));
		Func<string, string> resolve = gameTextResolver ?? ((Func<string, string>)((string result) => result));
		int? num = StringUtils.FindBestMatchByDifflib(text, list.Select((LostVoidInteractCandidate item) => resolve(item.MatchText)).ToArray());
		return ((!num.HasValue) ? null : list[num.Value])?.ToTarget();
	}

	public LostVoidChooseTitleState ParseChooseTitle(IEnumerable<string> titleWords, bool gearMarkerFound = false)
	{
		string normalized = StringUtils.RemoveWhitespace(string.Concat(titleWords)).Replace('（', '(').Replace('）', ')');
		foreach (var (ruleId, source) in ExactTitleRules)
		{
			if (source.Any((string phrase) => normalized.Contains(phrase, StringComparison.Ordinal)))
			{
				return ApplyTitleRule(ruleId);
			}
		}
		foreach (string item in from item in titleWords
			select StringUtils.RemoveWhitespace(item) into item
			where item.Length > 0
			select item)
		{
			int? num = StringUtils.FindBestMatchByDifflib(item, FuzzyTitlePhrases, 0.9);
			if (!num.HasValue)
			{
				continue;
			}
			return ApplyTitleRule(FuzzyTitlePhraseToRule[FuzzyTitlePhrases[num.Value]]);
		}
		return gearMarkerFound ? new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: true, ToChooseGearBranch: false, 1, "fallback:gear_marker") : new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: false, ToChooseGearBranch: false, 0, "fallback:none");
	}

	public int? ParseConfirmChosenCount(IEnumerable<string> texts, int? targetNum = null)
	{
		int? result = null;
		foreach (string text in texts)
		{
			string input = text.Trim().Replace('（', '(').Replace('）', ')');
			Match match = ConfirmCountRegex.Match(input);
			if (match.Success)
			{
				int num = int.Parse(match.Groups[1].Value);
				int num2 = int.Parse(match.Groups[2].Value);
				if (!targetNum.HasValue || num2 == targetNum.Value)
				{
					result = ((!result.HasValue) ? num : Math.Max(result.Value, num));
				}
			}
		}
		return result;
	}

	public IReadOnlyList<LostVoidArtifactPos> SortCandidates(IEnumerable<LostVoidArtifactPos> candidates)
	{
		return (from item in candidates
			orderby (!item.IsPrimaryName) ? 1 : 0, LevelRank(item.Artifact.Level), item.Rect.Center.X, item.Rect.Center.Y
			select item).ToArray();
	}

	public LostVoidArtifactNameResult BuildArtifactFromOcrName(string? ocrText)
	{
		string text = (ocrText ?? string.Empty).Trim().Replace('【', '[').Replace('】', ']');
		if (text.Length == 0)
		{
			return new LostVoidArtifactNameResult(null, IsPrimaryName: false);
		}
		Match match = ArtifactNameRegex.Match(text);
		if (!match.Success)
		{
			return new LostVoidArtifactNameResult(null, IsPrimaryName: false);
		}
		string text2 = match.Groups[1].Value.Trim().Replace("昇常", "异常", StringComparison.Ordinal);
		string text3 = match.Groups[2].Value.Trim();
		if (text3.Length == 0)
		{
			return new LostVoidArtifactNameResult(null, IsPrimaryName: false);
		}
		string text4 = text2.Split('：', ':')[0].Trim();
		if (text4.Length == 0)
		{
			text4 = text2;
		}
		return new LostVoidArtifactNameResult(new LostVoidArtifact
		{
			Category = text4,
			Name = text3,
			Level = "?",
			IsGear = true
		}, IsPrimaryName: true);
	}

	public IReadOnlyList<string> ExtractNamesFromStitchedOcr(IReadOnlyList<OcrMatchResult> ocrResults, int slotCount, int slotHeight)
	{
		List<(int, string)>[] array = (from _ in Enumerable.Range(0, slotCount)
			select new List<(int, string)>()).ToArray();
		foreach (OcrMatchResult ocrResult in ocrResults)
		{
			string text = ocrResult.Text.Trim();
			if (text.Length != 0)
			{
				int num = ocrResult.Center.Y / slotHeight;
				if (num >= 0 && num < slotCount)
				{
					array[num].Add((ocrResult.Center.X, text));
				}
			}
		}
		return array.Select((List<(int X, string Text)> slot) => string.Concat(from item in slot
			orderby item.X
			select item.Text).Trim()).ToArray();
	}

	public bool HasLotteryTimesLeft(IEnumerable<string> ocrTexts)
	{
		foreach (string ocrText in ocrTexts)
		{
			int? positiveDigits = StringUtils.GetPositiveDigits(ocrText, 0);
			if (positiveDigits.HasValue && positiveDigits.GetValueOrDefault() > 0)
			{
				return true;
			}
		}
		return false;
	}

	private static LostVoidChooseTitleState ApplyTitleRule(string ruleId)
	{
		if (1 == 0)
		{
		}
		LostVoidChooseTitleState result = ruleId switch
		{
			"GEAR_GAIN" => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: true, ToChooseGearBranch: false, 0, ruleId), 
			"GEAR_UPGRADE" => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: true, ToChooseGearBranch: false, 0, ruleId), 
			"ARTIFACT_GAIN" => new LostVoidChooseTitleState(ToChooseArtifact: true, ToChooseGear: false, ToChooseGearBranch: false, 0, ruleId), 
			"GEAR_BRANCH" => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: true, ToChooseGearBranch: true, 1, ruleId), 
			"CHOOSE_2" => new LostVoidChooseTitleState(ToChooseArtifact: true, ToChooseGear: false, ToChooseGearBranch: false, 2, ruleId), 
			"CHOOSE_1_GEAR" => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: true, ToChooseGearBranch: false, 1, ruleId), 
			"CHOOSE_1_CARD" => new LostVoidChooseTitleState(ToChooseArtifact: true, ToChooseGear: false, ToChooseGearBranch: false, 1, ruleId), 
			"CHOOSE_1" => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: false, ToChooseGearBranch: false, 1, ruleId), 
			_ => new LostVoidChooseTitleState(ToChooseArtifact: false, ToChooseGear: false, ToChooseGearBranch: false, 0, ruleId), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static int LevelRank(string? level)
	{
		if (1 == 0)
		{
		}
		int result = level switch
		{
			"S" => 0, 
			"A" => 1, 
			"B" => 2, 
			_ => 9, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
