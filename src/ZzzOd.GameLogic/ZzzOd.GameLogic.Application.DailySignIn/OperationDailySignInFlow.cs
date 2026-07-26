using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.DailySignIn;

/// <summary>
/// 默认每日签到 Operation 流程。
/// </summary>
public sealed class OperationDailySignInFlow : IDailySignInFlow
{
	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, DailySignInConfig config, int instanceIndex, string groupId, CancellationToken cancellationToken)
	{
		DailySignInOperation dailySignInOperation = new DailySignInOperation(context, config, instanceIndex, groupId);
		return dailySignInOperation.ExecuteAsync(cancellationToken);
	}
}
