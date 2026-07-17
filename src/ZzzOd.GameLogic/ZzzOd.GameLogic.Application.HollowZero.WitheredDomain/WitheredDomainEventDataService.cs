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
		int num = text.IndexOfAny(new char[3] { '】', ']', ' ' });
		if (num >= 0 && num + 1 < text.Length)
		{
			return MatchResonium(text.Substring(0, num).Trim(), text.Substring(num + 1).Trim());
		}
		if (text.Length < 3)
		{
			return null;
		}
		WitheredDomainResonium witheredDomainResonium = MatchResonium(text.Substring(0, 2), text.Substring(2));
		return witheredDomainResonium ?? MatchResonium(text.Substring(0, 1), text.Substring(1));
	}

	private WitheredDomainResonium? MatchResonium(string category, string name)
	{
		string[] array = Resonium.Select((WitheredDomainResonium item) => item.Category).Distinct<string>(StringComparer.Ordinal).ToArray();
		int? num = StringUtils.FindBestMatchByDifflib(category, array, 0.5);
		if (!num.HasValue)
		{
			return null;
		}
		string matchedCategory = array[num.Value];
		WitheredDomainResonium[] array2 = Resonium.Where((WitheredDomainResonium item) => item.Category == matchedCategory).ToArray();
		int? num2 = StringUtils.FindBestMatchByDifflib(name, array2.Select((WitheredDomainResonium item) => item.Name).ToArray());
		return (!num2.HasValue) ? null : array2[num2.Value];
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
