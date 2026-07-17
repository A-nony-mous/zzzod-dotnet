using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板应用配置。
/// </summary>
public sealed class IntelBoardConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "predefined_team_idx", ApplyNamingConventions = false)]
	public int PredefinedTeamIndex { get; set; } = -1;

	[YamlMember(Alias = "auto_battle_config", ApplyNamingConventions = false)]
	public string AutoBattleConfig { get; set; } = "全配队通用";

	[YamlMember(Alias = "exp_grind_mode", ApplyNamingConventions = false)]
	public bool ExpGrindMode { get; set; }

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static IntelBoardConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<IntelBoardConfig> yamlConfig = new YamlConfig<IntelBoardConfig>(environment, "intel_board", null, instanceIndex, new string[2] { "app_config", groupId });
		IntelBoardConfig current = yamlConfig.Current;
		current.ConfigureRuntime("intel_board", instanceIndex, groupId);
		return current;
	}
}
