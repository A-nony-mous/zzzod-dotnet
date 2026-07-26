using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using OneDragon.Core.Utils;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 读取 BaselineParity 同源的枯萎之都普通事件定义。
/// </summary>
internal sealed class WitheredDomainEventDataService
{
	private sealed class WitheredDomainNormalEventYaml
	{
		[YamlMember(Alias = "entry_name", ApplyNamingConventions = false)]
		public string? EntryName { get; set; }

		[YamlMember(Alias = "event_name", ApplyNamingConventions = false)]
		public string EventName { get; set; } = string.Empty;

		[YamlMember(Alias = "lcs_percent", ApplyNamingConventions = false)]
		public float LcsPercent { get; set; } = 1f;

		[YamlMember(Alias = "options", ApplyNamingConventions = false)]
		public List<WitheredDomainNormalEventOptionYaml> Options { get; set; } = new List<WitheredDomainNormalEventOptionYaml>();

		public HollowZeroEvent ToEvent()
		{
			return new HollowZeroEvent(EventName, EntryName, Options.Select((WitheredDomainNormalEventOptionYaml option) => option.ToOption()).ToList(), LcsPercent);
		}
	}

	private sealed class WitheredDomainNormalEventOptionYaml
	{
		[YamlMember(Alias = "option_name", ApplyNamingConventions = false)]
		public string OptionName { get; set; } = string.Empty;

		[YamlMember(Alias = "desc", ApplyNamingConventions = false)]
		public string? Desc { get; set; }

		[YamlMember(Alias = "wait", ApplyNamingConventions = false)]
		public float Wait { get; set; } = 1f;

		[YamlMember(Alias = "ocr_word", ApplyNamingConventions = false)]
		public string? OcrWord { get; set; }

		[YamlMember(Alias = "lcs_percent", ApplyNamingConventions = false)]
		public float LcsPercent { get; set; } = 0.5f;

		public HollowZeroNormalEventOption ToOption()
		{
			return new HollowZeroNormalEventOption(OptionName, Desc, Wait, OcrWord, LcsPercent);
		}
	}

	private readonly OneDragonEnvironment _environment;

	private readonly YamlOperator _yaml = new YamlOperator();

	private IReadOnlyList<HollowZeroEvent>? _normalEvents;

	private IReadOnlyList<WitheredDomainResonium>? _resonium;

	public IReadOnlyList<HollowZeroEvent> NormalEvents => _normalEvents ?? (_normalEvents = LoadNormalEvents());

	public IReadOnlyList<WitheredDomainResonium> Resonium => _resonium ?? (_resonium = LoadResonium());

	public WitheredDomainEventDataService(OneDragonEnvironment environment)
	{
		_environment = environment ?? throw new ArgumentNullException("environment");
	}

	public HollowZeroEvent? GetNormalEventByName(string? eventName)
	{
		return string.IsNullOrWhiteSpace(eventName) ? null : NormalEvents.FirstOrDefault((HollowZeroEvent item) => string.Equals(item.EventName, eventName, StringComparison.Ordinal));
	}

	public IReadOnlyList<HollowZeroEvent> GetNormalEventsByEntryName(string entryName)
	{
		return NormalEvents.Where((HollowZeroEvent item) => string.Equals(item.EntryName, entryName, StringComparison.Ordinal)).ToArray();
	}

	public WitheredDomainResonium? MatchResoniumByOcrFull(string? ocrText)
	{
		string text = (ocrText ?? string.Empty).Trim().Replace("[", string.Empty).Replace("]", string.Empty);

		// 分隔符按优先级依次尝试：全角右括号、半角右括号（已被上面替换掉，实际恒为未命中）、空格
		int idx = text.IndexOf('】');
		if (idx < 0)
		{
			idx = text.IndexOf(']');
		}
		if (idx < 0)
		{
			idx = text.IndexOf(' ');
		}

		if (idx < 0)
		{
			if (text.Length < 2)
			{
				return null;
			}

			// 没有分隔符的情况，大概率是第二个字识别失败
			WitheredDomainResonium? result = MatchResonium(text.Substring(0, 1) + "_", text.Substring(1));
			if (result != null)
			{
				return result;
			}

			// 尝试看看是不是第一个字识别失败
			result = MatchResonium("_" + text.Substring(0, 1), text.Substring(1));
			if (result != null)
			{
				return result;
			}

			if (text.Length == 2)
			{
				// 已经没法匹配到了
				return null;
			}

			// 尝试看前两个字
			return MatchResonium(text.Substring(0, 2), text.Substring(2));
		}

		string categoryText = text.Substring(0, idx).Trim();
		string nameText = text.Substring(idx + 1).Trim();

		if (categoryText.Length > 1)
		{
			return MatchResonium(categoryText, nameText);
		}

		WitheredDomainResonium? trailingUnderscoreResult = MatchResonium(categoryText + "_", nameText);
		if (trailingUnderscoreResult != null)
		{
			return trailingUnderscoreResult;
		}
		return MatchResonium("_" + categoryText, nameText);
	}

