using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业应用流程。
/// </summary>
public interface IRandomPlayAppFlow
{
	/// <summary>
	/// 运行录像店营业流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, RandomPlayConfig config, RandomPlayRunRecord runRecord, CancellationToken cancellationToken);
}
