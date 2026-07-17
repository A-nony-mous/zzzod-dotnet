using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RiduWeekly;

/// <summary>
/// 丽都周纪应用。
/// </summary>
public sealed class RiduWeeklyApp : ZApplication
{
	private readonly IRiduWeeklyAppFlow _flow;

	/// <summary>
	/// 初始化丽都周纪应用。
	/// </summary>
	public RiduWeeklyApp(ZContext context, ZApplicationRunRecord? runRecord = null, IRiduWeeklyAppFlow? flow = null)
		: base(context, "ridu_weekly", runRecord, "丽都周纪 (领奖励)")
	{
		_flow = flow ?? new OperationRiduWeeklyAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("ridu_weekly");
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
