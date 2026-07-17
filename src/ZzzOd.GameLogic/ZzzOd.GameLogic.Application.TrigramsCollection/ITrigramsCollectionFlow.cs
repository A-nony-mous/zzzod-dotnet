using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录流程。
/// </summary>
public interface ITrigramsCollectionFlow
{
	/// <summary>
	/// 运行卦象集录流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
