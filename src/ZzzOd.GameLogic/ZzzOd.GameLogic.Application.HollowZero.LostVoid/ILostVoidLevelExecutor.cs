using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public interface ILostVoidLevelExecutor
{
	Task<OperationResult> RunLevelAsync(ZContext context, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken);
}
