using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别应用。
/// </summary>
public sealed class PredefinedTeamCheckerApp : ZApplication
{
	private readonly IPredefinedTeamCheckerFlow _flow;

	/// <summary>
	/// 初始化预备编队角色识别应用。
	/// </summary>
	public PredefinedTeamCheckerApp(ZContext context, ZApplicationRunRecord? runRecord = null, IPredefinedTeamCheckerFlow? flow = null)
		: base(context, "predefined_team_checker", runRecord, "预备编队角色识别")
	{
		_flow = flow ?? new OperationPredefinedTeamCheckerFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, cancellationToken);
	}
}
