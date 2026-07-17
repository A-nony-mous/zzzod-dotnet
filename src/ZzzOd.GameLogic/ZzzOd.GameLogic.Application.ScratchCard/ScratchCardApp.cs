using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ScratchCard;

/// <summary>
/// 刮刮卡应用。
/// </summary>
public sealed class ScratchCardApp : ZApplication
{
	private readonly IScratchCardAppFlow _flow;

	/// <summary>
	/// 初始化刮刮卡应用。
	/// </summary>
	public ScratchCardApp(ZContext context, ZApplicationRunRecord? runRecord = null, IScratchCardAppFlow? flow = null)
		: base(context, "scratch_card", runRecord, "刮刮卡")
	{
		_flow = flow ?? new OperationScratchCardAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("scratch_card");
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
