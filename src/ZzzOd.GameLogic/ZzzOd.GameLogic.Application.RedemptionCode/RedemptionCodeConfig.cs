using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码全局配置，读取 `config/redemption_codes.yml` 与 `config/redemption_codes.sample.yml`。
/// </summary>
public sealed class RedemptionCodeConfig : ZApplicationConfig, IApplicationConfig
{
	private readonly YamlConfig<RedemptionCodeConfigData> _userConfig;

	private readonly YamlConfig<RedemptionCodeConfigData> _sampleConfig;

	/// <summary>
	/// sample 配置中的兑换码。
	/// </summary>
	[YamlIgnore]
	public IReadOnlyDictionary<string, int> SampleCodesDict => CopyCodes(_sampleConfig.Current.Codes);

	/// <summary>
	/// 用户配置中的兑换码。
	/// </summary>
	[YamlIgnore]
	public IReadOnlyDictionary<string, int> UserCodesDict => CopyCodes(_userConfig.Current.Codes);

	/// <summary>
	/// 合并后的兑换码，用户配置覆盖 sample 配置。
	/// </summary>
	[YamlIgnore]
	public IReadOnlyDictionary<string, int> CodesDict
	{
		get
		{
			Dictionary<string, int> dictionary = CopyCodes(_sampleConfig.Current.Codes);
			foreach (KeyValuePair<string, int> item in CopyCodes(_userConfig.Current.Codes))
			{
				item.Deconstruct(out var key, out var value);
				string key2 = key;
				int value2 = value;
				dictionary[key2] = value2;
			}
			return dictionary;
		}
	}

	/// <summary>
	/// 合并后的兑换码列表。
	/// </summary>
	[YamlIgnore]
	public IReadOnlyList<string> CodesList => CodesDict.Keys.ToArray();

	/// <summary>
	/// 初始化兑换码配置。
	/// </summary>
	public RedemptionCodeConfig(OneDragonEnvironment environment, int instanceIndex = 0, string groupId = "one_dragon")
		: this(new YamlConfig<RedemptionCodeConfigData>(environment, "redemption_codes"), new YamlConfig<RedemptionCodeConfigData>(environment, "redemption_codes.sample"), instanceIndex, groupId)
	{
	}

	internal RedemptionCodeConfig(YamlConfig<RedemptionCodeConfigData> userConfig, YamlConfig<RedemptionCodeConfigData> sampleConfig, int instanceIndex = 0, string groupId = "one_dragon")
	{
		_userConfig = userConfig;
		_sampleConfig = sampleConfig;
		ConfigureRuntime("redemption_code", instanceIndex, groupId);
	}

	/// <summary>
	/// 加载 BaselineParity 兼容全局配置。
	/// </summary>
	public static RedemptionCodeConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		return new RedemptionCodeConfig(environment, instanceIndex, groupId);
	}

	/// <summary>
	/// 添加用户兑换码。
	/// </summary>
	public void AddCode(string code, int endDt = 20990101)
	{
		string trimmed = code.Trim();
		if (!string.IsNullOrWhiteSpace(trimmed))
		{
			UpdateUserCodes(delegate(Dictionary<string, int> codes)
			{
				codes[trimmed] = endDt;
			});
		}
	}

	/// <summary>
	/// 更新用户兑换码。
	/// </summary>
	public void UpdateCode(string oldCode, string newCode, int endDt)
	{
		string trimmed = newCode.Trim();
		UpdateUserCodes(delegate(Dictionary<string, int> codes)
		{
			if (!string.Equals(oldCode, trimmed, StringComparison.Ordinal))
			{
				codes.Remove(oldCode);
			}
			if (!string.IsNullOrWhiteSpace(trimmed))
			{
				codes[trimmed] = endDt;
			}
		});
	}

	/// <summary>
	/// 删除用户兑换码。
	/// </summary>
	public void DeleteCode(string code)
	{
		UpdateUserCodes(delegate(Dictionary<string, int> codes)
		{
			codes.Remove(code);
		});
	}

	/// <summary>
	/// 添加 sample 兑换码。
	/// </summary>
	public void AddSampleCode(string code, int endDt = 20990101)
	{
		string trimmed = code.Trim();
		if (!string.IsNullOrWhiteSpace(trimmed))
		{
			UpdateSampleCodes(delegate(Dictionary<string, int> codes)
			{
				codes[trimmed] = endDt;
			});
		}
	}

	/// <summary>
	/// 删除 sample 兑换码。
	/// </summary>
	public void DeleteSampleCode(string code)
	{
		UpdateSampleCodes(delegate(Dictionary<string, int> codes)
		{
			codes.Remove(code);
		});
	}

	/// <summary>
	/// 清理 sample 中已经过期的兑换码。
	/// </summary>
	public int CleanExpiredSampleCodes(int today)
	{
		int expiredCount = 0;
		UpdateSampleCodes(delegate(Dictionary<string, int> codes)
		{
			string[] array = (from item in codes
				where item.Value < today
				select item.Key).ToArray();
			expiredCount = array.Length;
			string[] array2 = array;
			foreach (string key in array2)
			{
				codes.Remove(key);
			}
		});
		return expiredCount;
	}

	private void UpdateUserCodes(Action<Dictionary<string, int>> apply)
	{
		_userConfig.Update(delegate(RedemptionCodeConfigData data)
		{
			data.Codes = CopyCodes(data.Codes);
			apply(data.Codes);
		});
	}

	private void UpdateSampleCodes(Action<Dictionary<string, int>> apply)
	{
		_sampleConfig.Update(delegate(RedemptionCodeConfigData data)
		{
			data.Codes = CopyCodes(data.Codes);
			apply(data.Codes);
		});
	}

	private static Dictionary<string, int> CopyCodes(IDictionary<string, int>? source)
	{
		return (source == null) ? new Dictionary<string, int>() : new Dictionary<string, int>(source, StringComparer.Ordinal);
	}
}
