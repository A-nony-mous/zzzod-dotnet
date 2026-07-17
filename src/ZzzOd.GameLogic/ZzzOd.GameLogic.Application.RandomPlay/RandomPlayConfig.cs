using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业配置。
/// </summary>
public sealed class RandomPlayConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "transport_point", ApplyNamingConventions = false)]
	public string TransportPoint { get; set; } = RandomPlayTransportPoint.VideoStoreCounter.Value;

	[YamlMember(Alias = "agent_name_1", ApplyNamingConventions = false)]
	public string AgentName1 { get; set; } = "随机";

	[YamlMember(Alias = "agent_name_2", ApplyNamingConventions = false)]
	public string AgentName2 { get; set; } = "随机";

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static RandomPlayConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<RandomPlayConfig> yamlConfig = new YamlConfig<RandomPlayConfig>(environment, "random_play", null, instanceIndex, new string[2] { "app_config", groupId });
		RandomPlayConfig current = yamlConfig.Current;
		current.TransportPoint = RandomPlayTransportPoint.FromValue(current.TransportPoint).Value;
		current.ConfigureRuntime("random_play", instanceIndex, groupId);
		return current;
	}
}
