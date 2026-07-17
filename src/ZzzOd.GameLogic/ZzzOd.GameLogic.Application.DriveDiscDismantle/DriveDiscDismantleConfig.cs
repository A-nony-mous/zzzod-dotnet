using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解配置。
/// </summary>
public sealed class DriveDiscDismantleConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "dismantle_level", ApplyNamingConventions = false)]
	public string DismantleLevel { get; set; } = "A及以下";

	[YamlMember(Alias = "dismantle_abandon", ApplyNamingConventions = false)]
	public bool DismantleAbandon { get; set; }

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static DriveDiscDismantleConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<DriveDiscDismantleConfig> yamlConfig = new YamlConfig<DriveDiscDismantleConfig>(environment, "drive_disc_dismantle", null, instanceIndex, new string[2] { "app_config", groupId });
		DriveDiscDismantleConfig current = yamlConfig.Current;
		current.ConfigureRuntime("drive_disc_dismantle", instanceIndex, groupId);
		return current;
	}
}
