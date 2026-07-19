using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.HollowZero;

public class LostVoidContext
{
	private static readonly Regex BracketArtifactNameRegex = new Regex("^\\[(.+?)\\](.+)$", RegexOptions.Compiled);

	private static readonly Regex CardArtifactNameRegex = new Regex("^「(.+?)」\\s*(.+)$", RegexOptions.Compiled);

	private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

	private readonly ZContext _ctx;

	public HollowLevelInfo LevelInfo { get; }

	public string ChallengeConfigName { get; private set; } = "默认-成就模式";

	public LostVoidChallengeConfig? ChallengeConfig { get; private set; }

	public LostVoidDetector? Detector { get; private set; }

	public IReadOnlyList<LostVoidArtifact> AllArtifactList { get; private set; } = Array.Empty<LostVoidArtifact>();

	public IReadOnlyDictionary<string, LostVoidArtifact> GearByName { get; private set; } = new Dictionary<string, LostVoidArtifact>(StringComparer.Ordinal);

	public IReadOnlyDictionary<string, IReadOnlyList<LostVoidArtifact>> CategoryToArtifacts { get; private set; } = new Dictionary<string, IReadOnlyList<LostVoidArtifact>>(StringComparer.Ordinal);

	public IReadOnlyList<LostVoidInvestigationStrategy> InvestigationStrategyList { get; private set; } = Array.Empty<LostVoidInvestigationStrategy>();

	public int PredefinedTeamIdx { get; set; } = -1;

	public bool PriorityUpdated { get; set; }

	public List<string> DynamicPriorityList { get; } = new List<string>();

	public List<string> DynamicAbandonList { get; } = new List<string>();

	public bool HadInteractedOpheliaOnCurrentLevel { get; set; }

	public bool AfterAppShutdownCalled { get; private set; }

	public LostVoidContext(ZContext ctx)
	{
		_ctx = ctx;
		LevelInfo = new HollowLevelInfo();
	}

	public void InitBeforeRun(string? challengeConfigName = null)
	{
		ChallengeConfigName = (string.IsNullOrWhiteSpace(challengeConfigName) ? "默认-成就模式" : challengeConfigName);
		PredefinedTeamIdx = -1;
		PriorityUpdated = false;
		DynamicPriorityList.Clear();
		DynamicAbandonList.Clear();
		HadInteractedOpheliaOnCurrentLevel = false;
		InitLostVoidDetectorModel();
		LoadArtifactData();
		LoadChallengeConfig();
		LoadInvestigationStrategy();
	}

	public string GetAutoOpName()
	{
		if (PredefinedTeamIdx >= 0 && PredefinedTeamIdx < _ctx.TeamConfig.TeamList.Count)
		{
			string autoBattle = _ctx.TeamConfig.TeamList[PredefinedTeamIdx].AutoBattle;
			if (!string.IsNullOrWhiteSpace(autoBattle))
			{
				return autoBattle;
			}
		}
		return (ChallengeConfig != null && !string.IsNullOrWhiteSpace(ChallengeConfig.AutoBattle)) ? ChallengeConfig.AutoBattle : "全配队通用";
	}

	public void InitLostVoidDetectorModel()
	{
		if (Detector == null || Detector.IsShutdown || Detector.UseGpu != _ctx.ModelConfig.LostVoidDetGpu)
		{
			Detector?.Shutdown();
			Detector = new LostVoidDetector(_ctx);
		}
	}

	/// <summary>
	/// 创建并加载迷失之地检测模型。
	/// </summary>
	public bool LoadLostVoidDetectorModel()
	{
		InitLostVoidDetectorModel();
		return Detector!.InitModel();
	}

	public void LoadChallengeConfig()
	{
		ChallengeConfig = LostVoidChallengeConfig.Load(_ctx.Environment, ChallengeConfigName);
		PredefinedTeamIdx = ChallengeConfig.PredefinedTeamIdx;
	}

