using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CityFund;

/// <summary>
/// 丽都城募应用流程。
/// </summary>
public interface ICityFundAppFlow
{
	/// <summary>
	/// 运行丽都城募领取流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
