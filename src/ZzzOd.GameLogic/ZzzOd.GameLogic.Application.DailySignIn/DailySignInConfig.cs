using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 每日签到配置。
/// </summary>
public sealed class DailySignInConfig : ZApplicationConfig, IApplicationConfig
{
	/// <summary>
	/// 选择签到的子应用 id，默认为吼吼饼铺。
	/// </summary>
	[YamlMember(Alias = "selected_sign", ApplyNamingConventions = false)]
	public string SelectedSign { get; set; } = "hou_hou_bakery";

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static DailySignInConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<DailySignInConfig> yamlConfig = new YamlConfig<DailySignInConfig>(environment, "daily_signin", null, instanceIndex, new string[2] { "app_config", groupId });
		DailySignInConfig current = yamlConfig.Current;
		current.ConfigureRuntime("daily_signin", instanceIndex, groupId);
		return current;
	}
}
