using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ScratchCard;

/// <summary>
/// 刮刮卡应用流程。
/// </summary>
public interface IScratchCardAppFlow
{
	/// <summary>
	/// 运行刮刮卡流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
