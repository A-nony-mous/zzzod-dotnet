using System.IO;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手配置。
/// </summary>
public sealed class CommissionAssistantConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "pause_in_background", ApplyNamingConventions = false)]
	public bool PauseInBackground { get; set; } = true;

	[YamlMember(Alias = "dialog_click_interval", ApplyNamingConventions = false)]
	public double DialogClickInterval { get; set; } = 0.5;

	[YamlMember(Alias = "story_mode", ApplyNamingConventions = false)]
	public string StoryMode { get; set; } = CommissionAssistantStoryMode.Click.Value;

	[YamlMember(Alias = "dialog_option", ApplyNamingConventions = false)]
	public string DialogOption { get; set; } = CommissionAssistantDialogOption.Last.Value;

	[YamlMember(Alias = "dodge_config", ApplyNamingConventions = false)]
	public string DodgeConfig { get; set; } = "闪避";

	[YamlMember(Alias = "dodge_switch", ApplyNamingConventions = false)]
	public string DodgeSwitch { get; set; } = "5";

	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; } = "全配队通用";

	[YamlMember(Alias = "auto_battle_switch", ApplyNamingConventions = false)]
	public string AutoBattleSwitch { get; set; } = "6";

	[YamlMember(Alias = "sleep_after_empty_screen", ApplyNamingConventions = false)]
	public double SleepAfterEmptyScreen { get; set; } = 0.5;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static CommissionAssistantConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		string pathUnderWorkDir = environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), groupId);
		string text = Path.Combine(pathUnderWorkDir, "screenshot_helper.yml");
		string pathUnderWorkDir2 = environment.GetPathUnderWorkDir("config", instanceIndex.ToString("00"), "screenshot_helper.yml");
		if (!File.Exists(text) && File.Exists(pathUnderWorkDir2))
		{
			Directory.CreateDirectory(pathUnderWorkDir);
			File.Copy(pathUnderWorkDir2, text, overwrite: false);
		}
		YamlConfig<CommissionAssistantConfig> yamlConfig = new YamlConfig<CommissionAssistantConfig>(environment, "screenshot_helper", null, instanceIndex, new string[] { groupId });
		CommissionAssistantConfig current = yamlConfig.Current;
		current.ConfigureRuntime("commission_assistant", instanceIndex, groupId);
		return current;
	}
}
