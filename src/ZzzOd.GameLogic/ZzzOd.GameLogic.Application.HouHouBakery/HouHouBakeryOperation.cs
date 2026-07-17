using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺签到流程。
/// </summary>
public sealed class HouHouBakeryOperation : ZOperation
{
	private readonly IHouHouBakeryOperationServices _services;

	private bool _claimed;

	/// <summary>
	/// 本次运行是否已点击领取卡片。
	/// </summary>
	public bool Claimed => _claimed;

	/// <summary>
	/// 初始化吼吼饼铺签到流程。
	/// </summary>
	public HouHouBakeryOperation(ZContext context, IHouHouBakeryOperationServices? services = null)
		: base(context, "吼吼饼铺")
	{
		_services = services ?? new DefaultHouHouBakeryOperationServices();
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
	/// 领取奖励。
	/// </summary>
	[NodeFrom("移动交互")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone)]
	[OperationNode("领取奖励", NodeMaxRetryTimes = 20)]
	public async Task<OperationRoundResult> Collect()
	{
		if (await _services.RecognizeTextAsync(base.ZContext, base.LastScreenshot, "同类型奖励").ConfigureAwait(continueOnCapturedContext: false))
		{
			return RoundSuccess(_claimed ? "领取成功" : "今日已领取", null, TimeSpan.FromSeconds(1L));
		}
		string[] array = new string[2] { "确定", "确认" };
		foreach (string confirmWord in array)
		{
			if (await _services.RecognizeTextAsync(base.ZContext, base.LastScreenshot, confirmWord).ConfigureAwait(continueOnCapturedContext: false) && (await _services.ClickTextAsync(base.ZContext, base.LastScreenshot, confirmWord).ConfigureAwait(continueOnCapturedContext: false)).IsSuccess)
			{
				_claimed = true;
				return RoundWait(confirmWord, null, TimeSpan.FromSeconds(1L));
			}
		}
		if (await _services.RecognizeTextAsync(base.ZContext, base.LastScreenshot, "查看今天").ConfigureAwait(continueOnCapturedContext: false))
		{
			OperationResult result = _services.ClickCenter(base.ZContext);
			if (!result.IsSuccess)
			{
				return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
			}
			return RoundWait("点击盲盒", null, TimeSpan.FromSeconds(1L));
		}
		if (await _services.RecognizeTextAsync(base.ZContext, base.LastScreenshot, "每日可领取一次").ConfigureAwait(continueOnCapturedContext: false))
		{
			return (await _services.ClickBlindBoxAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false)).IsSuccess ? RoundWait("选择盲盒", null, TimeSpan.FromSeconds(1L)) : RoundRetry("盲盒区域点击失败", null, TimeSpan.FromSeconds(1L));
		}
		return RoundRetry("未识别目标文本", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("领取奖励")]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}
}
