using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 卦象集录流程。
/// </summary>
public sealed class TrigramsCollectionOperation : ZOperation
{
	private readonly ITrigramsCollectionOperationServices _services;

	private bool _claimReward;

	/// <summary>
	/// 本次运行是否已获取卦象。
	/// </summary>
	public bool ClaimReward => _claimReward;

	/// <summary>
	/// 初始化卦象集录流程。
	/// </summary>
	public TrigramsCollectionOperation(ZContext context, ITrigramsCollectionOperationServices? services = null)
		: base(context, "卦象集录")
	{
		_services = services ?? new DefaultTrigramsCollectionOperationServices();
	}

	/// <summary>
	/// 传送。
	/// </summary>
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _services.TransportAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 移动交互。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("移动交互")]
	public OperationRoundResult MoveAndInteract()
	{
		OperationResult operationResult = _services.Interact(base.ZContext);
		if (!operationResult.IsSuccess)
		{
			return RoundFail(operationResult.Status);
		}
		return RoundSuccess(null, null, TimeSpan.FromSeconds(3L));
	}

	/// <summary>
	/// 获取卦象。
	/// </summary>
	[NodeFrom("移动交互")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone)]
	[OperationNode("获取卦象", NodeMaxRetryTimes = 10)]
	public async Task<OperationRoundResult> GetTrigram()
	{
		TrigramOcrMatch match = await _services.ReadPriorityTextAsync(base.ZContext, base.LastScreenshot, new string[3] { "卦象集录", "滑动屏幕以获取卦象", "确认" }).ConfigureAwait(continueOnCapturedContext: false);
		if (match?.Word == "卦象集录")
		{
			if (_claimReward)
			{
				return RoundSuccess(match.Word);
			}
			await _services.ClickGetTrigramAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
			return RoundWait(match.Word, null, TimeSpan.FromSeconds(1L));
		}
		if (match?.Word == "滑动屏幕以获取卦象")
		{
			_services.DragForTrigram(base.ZContext);
			return RoundWait(match.Word);
		}
		if (match?.Word == "确认")
		{
			_claimReward = true;
			await _services.ClickConfirmAsync(base.ZContext, match.Center).ConfigureAwait(continueOnCapturedContext: false);
			return RoundWait(match.Word, null, TimeSpan.FromSeconds(1L));
		}
		return RoundRetry("未识别目标文本", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 结束后返回。
	/// </summary>
	[NodeFrom("获取卦象")]
	[OperationNode("结束后返回")]
	public async Task<OperationRoundResult> BackAtLast()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}
}
