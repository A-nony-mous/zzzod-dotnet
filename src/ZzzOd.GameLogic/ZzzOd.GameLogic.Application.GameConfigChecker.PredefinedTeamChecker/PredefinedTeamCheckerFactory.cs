using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别 factory。
/// </summary>
public sealed class PredefinedTeamCheckerFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public PredefinedTeamCheckerFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("predefined_team_checker", "预备编队角色识别", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new PredefinedTeamCheckerApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}
}
