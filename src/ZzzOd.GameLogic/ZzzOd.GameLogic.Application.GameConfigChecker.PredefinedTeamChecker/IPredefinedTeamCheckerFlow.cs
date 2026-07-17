using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;

/// <summary>
/// 预备编队角色识别流程。
/// </summary>
public interface IPredefinedTeamCheckerFlow
{
	/// <summary>
	/// 运行预备编队角色识别流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
