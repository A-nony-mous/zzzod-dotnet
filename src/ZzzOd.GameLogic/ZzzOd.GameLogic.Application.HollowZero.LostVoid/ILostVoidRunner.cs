using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地 runner。
/// </summary>
public interface ILostVoidRunner
{
	/// <summary>运行指定类型的一层迷失之地。</summary>
	Task<OperationResult> RunLevelAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken);
}
