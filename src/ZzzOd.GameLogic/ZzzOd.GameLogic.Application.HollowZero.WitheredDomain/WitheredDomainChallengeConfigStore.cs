using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都挑战配置真实文件服务。
/// </summary>
public sealed class WitheredDomainChallengeConfigStore
{
	private sealed class WitheredDomainEntryCatalogItem
	{
		[YamlMember(Alias = "entry_name", ApplyNamingConventions = false)]
		public string EntryName { get; set; } = string.Empty;

		[YamlMember(Alias = "is_benefit", ApplyNamingConventions = false)]
		public bool IsBenefit { get; set; } = true;

		[YamlMember(Alias = "can_go", ApplyNamingConventions = false)]
		public bool CanGo { get; set; } = true;
	}

	private sealed class WitheredDomainResoniumCatalogItem
	{
		[YamlMember(Alias = "category", ApplyNamingConventions = false)]
		public string Category { get; set; } = string.Empty;

		[YamlMember(Alias = "name", ApplyNamingConventions = false)]
		public string Name { get; set; } = string.Empty;
	}

	private readonly OneDragonEnvironment _environment;

	private readonly YamlOperator _yaml = new YamlOperator();

	private string ChallengeDirectory => _environment.GetPathUnderWorkDir("config", "hollow_zero_challenge");

	public WitheredDomainChallengeConfigStore(OneDragonEnvironment environment)
	{
		_environment = environment;
	}

	public IReadOnlyList<WitheredDomainChallengeConfigEntry> GetAll(bool withSample = true)
	{
		string challengeDirectory = ChallengeDirectory;
		if (!Directory.Exists(challengeDirectory))
		{
			return Array.Empty<WitheredDomainChallengeConfigEntry>();
		}
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>(StringComparer.Ordinal);
		string key;
		foreach (string item in Directory.EnumerateFiles(challengeDirectory, "*.yml", SearchOption.TopDirectoryOnly))
		{
			string fileName = Path.GetFileName(item);
			bool flag = fileName.EndsWith(".sample.yml", StringComparison.Ordinal);
			if (!flag || withSample)
			{
				string text;
				if (!flag)
				{
					key = fileName;
					text = key.Substring(0, key.Length - 4);
				}
				else
				{
					key = fileName;
					text = key.Substring(0, key.Length - 11);
				}
				string key2 = text;
				if (!dictionary.TryGetValue(key2, out var value) || value)
				{
					dictionary[key2] = flag;
				}
			}
		}
		List<WitheredDomainChallengeConfigEntry> list = new List<WitheredDomainChallengeConfigEntry>();
		foreach (KeyValuePair<string, bool> item2 in dictionary.OrderBy<KeyValuePair<string, bool>, string>((KeyValuePair<string, bool> item) => item.Key, StringComparer.Ordinal))
		{
			item2.Deconstruct(out key, out var value2);
			string moduleName = key;
			bool isSample = value2;
			string path = GetPath(moduleName, isSample);
			try
			{
				list.Add(new WitheredDomainChallengeConfigEntry(moduleName, isSample, _yaml.Load<WitheredDomainChallengeConfig>(path)));
			}
			catch
			{
			}
		}
		return list;
	}

	public string GetNewModuleName()
	{
		int num = 0;
		foreach (WitheredDomainChallengeConfigEntry item in GetAll())
		{
			if (item.ModuleName.StartsWith("自定义-", StringComparison.Ordinal) && int.TryParse(item.ModuleName.Substring("自定义-".Length), out var result))
			{
				num = Math.Max(num, result);
			}
		}
		return $"{"自定义-"}{num + 1:00}";
	}

	public WitheredDomainChallengeConfigEntry Save(string? originalModuleName, string moduleName, WitheredDomainChallengeConfig config)
	{
		ValidateModuleName(moduleName);
		Directory.CreateDirectory(ChallengeDirectory);
		string path = GetPath(moduleName, isSample: false);
		_yaml.Save(path, config);
		if (!string.IsNullOrWhiteSpace(originalModuleName) && !string.Equals(originalModuleName, moduleName, StringComparison.Ordinal))
		{
			string path2 = GetPath(originalModuleName, isSample: false);
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
		}
		return new WitheredDomainChallengeConfigEntry(moduleName, IsSample: false, config.Clone());
	}

