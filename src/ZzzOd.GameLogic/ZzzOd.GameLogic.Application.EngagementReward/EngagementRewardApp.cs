using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 活跃度奖励应用。
/// </summary>
public sealed class EngagementRewardApp : ZApplication
{
	private readonly IEngagementRewardAppFlow _flow;

	/// <summary>
	/// 初始化活跃度奖励应用。
	/// </summary>
	public EngagementRewardApp(ZContext context, ZApplicationRunRecord? runRecord = null, IEngagementRewardAppFlow? flow = null)
		: base(context, "engagement_reward", runRecord, "活跃度奖励")
	{
		_flow = flow ?? new OperationEngagementRewardAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("engagement_reward");
		try
		{
			return await _flow.RunAsync(base.Context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}
}
