using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 闪避截图工具配置。
/// </summary>
public sealed class ScreenshotHelperConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "frequency_second", ApplyNamingConventions = false)]
	public double FrequencySecond { get; set; } = 0.1;

	[YamlMember(Alias = "length_second", ApplyNamingConventions = false)]
	public double LengthSecond { get; set; } = 1.0;

	[YamlMember(Alias = "key_save", ApplyNamingConventions = false)]
	public string KeySave { get; set; } = "1";

	[YamlMember(Alias = "dodge_detect", ApplyNamingConventions = false)]
	public bool DodgeDetect { get; set; } = true;

	[YamlMember(Alias = "screenshot_before_key", ApplyNamingConventions = false)]
	public bool ScreenshotBeforeKey { get; set; } = true;

	[YamlMember(Alias = "mini_map_angle_detect", ApplyNamingConventions = false)]
	public bool MiniMapAngleDetect { get; set; }

	/// <summary>
	/// 每轮截图间隔。
	/// </summary>
	[YamlIgnore]
	public TimeSpan Frequency => TimeSpan.FromSeconds(Math.Max(0.01, FrequencySecond));

	/// <summary>
	/// 缓存时长。
	/// </summary>
	[YamlIgnore]
	public TimeSpan CacheLength => TimeSpan.FromSeconds(Math.Max(0.0, LengthSecond));

	/// <summary>
	/// 截图缓存容量。
	/// </summary>
	[YamlIgnore]
	public int CacheMaxCount
	{
		get
		{
			double num = Math.Max(0.01, FrequencySecond);
			double num2 = Math.Max(0.0, LengthSecond);
			return Math.Max(1, (int)Math.Floor(num2 / num) + 1);
		}
	}

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static ScreenshotHelperConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<ScreenshotHelperConfig> yamlConfig = new YamlConfig<ScreenshotHelperConfig>(environment, "screenshot_helper", null, instanceIndex, new string[2] { "app_config", groupId });
		ScreenshotHelperConfig current = yamlConfig.Current;
		current.ConfigureRuntime("screenshot_helper", instanceIndex, groupId);
		return current;
	}
}
