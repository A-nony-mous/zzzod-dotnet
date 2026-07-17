using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 默认式舆防卫。Operation 流程。
/// </summary>
public sealed class OperationShiyuDefenseAppFlow : IShiyuDefenseAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, CancellationToken cancellationToken)
	{
		ShiyuDefenseOperation shiyuDefenseOperation = new ShiyuDefenseOperation(context, config, runRecord);
		return shiyuDefenseOperation.ExecuteAsync(cancellationToken);
	}
}
