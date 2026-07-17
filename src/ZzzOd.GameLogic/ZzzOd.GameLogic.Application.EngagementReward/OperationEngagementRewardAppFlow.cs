using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 默认活跃度奖。Operation 流程。
/// </summary>
public sealed class OperationEngagementRewardAppFlow : IEngagementRewardAppFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		EngagementRewardOperation engagementRewardOperation = new EngagementRewardOperation(context);
		return engagementRewardOperation.ExecuteAsync(cancellationToken);
	}
}
