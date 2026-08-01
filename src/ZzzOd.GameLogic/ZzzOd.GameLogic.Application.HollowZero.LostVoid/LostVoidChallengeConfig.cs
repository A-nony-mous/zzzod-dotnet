using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地挑战配置。
/// </summary>
public sealed class LostVoidChallengeConfig : IYamlUnknownFieldPreserving
{
	private List<string>? _artifactPriorityInBattle;

	[YamlIgnore]
	internal string? SourceFilePath { get; private set; }

	[YamlMember(Alias = "predefined_team_idx", ApplyNamingConventions = false)]
	public int PredefinedTeamIdx { get; set; } = -1;

	[YamlMember(Alias = "choose_team_by_priority", ApplyNamingConventions = false)]
	public bool ChooseTeamByPriority { get; set; }

	[YamlMember(Alias = "manually_choose_agent", ApplyNamingConventions = false)]
	public bool ManuallyChooseAgent { get; set; }

	[YamlMember(Alias = "team_info", ApplyNamingConventions = false)]
	public List<string> TeamInfo { get; set; }

	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; }

	[YamlMember(Alias = "artifact_priority_new", ApplyNamingConventions = false)]
	public bool ArtifactPriorityNew { get; set; }

	[YamlMember(Alias = "artifact_priority", ApplyNamingConventions = false)]
	public List<string> ArtifactPriority { get; set; }

	[YamlMember(Alias = "artifact_priority_2", ApplyNamingConventions = false)]
	public List<string> ArtifactPriority2 { get; set; }

	[YamlMember(Alias = "region_type_priority", ApplyNamingConventions = false)]
	public List<string> RegionTypePriority { get; set; }

	[YamlMember(Alias = "period_buff_no", ApplyNamingConventions = false)]
	public string PeriodBuffNo { get; set; }

	[YamlMember(Alias = "buy_only_priority_1", ApplyNamingConventions = false)]
	public int BuyOnlyPriority1 { get; set; }

	[YamlMember(Alias = "buy_only_priority_2", ApplyNamingConventions = false)]
	public int BuyOnlyPriority2 { get; set; }

	[YamlMember(Alias = "store_gold", ApplyNamingConventions = false)]
	public bool StoreGold { get; set; }

	[YamlMember(Alias = "store_blood", ApplyNamingConventions = false)]
	public bool StoreBlood { get; set; }

	[YamlMember(Alias = "store_blood_min", ApplyNamingConventions = false)]
	public int StoreBloodMin { get; set; }

	[YamlMember(Alias = "investigation_strategy", ApplyNamingConventions = false)]
	public string InvestigationStrategy { get; set; }

	[YamlMember(Alias = "chase_new_mode", ApplyNamingConventions = false)]
	public bool ChaseNewMode { get; set; }

	/// <summary>
	/// 本轮运行时的第一优先级。协战代理人会把对应武备类型追加到这里。
	/// </summary>
	[YamlIgnore]
	public List<string> ArtifactPriorityInBattle => _artifactPriorityInBattle ?? (_artifactPriorityInBattle = ArtifactPriority.ToList());

	/// <summary>第一优先级文本。</summary>
	[YamlIgnore]
	public string ArtifactPriorityText => string.Join('\n', ArtifactPriority);

	/// <summary>第二优先级文本。</summary>
	[YamlIgnore]
	public string ArtifactPriority2Text => string.Join('\n', ArtifactPriority2);

	/// <summary>区域优先级文本。</summary>
	[YamlIgnore]
	public string RegionTypePriorityText => string.Join('\n', RegionTypePriority);

	/// <summary>
	/// 清除本轮运行时优先级，使下次读取重新复制配置优先级。
	/// </summary>
	public void ClearArtifactPriorityInBattle()
	{
		_artifactPriorityInBattle = null;
	}

	/// <summary>
	/// 加载挑战配置。
	/// </summary>
	public static LostVoidChallengeConfig Load(OneDragonEnvironment environment, string moduleName)
	{
		IReadOnlyList<string> subDirectories = new string[] { "lost_void_challenge" };
		YamlConfig<LostVoidChallengeConfig> yamlConfig = new YamlConfig<LostVoidChallengeConfig>(environment, moduleName, null, null, subDirectories, sample: true);
		LostVoidChallengeConfig current = yamlConfig.Current;
		current.SourceFilePath = yamlConfig.FilePath;
		return current;
	}

	/// <summary>
	/// 把挑战配置写入真实用户配置文件。
	/// </summary>
	public static void Save(
		OneDragonEnvironment environment,
		string moduleName,
		LostVoidChallengeConfig config,
		string? sourceModuleName = null)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		ArgumentNullException.ThrowIfNull(config, "config");
		string userFilePath = GetUserFilePath(environment, moduleName);
		Directory.CreateDirectory(Path.GetDirectoryName(userFilePath));
		string? sourceFilePath = config.SourceFilePath;
		if (string.IsNullOrWhiteSpace(sourceFilePath) && !string.IsNullOrWhiteSpace(sourceModuleName))
		{
			sourceFilePath = Load(environment, sourceModuleName).SourceFilePath;
		}
		new YamlOperator().SavePreservingUnknownFields(userFilePath, config, sourceFilePath);
		config.SourceFilePath = userFilePath;
	}

	/// <summary>
	/// 删除真实用户配置；sample 文件不会被删除。
	/// </summary>
	public static bool Delete(OneDragonEnvironment environment, string moduleName)
	{
		string userFilePath = GetUserFilePath(environment, moduleName);
		if (!File.Exists(userFilePath))
		{
			return false;
		}
		File.Delete(userFilePath);
		YamlOperator.InvalidateCache(userFilePath);
		return true;
	}

	/// <summary>
	/// 判断当前模块是否只存在 sample 配置。
	/// </summary>
	public static bool IsSample(OneDragonEnvironment environment, string moduleName)
	{
		string userFilePath = GetUserFilePath(environment, moduleName);
		string path = Path.Combine(Path.GetDirectoryName(userFilePath), moduleName + ".sample.yml");
		return !File.Exists(userFilePath) && File.Exists(path);
	}

	/// <summary>
	/// 获取用户配置文件路径并校验模块名。
	/// </summary>
	public static string GetUserFilePath(OneDragonEnvironment environment, string moduleName)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		string text = ValidateModuleName(moduleName);
		return Path.Combine(environment.WorkDirectory, "config", "lost_void_challenge", text + ".yml");
	}

	/// <summary>
	/// 获取配置目录下全部可读取的挑战配置名。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <param name="withSample">是否包含 sample 配置。</param>
	/// <param name="onInvalidConfig">发现无法读取的配置时接收模块名和原始异常。</param>
	public static IReadOnlyList<string> GetAllModuleNames(
		OneDragonEnvironment environment,
		bool withSample = true,
		Action<string, Exception>? onInvalidConfig = null)
	{
		IReadOnlyList<string> moduleNames = GetModuleNamesOnDisk(environment, withSample);
		List<string> validModuleNames = new List<string>(moduleNames.Count);
		foreach (string moduleName in moduleNames)
		{
			try
			{
				Load(environment, moduleName);
				validModuleNames.Add(moduleName);
			}
			catch (Exception ex)
			{
				onInvalidConfig?.Invoke(moduleName, ex);
			}
		}
		return validModuleNames;
	}

	/// <summary>
	/// 获取新的自定义配置名。
	/// </summary>
	public static string GetNewModuleName(OneDragonEnvironment environment)
	{
		int num = 0;
		foreach (string allModuleName in GetModuleNamesOnDisk(environment, withSample: true))
		{
			if (allModuleName.StartsWith("自定义-", StringComparison.Ordinal) && int.TryParse(allModuleName.Substring("自定义-".Length), out var result))
			{
				num = Math.Max(num, result);
			}
		}
		return $"{"自定义-"}{num + 1:00}";
	}

	private static IReadOnlyList<string> GetModuleNamesOnDisk(OneDragonEnvironment environment, bool withSample)
	{
		string path = Path.Combine(environment.WorkDirectory, "config", "lost_void_challenge");
		if (!Directory.Exists(path))
		{
			return Array.Empty<string>();
		}
		return (from name in Directory.EnumerateFiles(path, "*.yml", SearchOption.TopDirectoryOnly).Select(Path.GetFileName)
			where !string.IsNullOrWhiteSpace(name)
			select name into fileName
			where withSample || !fileName.EndsWith(".sample.yml", StringComparison.Ordinal)
			select fileName.EndsWith(".sample.yml", StringComparison.Ordinal)
				? fileName.Substring(0, fileName.Length - 11)
				: fileName.Substring(0, fileName.Length - 4))
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal)
			.ToArray();
	}

	private static string ValidateModuleName(string moduleName)
	{
		string text = (moduleName ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			throw new ArgumentException("配置名称不能为空。", "moduleName");
		}
		bool flag = ((text == "." || text == "..") ? true : false);
		if (flag || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || text.Contains(Path.DirectorySeparatorChar) || text.Contains(Path.AltDirectorySeparatorChar))
		{
			throw new ArgumentException("配置名称包含非法字符。", "moduleName");
		}
		return text;
	}

	public LostVoidChallengeConfig()
	{
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "unknown";
		span[1] = "unknown";
		span[2] = "unknown";
		TeamInfo = list;
		AutoBattle = "全配队通用";
		ArtifactPriority = new List<string>();
		ArtifactPriority2 = new List<string>();
		RegionTypePriority = new List<string>();
		PeriodBuffNo = "第一个";
		BuyOnlyPriority1 = 1;
		BuyOnlyPriority2 = 3;
		StoreGold = true;
		StoreBloodMin = 50;
		InvestigationStrategy = "鸣徽狂热战略";
	}
}
