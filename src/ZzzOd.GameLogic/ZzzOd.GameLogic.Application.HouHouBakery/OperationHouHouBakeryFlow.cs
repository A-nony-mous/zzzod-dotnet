using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 默认吼吼饼铺 Operation 流程。
/// </summary>
public sealed class OperationHouHouBakeryFlow : IHouHouBakeryFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		HouHouBakeryOperation houHouBakeryOperation = new HouHouBakeryOperation(context);
		return houHouBakeryOperation.ExecuteAsync(cancellationToken);
	}
}
