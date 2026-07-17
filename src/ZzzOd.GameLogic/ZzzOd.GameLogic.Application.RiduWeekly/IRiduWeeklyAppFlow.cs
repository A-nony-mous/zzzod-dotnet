using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RiduWeekly;

/// <summary>
/// 丽都周纪应用流程。
/// </summary>
public interface IRiduWeeklyAppFlow
{
	/// <summary>
	/// 运行丽都周纪领奖流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);
}
