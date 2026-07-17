using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线应用流程。
/// </summary>
public interface ILifeOnLineAppFlow
{
	/// <summary>
	/// 运行生命热线流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, LifeOnLineConfig config, LifeOnLineRunRecord runRecord, CancellationToken cancellationToken);
}
