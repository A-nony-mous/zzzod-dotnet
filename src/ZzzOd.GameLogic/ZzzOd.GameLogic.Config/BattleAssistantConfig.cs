using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 战斗助手配置。
/// </summary>
public sealed class BattleAssistantConfig : IApplicationConfig
{
	public const string ModuleName = "battle_assistant";

	public const string ControlMethodKeyboard = "keyboard";

	public const string ControlMethodXbox = "xbox";

	public const string ControlMethodDs4 = "ds4";

	private string _controlMethod = "keyboard";

	[YamlMember(Alias = "dodge_assistant_config", ApplyNamingConventions = false)]
	public string DodgeAssistantConfig { get; set; } = "闪避";

	[YamlMember(Alias = "screenshot_interval", ApplyNamingConventions = false)]
	public double ScreenshotInterval { get; set; } = 0.02;

	[YamlMember(Alias = "control_method", ApplyNamingConventions = false)]
	public string ControlMethod
	{
		get
		{
			return _controlMethod;
		}
		set
		{
			_controlMethod = NormalizeControlMethod(value);
		}
	}

	[YamlMember(Alias = "auto_battle_config", ApplyNamingConventions = false)]
	public string AutoBattleConfig { get; set; } = "全配队通用";

	[YamlMember(Alias = "use_merged_file", ApplyNamingConventions = false)]
	public bool UseMergedFile { get; set; } = true;

	[YamlMember(Alias = "auto_ultimate_enabled", ApplyNamingConventions = false)]
	public bool AutoUltimateEnabled { get; set; }

	[YamlMember(Alias = "battle_replay_enabled", ApplyNamingConventions = false)]
	public bool BattleReplayEnabled { get; set; }

	public static BattleAssistantConfig Load(OneDragonEnvironment environment, int instanceIndex)
	{
		YamlConfig<BattleAssistantConfig> yamlConfig = new YamlConfig<BattleAssistantConfig>(environment, "battle_assistant", null, instanceIndex);
		return yamlConfig.Current;
	}

	public static string NormalizeControlMethod(string? value)
	{
		string text = value?.Trim().ToLowerInvariant();
		if (1 == 0)
		{
		}
		string result = text switch
		{
			"xbox" => "xbox", 
			"ds4" => "ds4", 
			"keyboard" => "keyboard", 
			"键鼠" => "keyboard", 
			null => "keyboard", 
			_ => text, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
