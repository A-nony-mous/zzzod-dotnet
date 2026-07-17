using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都完成后的返回节点。
/// </summary>
internal sealed class WitheredDomainFinishOperation : ZOperation
{
	private readonly IWitheredDomainAppActions _actions;

	private CancellationToken _cancellationToken;

	public WitheredDomainFinishOperation(ZContext context, IWitheredDomainAppActions actions)
		: base(context, "枯萎之都 完成")
	{
		_actions = actions;
	}

	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	[OperationNode("完成后等待加载", IsStartNode = true)]
	private async Task<OperationRoundResult> WaitBackLoadingAsync()
	{
		return RoundByOperationResult(await _actions.WaitBackLoadingAsync(base.ZContext, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("完成后等待加载")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone, Detail = true)]
	[OperationNode("完成")]
	private async Task<OperationRoundResult> FinishAsync()
	{
		return RoundByOperationResult(await _actions.FinishAsync(base.ZContext, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}
}
