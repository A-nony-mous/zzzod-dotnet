using System;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 实例级游戏账号配置。
/// </summary>
public sealed class GameAccountConfig
{
	[YamlMember(Alias = "platform", ApplyNamingConventions = false)]
	public string Platform { get; set; } = "PC";

	[YamlMember(Alias = "game_region", ApplyNamingConventions = false)]
	public string GameRegion { get; set; } = "cn";

	[YamlMember(Alias = "use_custom_win_title", ApplyNamingConventions = false)]
	public bool UseCustomWinTitle { get; set; }

	[YamlMember(Alias = "custom_win_title", ApplyNamingConventions = false)]
	public string CustomWinTitle { get; set; } = string.Empty;

	[YamlMember(Alias = "game_path", ApplyNamingConventions = false)]
	public string GamePath { get; set; } = string.Empty;

	[YamlMember(Alias = "game_language", ApplyNamingConventions = false)]
	public string GameLanguage { get; set; } = "cn";

	[YamlMember(Alias = "account", ApplyNamingConventions = false)]
	public string Account { get; set; } = string.Empty;

	[YamlMember(Alias = "password", ApplyNamingConventions = false)]
	public string Password { get; set; } = string.Empty;

	[YamlMember(Alias = "bilibili_account_name", ApplyNamingConventions = false)]
	public string BilibiliAccountName { get; set; } = string.Empty;

	[YamlIgnore]
	public bool HasLoginInfo => string.Equals(GameRegion, "cn_b", StringComparison.Ordinal)
		? !string.IsNullOrWhiteSpace(BilibiliAccountName)
		: !string.IsNullOrWhiteSpace(Account) && !string.IsNullOrWhiteSpace(Password);

	[YamlIgnore]
	public int GameRefreshHourOffset
	{
		get
		{
			string gameRegion = GameRegion;
			if (1 == 0)
			{
			}
			int result;
			switch (gameRegion)
			{
			case "us":
				result = -9;
				break;
			case "eu":
				result = -3;
				break;
			case "cn":
			case "cn_b":
			case "asia":
			case "twhkmo":
				result = 4;
				break;
			default:
				result = 4;
				break;
			}
			if (1 == 0)
			{
			}
			return result;
		}
	}

	/// <summary>
	/// 判断两个实例是否需要关闭当前游戏并按另一游戏路径重新启动。
	/// </summary>
	public static bool IsDifferentGamePath(OneDragonEnvironment environment, int currentInstanceIndex, int nextInstanceIndex)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		string text = LoadGamePath(environment, currentInstanceIndex);
		string text2 = LoadGamePath(environment, nextInstanceIndex);
		return !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && !string.Equals(text, text2, StringComparison.Ordinal);
	}

	private static string LoadGamePath(OneDragonEnvironment environment, int instanceIndex)
	{
		YamlConfig<GameAccountConfig> yamlConfig = new YamlConfig<GameAccountConfig>(environment, "game_account", null, instanceIndex);
		return yamlConfig.Current.GamePath;
	}
}
