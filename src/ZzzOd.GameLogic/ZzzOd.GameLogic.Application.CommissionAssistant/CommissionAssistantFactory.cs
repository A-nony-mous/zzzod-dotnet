using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手应用 factory。
/// </summary>
public sealed class CommissionAssistantFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化委托助手 factory。
	/// </summary>
	public CommissionAssistantFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("commission_assistant", "委托助手", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		CommissionAssistantConfig config = (CommissionAssistantConfig)GetConfig(instanceIndex, groupId);
		return new CommissionAssistantApp(base.Context, config);
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return CommissionAssistantConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}
}
