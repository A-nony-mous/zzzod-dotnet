using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;

/// <summary>
/// 闪避助手应用 factory。
/// </summary>
public sealed class DodgeAssistantFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化闪避助手 factory。
	/// </summary>
	public DodgeAssistantFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("dodge_assistant", "闪避助手", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new DodgeAssistantApp(base.Context);
	}
}
