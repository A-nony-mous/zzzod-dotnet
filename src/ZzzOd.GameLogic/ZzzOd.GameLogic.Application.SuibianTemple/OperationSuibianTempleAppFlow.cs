using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 默认随便。Operation 流程。
/// </summary>
public sealed class OperationSuibianTempleAppFlow : ISuibianTempleAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, SuibianTempleConfig config, CancellationToken cancellationToken)
	{
		SuibianTempleOperation suibianTempleOperation = new SuibianTempleOperation(context, config);
		return suibianTempleOperation.ExecuteAsync(cancellationToken);
	}
}
