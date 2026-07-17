using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用流程。
/// </summary>
public interface IShiyuDefenseAppFlow
{
	/// <summary>
	/// 运行式舆防卫战。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, CancellationToken cancellationToken);
}
