using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 默认预备编队角色识别 Operation 流程。
/// </summary>
public sealed class OperationPredefinedTeamCheckerFlow : IPredefinedTeamCheckerFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		PredefinedTeamCheckerOperation predefinedTeamCheckerOperation = new PredefinedTeamCheckerOperation(context);
		return predefinedTeamCheckerOperation.ExecuteAsync(cancellationToken);
	}
}
