using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地应用流程扩展点。
/// </summary>
public interface IWorldPatrolAppFlow
{
	/// <summary>运行锄大地。</summary>
	Task<OperationResult> RunAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRunRecord runRecord, CancellationToken cancellationToken);
}
