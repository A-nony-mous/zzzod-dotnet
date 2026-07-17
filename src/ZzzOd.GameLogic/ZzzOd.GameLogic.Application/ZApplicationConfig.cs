using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用配置基类。
/// </summary>
public class ZApplicationConfig : IApplicationConfig
{
	/// <summary>
	/// 应用 id。
	/// </summary>
	[YamlIgnore]
	public string AppId { get; private set; } = string.Empty;

	/// <summary>
	/// 实例编号。
	/// </summary>
	[YamlIgnore]
	public int InstanceIndex { get; private set; }

	/// <summary>
	/// 分组 id。
	/// </summary>
	[YamlIgnore]
	public string GroupId { get; private set; } = "default";

	/// <summary>
	/// 从应用配置目录加载 YAML。
	/// </summary>
	public static T Load<T>(OneDragonEnvironment environment, string appId, int instanceIndex, string groupId) where T : ZApplicationConfig, new()
	{
		YamlConfig<T> yamlConfig = new YamlConfig<T>(environment, appId, null, instanceIndex, new string[2] { "app_config", groupId });
		T current = yamlConfig.Current;
		current.ConfigureRuntime(appId, instanceIndex, groupId);
		return current;
	}

	/// <summary>
	/// 配置运行时元数据。
	/// </summary>
	protected void ConfigureRuntime(string appId, int instanceIndex, string groupId)
	{
		AppId = appId;
		InstanceIndex = instanceIndex;
		GroupId = groupId;
	}
}
