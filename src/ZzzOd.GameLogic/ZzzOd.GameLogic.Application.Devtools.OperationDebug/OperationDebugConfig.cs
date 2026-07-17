using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试配置。
/// </summary>
public sealed class OperationDebugConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "operation_template", ApplyNamingConventions = false)]
	public string OperationTemplate { get; set; } = "安比-3A特殊攻击";

	[YamlMember(Alias = "repeat_enabled", ApplyNamingConventions = false)]
	public bool RepeatEnabled { get; set; } = true;

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static OperationDebugConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<OperationDebugConfig> yamlConfig = new YamlConfig<OperationDebugConfig>(environment, "operation_debug", null, instanceIndex, new string[2] { "app_config", groupId });
		OperationDebugConfig current = yamlConfig.Current;
		current.ConfigureRuntime("operation_debug", instanceIndex, groupId);
		return current;
	}
}