	public void LoadArtifactData()
	{
		string path = Path.Combine(GameConst.GetGameDataPath(_ctx.Environment), "hollow_zero", "lost_void", "lost_void_artifact_data.yml");
		if (!File.Exists(path))
		{
			AllArtifactList = Array.Empty<LostVoidArtifact>();
			GearByName = new Dictionary<string, LostVoidArtifact>(StringComparer.Ordinal);
			CategoryToArtifacts = new Dictionary<string, IReadOnlyList<LostVoidArtifact>>(StringComparer.Ordinal);
			return;
		}
		using StreamReader input = new StreamReader(path);
		List<LostVoidArtifact> list = Deserializer.Deserialize<List<LostVoidArtifact>>(input);
		AllArtifactList = list ?? new List<LostVoidArtifact>();
		GearByName = AllArtifactList.Where((LostVoidArtifact artifact) => !string.IsNullOrWhiteSpace(artifact.Name)).GroupBy<LostVoidArtifact, string>((LostVoidArtifact artifact) => artifact.Name, StringComparer.Ordinal).ToDictionary<IGrouping<string, LostVoidArtifact>, string, LostVoidArtifact>((IGrouping<string, LostVoidArtifact> group) => group.Key, (IGrouping<string, LostVoidArtifact> group) => group.First(), StringComparer.Ordinal);
		CategoryToArtifacts = AllArtifactList.Where((LostVoidArtifact artifact) => !string.IsNullOrWhiteSpace(artifact.Category)).GroupBy<LostVoidArtifact, string>((LostVoidArtifact artifact) => artifact.Category, StringComparer.Ordinal).ToDictionary<IGrouping<string, LostVoidArtifact>, string, IReadOnlyList<LostVoidArtifact>>((IGrouping<string, LostVoidArtifact> group) => group.Key, (IGrouping<string, LostVoidArtifact> group) => group.ToList(), StringComparer.Ordinal);
	}

	public void LoadInvestigationStrategy()
	{
		string path = Path.Combine(GameConst.GetGameDataPath(_ctx.Environment), "hollow_zero", "lost_void", "lost_void_investigation_strategy.yml");
		if (!File.Exists(path))
		{
			InvestigationStrategyList = Array.Empty<LostVoidInvestigationStrategy>();
			return;
		}
		using StreamReader input = new StreamReader(path);
		List<LostVoidInvestigationStrategy> list = Deserializer.Deserialize<List<LostVoidInvestigationStrategy>>(input);
		InvestigationStrategyList = list ?? new List<LostVoidInvestigationStrategy>();
	}

	public LostVoidArtifact? GetArtifactByFullName(string? fullName)
	{
		if (string.IsNullOrWhiteSpace(fullName))
		{
			return null;
		}
		return AllArtifactList.FirstOrDefault((LostVoidArtifact artifact) => string.Equals(artifact.DisplayName, fullName, StringComparison.Ordinal));
	}

