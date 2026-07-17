using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺流程。
/// </summary>
public interface IHouHouBakeryFlow
{
	/// <summary>
	/// 运行吼吼饼铺签到。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