	public void Delete(string moduleName)
	{
		ValidateModuleName(moduleName);
		string path = GetPath(moduleName, isSample: false);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public WitheredDomainChallengeValidationResult ValidateEntryText(string? input)
	{
		HashSet<string> allowed = (from item in LoadEntries()
			select StripEntryPrefix(item.EntryName) into name
			where !string.IsNullOrWhiteSpace(name)
			select name).ToHashSet<string>(StringComparer.Ordinal);
		return ValidateLines(input, (string value) => allowed.Contains(value));
	}

	public WitheredDomainChallengeValidationResult ValidateResoniumText(string? input)
	{
		List<WitheredDomainResoniumCatalogItem> source = LoadResonium();
		HashSet<string> categories = source.Select((WitheredDomainResoniumCatalogItem item) => item.Category).ToHashSet<string>(StringComparer.Ordinal);
		HashSet<string> items = source.Select((WitheredDomainResoniumCatalogItem item) => item.Category + " " + item.Name).ToHashSet<string>(StringComparer.Ordinal);
		return ValidateLines(input, (string value) => categories.Contains(value) || items.Contains(value));
	}

	public IReadOnlyList<string> GetDefaultGoInOneStep()
	{
		return (from item in LoadEntries()
			where item.IsBenefit
			select StripEntryPrefix(item.EntryName) into name
			where !string.IsNullOrWhiteSpace(name)
			select name).ToArray();
	}

	public IReadOnlyList<string> GetDefaultWaypoint()
	{
		return new string[5] { "呼叫增援", "业绩考察点", "零号银行", "邦布商人", "诡雾" };
	}

	public IReadOnlyList<string> GetOnlyBossWaypoint()
	{
		return new string[3] { "呼叫增援", "业绩考察点", "诡雾" };
	}

	public IReadOnlyList<string> GetDefaultAvoid()
	{
		return new string[3] { "危机", "双重危机", "限时战斗" };
	}

	public IReadOnlyList<string> GetNoBattle()
	{
		return (from item in LoadEntries()
			where item.CanGo
			select StripEntryPrefix(item.EntryName)).Where(delegate(string name)
		{
			bool flag = !string.IsNullOrWhiteSpace(name);
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3;
				switch (name)
				{
				case "危机":
				case "双重危机":
				case "限时战斗":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = !flag3;
			}
			return flag2;
		}).ToArray();
	}

	private string GetPath(string moduleName, bool isSample)
	{
		return Path.Combine(ChallengeDirectory, moduleName + (isSample ? ".sample" : string.Empty) + ".yml");
	}

	private List<WitheredDomainEntryCatalogItem> LoadEntries()
	{
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir("assets", "game_data", "hollow_zero", "entry_list.yml");
		if (!File.Exists(pathUnderWorkDir))
		{
			throw new FileNotFoundException("缺少零号空洞入口目录。", pathUnderWorkDir);
		}
		return _yaml.Load<List<WitheredDomainEntryCatalogItem>>(pathUnderWorkDir);
	}

	private List<WitheredDomainResoniumCatalogItem> LoadResonium()
	{
		string pathUnderWorkDir = _environment.GetPathUnderWorkDir("assets", "game_data", "hollow_zero", "resonium.yml");
		if (!File.Exists(pathUnderWorkDir))
		{
			throw new FileNotFoundException("缺少零号空洞鸣徽目录。", pathUnderWorkDir);
		}
		return _yaml.Load<List<WitheredDomainResoniumCatalogItem>>(pathUnderWorkDir);
	}

	private static WitheredDomainChallengeValidationResult ValidateLines(string? input, Func<string, bool> valid)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string[] array = (input ?? string.Empty).Split('\n');
		foreach (string text in array)
		{
			string text2 = text.Trim();
			if (text2.Length != 0)
			{
				if (valid(text2))
				{
					list.Add(text2);
				}
				else
				{
					list2.Add("输入非法 " + text2);
				}
			}
		}
		return new WitheredDomainChallengeValidationResult(list, string.Join("; ", list2));
	}

	private static string StripEntryPrefix(string value)
	{
		return (value.Length > 5) ? value.Substring(5) : string.Empty;
	}

	private static void ValidateModuleName(string moduleName)
	{
		if (string.IsNullOrWhiteSpace(moduleName) || moduleName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || moduleName.Contains(Path.DirectorySeparatorChar) || moduleName.Contains(Path.AltDirectorySeparatorChar))
		{
			throw new ArgumentException("挑战配置名称无效。", "moduleName");
		}
	}

	/// <summary>
	/// 读取当前选中的真实挑战配置。用户文件优先于同名 sample 文件；两者都不存在时静默返回一份全默认配置。
	/// </summary>
	public WitheredDomainChallengeConfig LoadSelected(string moduleName)
	{
		ValidateModuleName(moduleName);
		string path = GetPath(moduleName, isSample: false);
		string path2 = GetPath(moduleName, isSample: true);
		string text;
		if (!File.Exists(path))
		{
			if (!File.Exists(path2))
			{
				// 用户文件和 sample 都不存在时不视为错误，与配置文件读取的静默兜底语义保持一致，
				// 直接给出一份全默认配置（该名字仍由调用方自行记录）。
				return new WitheredDomainChallengeConfig();
			}
			text = path2;
		}
		else
		{
			text = path;
		}
		string filePath = text;
		return _yaml.Load<WitheredDomainChallengeConfig>(filePath);
	}
}
