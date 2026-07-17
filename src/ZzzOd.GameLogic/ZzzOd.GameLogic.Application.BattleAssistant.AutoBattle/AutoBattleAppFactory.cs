using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 自动战斗应用 factory。
/// </summary>
public sealed class AutoBattleAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化自动战斗 factory。
	/// </summary>
	public AutoBattleAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("auto_battle", "自动战斗", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new AutoBattleApp(base.Context);
	}
}