	private WitheredDomainResonium? MatchResonium(string categoryOcr, string nameOcr)
	{
		string[] categoryList = Resonium.Select((WitheredDomainResonium item) => item.Category).Distinct<string>(StringComparer.Ordinal).ToArray();
		List<int> categoryMatches = FindTop2MatchesByDifflib(categoryOcr, categoryList, 0.5);
		if (categoryMatches.Count == 0)
		{
			return null;
		}

		int categoryIdx;
		if (categoryMatches.Count == 2 && categoryOcr.Length > 1 && categoryOcr[1] == '_')
		{
			// 强x 会同时匹配到强袭和顽强，这里用已识别到的字符顺序额外判断一下
			string bestCategory = categoryList[categoryMatches[0]];
			categoryIdx = (bestCategory.Length > 0 && bestCategory[0] == categoryOcr[0]) ? categoryMatches[0] : categoryMatches[1];
		}
		else if (categoryMatches.Count == 2 && categoryOcr.Length > 1 && categoryOcr[0] == '_')
		{
			string bestCategory = categoryList[categoryMatches[0]];
			categoryIdx = (bestCategory.Length > 0 && bestCategory[^1] == categoryOcr[1]) ? categoryMatches[0] : categoryMatches[1];
		}
		else
		{
			categoryIdx = categoryMatches[0];
		}

		string matchedCategory = categoryList[categoryIdx];
		WitheredDomainResonium[] inCategory = Resonium.Where((WitheredDomainResonium item) => item.Category == matchedCategory).ToArray();
		int? nameIdx = StringUtils.FindBestMatchByDifflib(nameOcr, inCategory.Select((WitheredDomainResonium item) => item.Name).ToArray());
		return nameIdx.HasValue ? inCategory[nameIdx.Value] : null;
	}

	/// <summary>
	/// 近似 difflib.get_close_matches(word, candidates, n=2, cutoff) 的效果：
	/// 取相似度最高的最多两个候选下标，按相似度从高到低排列。复用单候选匹配算法，
	/// 先取最优，再从剩余候选中取次优，两次调用彼此独立、结果等价于一次性排序取前二。
	/// </summary>
	private static List<int> FindTop2MatchesByDifflib(string word, IReadOnlyList<string> candidates, double cutoff)
	{
		List<int> result = new List<int>();
		int? first = StringUtils.FindBestMatchByDifflib(word, candidates, cutoff);
		if (!first.HasValue)
		{
			return result;
		}
		result.Add(first.Value);

		List<string> remaining = new List<string>(candidates);
		List<int> remainingOriginalIndex = new List<int>();
		for (int i = 0; i < candidates.Count; i++)
		{
			if (i != first.Value)
			{
				remainingOriginalIndex.Add(i);
			}
		}
		remaining.RemoveAt(first.Value);

		int? second = StringUtils.FindBestMatchByDifflib(word, remaining, cutoff);
		if (second.HasValue)
		{
			result.Add(remainingOriginalIndex[second.Value]);
		}
		return result;
	}

	private IReadOnlyList<HollowZeroEvent> LoadNormalEvents()
	{
		string resourcePath = _environment.GetResourcePath("assets", "game_data", "hollow_zero", "normal_event");
		if (!Directory.Exists(resourcePath))
		{
			return Array.Empty<HollowZeroEvent>();
		}
		List<HollowZeroEvent> list = new List<HollowZeroEvent>();
		foreach (string item in Directory.EnumerateFiles(resourcePath, "*.yml", SearchOption.TopDirectoryOnly).OrderBy<string, string>((string path) => path, StringComparer.Ordinal))
		{
			try
			{
				list.AddRange(from eventYaml in _yaml.Load<List<WitheredDomainNormalEventYaml>>(item)
					where !string.IsNullOrWhiteSpace(eventYaml.EventName)
					select eventYaml.ToEvent());
			}
			catch (Exception ex) when (((ex is IOException || ex is YamlException) ? 1 : 0) != 0)
			{
				throw new InvalidOperationException("读取枯萎之都事件定义失败: " + item, ex);
			}
		}
		return list;
	}

	private IReadOnlyList<WitheredDomainResonium> LoadResonium()
	{
		string resourcePath = _environment.GetResourcePath("assets", "game_data", "hollow_zero", "resonium.yml");
		if (!File.Exists(resourcePath))
		{
			return Array.Empty<WitheredDomainResonium>();
		}
		try
		{
			return _yaml.Load<List<WitheredDomainResonium>>(resourcePath);
		}
		catch (Exception ex) when (((ex is IOException || ex is YamlException) ? 1 : 0) != 0)
		{
			throw new InvalidOperationException("读取枯萎之都鸣徽目录失败: " + resourcePath, ex);
		}
	}
}