	public LostVoidArtifact? MatchArtifactByOcrFull(string? fullName)
	{
		if (string.IsNullOrWhiteSpace(fullName) || CategoryToArtifacts.Count == 0)
		{
			return null;
		}
		string normalized = fullName.Trim().Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal)
			.Replace("【", string.Empty, StringComparison.Ordinal)
			.Replace("】", string.Empty, StringComparison.Ordinal);
		List<string> list = (from item in CategoryToArtifacts.Keys.Where(delegate(string category)
			{
				bool flag = ((category == "卡牌" || category == "无详情") ? true : false);
				return !flag;
			}).Select(delegate(string category)
			{
				string text3 = _ctx.GameTextResolver(category);
				if (normalized.Length < text3.Length)
				{
					return (Category: category, Score: -1);
				}
				string first = normalized.Substring(0, text3.Length);
				return (Category: category, Score: StringUtils.LongestCommonSubsequenceLength(first, text3));
			})
			where item.Score >= 0
			orderby item.Score descending
			select item.Category).ToList();
		list.Add("卡牌");
		list.Add("无详情");
		foreach (string item in list)
		{
			if (!CategoryToArtifacts.TryGetValue(item, out IReadOnlyList<LostVoidArtifact> value))
			{
				continue;
			}
			foreach (LostVoidArtifact item2 in value)
			{
				string text = _ctx.GameTextResolver(item2.Name);
				if (!string.IsNullOrWhiteSpace(text) && normalized.Length >= text.Length)
				{
					string text2 = normalized;
					int length = text.Length;
					string target = text2.Substring(text2.Length - length);
					if (StringUtils.FindByLcs(text, target, 0.5, ignoreCase: false))
					{
						return item2;
					}
				}
			}
		}
		return null;
	}

	/// <summary>
	/// 从当前选择或商店画面提取藏品候选，并关联选择、NEW 和不可购买标识。
	/// </summary>
	public IReadOnlyList<LostVoidArtifactPos> GetArtifactPos(Mat screen, bool toChooseGearBranch = false, string screenName = "迷失之地-通用选择")
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		List<LostVoidArtifactPos> list = BuildArtifactCandidatesFromNameOcr(screen, screenName);
		if (list.Count == 0)
		{
			return list;
		}
		if (toChooseGearBranch)
		{
			string[] array = new string[2] { "a", "b" };
			foreach (string text in array)
			{
				MatchResultList matchResultList = _ctx.TemplateMatcher.MatchTemplate(screen, "lost_void", "gear_branch_" + text, "raw", 0.9);
				MatchResult match = matchResultList.Max;
				if (match != null)
				{
					LostVoidArtifactPos lostVoidArtifactPos = list.Where((LostVoidArtifactPos candidate) => match.Rect.X1 > candidate.Rect.Center.X).MinBy((LostVoidArtifactPos candidate) => Math.Abs(match.Center.X - candidate.Rect.Center.X));
					if (lostVoidArtifactPos != null)
					{
						lostVoidArtifactPos.Artifact = new LostVoidArtifact
						{
							Category = lostVoidArtifactPos.Artifact.Category,
							Name = lostVoidArtifactPos.Artifact.Name + "-" + text,
							Level = lostVoidArtifactPos.Artifact.Level,
							IsGear = lostVoidArtifactPos.Artifact.IsGear,
							TemplateId = lostVoidArtifactPos.Artifact.TemplateId
						};
					}
				}
			}
		}
		string[] targetWords = new string[4] { "有同流派武备", "已选择", "齿轮硬币不足", "NEW!" }.Select(_ctx.GameTextResolver).ToArray();
		foreach (OcrMatchResult marker in _ctx.OcrService.GetOcrResultList(screen))
		{
			int? num = StringUtils.FindBestMatchByDifflib(marker.Text, targetWords);
			if (!num.HasValue)
			{
				continue;
			}
			LostVoidArtifactPos lostVoidArtifactPos2 = list.Where((LostVoidArtifactPos candidate) => marker.Rect.Y2 < candidate.Rect.Y1).MinBy((LostVoidArtifactPos candidate) => Math.Abs(marker.Center.X - candidate.Rect.Center.X));
			if (lostVoidArtifactPos2 != null)
			{
				switch (num.Value)
				{
				case 0:
					lostVoidArtifactPos2.HasSameStyle = true;
					lostVoidArtifactPos2.Chosen = true;
					lostVoidArtifactPos2.CanChoose = false;
					break;
				case 1:
					lostVoidArtifactPos2.Chosen = true;
					lostVoidArtifactPos2.CanChoose = false;
					break;
				case 2:
					lostVoidArtifactPos2.CanChoose = false;
					break;
				case 3:
					lostVoidArtifactPos2.IsNew = true;
					break;
				}
			}
		}
		return list;
	}

	/// <summary>
	/// 按 BaselineParity 的主选、NEW、两级优先级、动态放弃组和坐标顺序挑选候选。
	/// </summary>
	public IReadOnlyList<LostVoidArtifactPos> GetArtifactByPriority(IEnumerable<LostVoidArtifactPos> artifactList, int chooseNum, bool considerPriority1 = true, bool considerPriority2 = true, bool considerNotInPriority = true, IReadOnlyCollection<int>? ignoreIndexList = null, bool considerPriorityNew = false)
	{
		ArgumentNullException.ThrowIfNull(artifactList, "artifactList");
		if (chooseNum <= 0)
		{
			return Array.Empty<LostVoidArtifactPos>();
		}
		List<LostVoidArtifactPos> candidates = (from candidate in RemoveOverlappingArtifacts(artifactList)
			orderby candidate.Rect.Center.X, candidate.Rect.Center.Y
			select candidate).ToList();
		LostVoidChallengeConfig challengeConfig = ChallengeConfig;
		if (challengeConfig == null || candidates.Count == 0)
		{
			return candidates.Take(chooseNum).ToArray();
		}
		List<IReadOnlyList<string>> list = new List<IReadOnlyList<string>>();
		List<string> list2 = DynamicPriorityList.ToList();
		if (considerPriority1)
		{
			list2.AddRange(challengeConfig.ArtifactPriorityInBattle);
		}
		list.Add(list2);
		if (considerPriority2 && challengeConfig.ArtifactPriority2.Count > 0)
		{
			list.Add(challengeConfig.ArtifactPriority2);
		}
		if (list.All((IReadOnlyList<string> priority) => priority.Count == 0))
		{
			considerNotInPriority = true;
		}
		HashSet<int> hashSet;
		if (ignoreIndexList == null)
		{
			hashSet = new HashSet<int>();
		}
		else
		{
			HashSet<int> hashSet2 = new HashSet<int>();
			foreach (int ignoreIndex in ignoreIndexList)
			{
				hashSet2.Add(ignoreIndex);
			}
			hashSet = hashSet2;
		}
		HashSet<int> ignored = hashSet;
		List<int> selected = new List<int>();
		bool[] array = new bool[2] { true, false };
		foreach (bool primary in array)
		{
			List<int> source = (from index in Enumerable.Range(0, candidates.Count)
				where !ignored.Contains(index) && candidates[index].IsPrimaryName == primary
				select index).ToList();
			if (considerPriorityNew)
			{
				string[] array2 = new string[4] { "S", "A", "B", "?" };
				foreach (string level in array2)
				{
					foreach (int item in source.Where((int index) => candidates[index].IsNew && IsLevelMatch(candidates[index].Artifact.Level, level)))
					{
						AddSelection(selected, item);
					}
				}
			}
			foreach (IReadOnlyList<string> item2 in list)
			{
				foreach (string rule in item2)
				{
					string text = ExtractPriorityRuleCategory(rule);
					if (text != null && DynamicAbandonList.Contains<string>(text, StringComparer.Ordinal) && !IsSpecificPriorityRule(rule))
					{
						continue;
					}
					foreach (int item3 in source.Where((int index) => IsPriorityRuleMatch(candidates[index], rule)))
					{
						AddSelection(selected, item3);
					}
				}
			}
			if (!considerNotInPriority)
			{
				continue;
			}
			foreach (int item4 in source.Where((int index) => !selected.Contains(index) && !DynamicAbandonList.Contains<string>(candidates[index].Artifact.Category, StringComparer.Ordinal)))
			{
				AddSelection(selected, item4);
			}
			foreach (int item5 in source.Where((int index) => !selected.Contains(index)))
			{
				AddSelection(selected, item5);
			}
		}
		return (from index in selected.Take(chooseNum)
			select candidates[index]).ToArray();
	}

	private List<LostVoidArtifactPos> BuildArtifactCandidatesFromNameOcr(Mat screen, string screenName)
	{
		OneDragon.Core.Screen.ScreenArea area = _ctx.ScreenContext.GetArea(screenName, "区域-藏品名称");
		if (area == null)
		{
			return new List<LostVoidArtifactPos>();
		}
		List<LostVoidArtifactPos> list = new List<LostVoidArtifactPos>();
		foreach (OcrMatchResult ocrResult in _ctx.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect))
		{
			if (TryCreateArtifactFromOcrText(ocrResult.Text, out LostVoidArtifact artifact, out bool isPrimaryName))
			{
				list.Add(new LostVoidArtifactPos(artifact, ocrResult.Rect, ocrResult.Text, isPrimaryName));
			}
		}
		List<LostVoidArtifactPos> list2 = new List<LostVoidArtifactPos>();
		foreach (LostVoidArtifactPos candidate in from item in list
			orderby item.Rect.Center.X, item.Rect.Center.Y
			select item)
		{
			int num = list2.FindIndex((LostVoidArtifactPos item) => Math.Abs(item.Rect.Center.X - candidate.Rect.Center.X) < 90);
			if (num < 0)
			{
				list2.Add(candidate);
			}
			else
			{
				list2[num] = PickBetterCandidate(list2[num], candidate);
			}
		}
		return (from item in list2
			orderby item.Rect.Center.X, item.Rect.Center.Y
			select item).ToList();
	}

	private static bool TryCreateArtifactFromOcrText(string? ocrText, out LostVoidArtifact? artifact, out bool isPrimaryName)
	{
		artifact = null;
		isPrimaryName = false;
		string text = (ocrText ?? string.Empty).Trim();
		if (text.Length < 2)
		{
			return false;
		}
		Match match = BracketArtifactNameRegex.Match(text.Replace('【', '[').Replace('】', ']'));
		if (match.Success)
		{
			string text2 = match.Groups[1].Value.Trim();
			string text3 = match.Groups[2].Value.Trim();
			if (text3.Length == 0)
			{
				return false;
			}
			string text4 = text2.Split('：', ':')[0].Trim();
			artifact = new LostVoidArtifact
			{
				Category = ((text4.Length == 0) ? text2 : text4),
				Name = text3,
				Level = "?"
			};
			isPrimaryName = true;
			return true;
		}
		Match match2 = CardArtifactNameRegex.Match(text);
		if (match2.Success)
		{
			string text5 = match2.Groups[1].Value.Trim();
			string text6 = match2.Groups[2].Value.Trim();
			if (text5.Length > 0)
			{
				artifact = new LostVoidArtifact
				{
					Category = "卡牌",
					Name = (text5 + " " + text6).Trim(),
					Level = "?"
				};
				isPrimaryName = true;
				return true;
			}
		}
		artifact = new LostVoidArtifact
		{
			Category = "无详情",
			Name = text,
			Level = "?"
		};
		return true;
	}

	private static LostVoidArtifactPos PickBetterCandidate(LostVoidArtifactPos left, LostVoidArtifactPos right)
	{
		if (right.IsPrimaryName && !left.IsPrimaryName)
		{
			return right;
		}
		if (left.IsPrimaryName && !right.IsPrimaryName)
		{
			return left;
		}
		return (right.Rect.Center.Y < left.Rect.Center.Y) ? right : left;
	}

	private static IReadOnlyList<LostVoidArtifactPos> RemoveOverlappingArtifacts(IEnumerable<LostVoidArtifactPos> artifacts)
	{
		List<LostVoidArtifactPos> list = artifacts.OrderBy((LostVoidArtifactPos item) => item.Rect.Center.X).ToList();
		List<LostVoidArtifactPos> list2 = new List<LostVoidArtifactPos>();
		int num = 0;
		while (num < list.Count)
		{
			LostVoidArtifactPos lostVoidArtifactPos = list[num];
			int num2 = 1;
			List<LostVoidArtifactPos> list3 = new List<LostVoidArtifactPos>(num2);
			CollectionsMarshal.SetCount(list3, num2);
			CollectionsMarshal.AsSpan(list3)[0] = lostVoidArtifactPos;
			List<LostVoidArtifactPos> list4 = list3;
			int num3;
			for (num3 = num + 1; num3 < list.Count && Math.Abs(lostVoidArtifactPos.Rect.Center.X - list[num3].Rect.Center.X) < 100; num3++)
			{
				list4.Add(list[num3]);
			}
			list2.Add(list4.MinBy((LostVoidArtifactPos item) => item.Rect.Center.Y));
			num = num3;
		}
		return list2;
	}

	private static void AddSelection(List<int> selected, int index)
	{
		if (!selected.Contains(index))
		{
			selected.Add(index);
		}
	}

	private static bool IsLevelMatch(string actual, string expected)
	{
		if (expected == "?")
		{
			bool flag;
			switch (actual)
			{
			case "S":
			case "A":
			case "B":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			return !flag;
		}
		return string.Equals(actual, expected, StringComparison.Ordinal);
	}

	private static string? ExtractPriorityRuleCategory(string rule)
	{
		string text = rule.Trim();
		if (text.Length == 0)
		{
			return null;
		}
		int num = text.IndexOf(' ');
		return (num < 0) ? text : text.Substring(0, num).Trim();
	}

	private static bool IsSpecificPriorityRule(string rule)
	{
		return rule.Trim().Contains(' ', StringComparison.Ordinal);
	}

	private static bool IsPriorityRuleMatch(LostVoidArtifactPos artifactPos, string rule)
	{
		string text = rule.Trim();
		if (text.Length == 0)
		{
			return false;
		}
		int num = text.IndexOf(' ');
		return (num < 0) ? string.Equals(artifactPos.Artifact.Category, text, StringComparison.Ordinal) : string.Equals(artifactPos.Artifact.DisplayName, text, StringComparison.Ordinal);
	}

	public (List<string> Items, string ErrorMessage) CheckArtifactPriorityInput(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return (Items: new List<string>(), ErrorMessage: string.Empty);
		}
		return (Items: (from item in input.Split('\n')
			select item.Trim() into item
			where item.Length > 0
			select item).ToList(), ErrorMessage: string.Empty);
	}

	public (List<string> Items, string ErrorMessage) CheckRegionTypePriorityInput(string? input, IReadOnlySet<string>? validRegionTypes = null)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return (Items: new List<string>(), ErrorMessage: string.Empty);
		}
		if (validRegionTypes == null)
		{
			validRegionTypes = LostVoidRegionType.All.ToHashSet<string>(StringComparer.Ordinal);
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (string item in from item in input.Split('\n')
			select item.Trim() into item
			where item.Length > 0
			select item)
		{
			if (validRegionTypes.Contains(item))
			{
				list.Add(item);
			}
			else
			{
				list2.Add(item);
			}
		}
		return (Items: list, ErrorMessage: string.Concat(list2.Select((string item) => "输入非法 " + item)));
	}

	public LostVoidMoveTarget? GetEntryByPriority(IReadOnlyList<LostVoidMoveTarget>? entryList, IReadOnlyCollection<string>? ignoreEntryList = null)
	{
		if (entryList == null || entryList.Count == 0)
		{
			return null;
		}
		HashSet<string> ignored = new HashSet<string>(ignoreEntryList ?? Array.Empty<string>(), StringComparer.Ordinal);
		if (HadInteractedOpheliaOnCurrentLevel)
		{
			ignored.Add("战斗-道中危机");
		}
		LostVoidChallengeConfig? challengeConfig = ChallengeConfig;
		IReadOnlyList<string> readOnlyList;
		if (challengeConfig == null || challengeConfig.RegionTypePriority.Count <= 0)
		{
			readOnlyList = LostVoidRegionType.All;
		}
		else
		{
			IReadOnlyList<string> regionTypePriority = ChallengeConfig.RegionTypePriority;
			readOnlyList = regionTypePriority;
		}
		IReadOnlyList<string> readOnlyList2 = readOnlyList;
		foreach (string priority in readOnlyList2)
		{
			if (ignored.Contains(priority))
			{
				continue;
			}
			LostVoidMoveTarget lostVoidMoveTarget = entryList.Where((LostVoidMoveTarget entry) => entry.TargetNames.Any((string name) => !ignored.Contains(name) && string.Equals(name, priority, StringComparison.Ordinal))).MaxBy((LostVoidMoveTarget entry) => entry.EntireRect.X1);
			if (lostVoidMoveTarget == null)
			{
				continue;
			}
			return lostVoidMoveTarget;
		}
		return entryList.Where((LostVoidMoveTarget entry) => entry.TargetNames.All((string name) => !ignored.Contains(name))).MaxBy((LostVoidMoveTarget entry) => entry.EntireRect.X1);
	}

	public void AppendAgentTypePriorityFromCurrentTeam()
	{
		_ctx.Logger.Information("迷失之地追加代理人类型优先级前刷新当前队伍");
		_ctx.AutoBattleContext.AgentContext.Team.RequestCheckAllAgents();
		if (_ctx.Controller != null)
		{
			var (dateTimeOffset, mat) = _ctx.Controller.Screenshot();
			try
			{
				if (mat != null)
				{
					_ctx.AutoBattleContext.AgentContext.ResetCheckAgentTime();
					_ctx.AutoBattleContext.AgentContext.CheckAgentRelated(mat, (double)dateTimeOffset.ToUnixTimeMilliseconds() / 1000.0);
				}
			}
			finally
			{
				mat?.Dispose();
			}
		}
		HashSet<string> hashSet = (from agent in _ctx.AutoBattleContext.AgentContext.Team.Snapshot()
			select agent.Agent?.AgentType.GetStringValue() into type
			where !string.IsNullOrWhiteSpace(type)
			select (type)).ToHashSet<string>(StringComparer.Ordinal);
		if (hashSet.Count == 0)
		{
			_ctx.Logger.Information("迷失之地刷新后仍未识别到队伍，跳过代理人类型优先级追加");
			return;
		}
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		if (ChallengeConfig != null)
		{
			foreach (string item in ChallengeConfig.ArtifactPriorityInBattle.Concat(ChallengeConfig.ArtifactPriority2))
			{
				if (IsSpecificPriorityRule(item))
				{
					string text = ExtractPriorityRuleCategory(item);
					if (!string.IsNullOrWhiteSpace(text))
					{
						hashSet2.Add(text);
					}
				}
			}
		}
		foreach (string currentType in hashSet)
		{
			if (!DynamicPriorityList.Contains<string>(currentType, StringComparer.Ordinal))
			{
				DynamicPriorityList.Add(currentType);
			}
			DynamicAbandonList.RemoveAll((string item) => string.Equals(item, currentType, StringComparison.Ordinal));
		}
		AgentTypeEnum[] values = Enum.GetValues<AgentTypeEnum>();
		foreach (AgentTypeEnum agentTypeEnum in values)
		{
			if (agentTypeEnum != AgentTypeEnum.UNKNOWN)
			{
				string stringValue = agentTypeEnum.GetStringValue();
				if (!hashSet.Contains(stringValue) && !hashSet2.Contains(stringValue) && !DynamicAbandonList.Contains<string>(stringValue, StringComparer.Ordinal))
				{
					DynamicAbandonList.Add(stringValue);
				}
			}
		}
		_ctx.Logger.Information("迷失之地代理人类型优先级更新: Current={Current}, Priority={Priority}, Abandon={Abandon}", string.Join(", ", hashSet), string.Join(", ", DynamicPriorityList), string.Join(", ", DynamicAbandonList));
	}

	public void AfterAppShutdown()
	{
		AfterAppShutdownCalled = true;
		Detector?.Shutdown();
	}
}
