using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地应用流程。
/// </summary>
public interface ILostVoidAppFlow
{
	Task<OperationResult> RunAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, CancellationToken cancellationToken);
}
