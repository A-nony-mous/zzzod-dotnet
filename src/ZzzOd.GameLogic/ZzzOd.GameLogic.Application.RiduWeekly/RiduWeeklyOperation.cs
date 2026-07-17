using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.RiduWeekly;

/// <summary>
/// 丽都周纪领奖流程。
/// </summary>
public sealed class RiduWeeklyOperation : ZOperation
{
	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan ScheduleDelay = TimeSpan.FromSeconds(2L);

	private static readonly TimeSpan ScoreDelay = TimeSpan.FromMilliseconds(500L);

	private static readonly IReadOnlyList<IReadOnlyList<int>> ScoreColorRange = new IReadOnlyList<int>[2]
	{
		new int[3] { 250, 250, 250 },
		new int[3] { 255, 255, 255 }
	};

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	/// <summary>
	/// 初始化丽都周纪领奖流程。
	/// </summary>
	public RiduWeeklyOperation(ZContext context, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null)
		: base(context, "丽都周纪 (领奖励)")
	{
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
	}

	/// <summary>
	/// 执行前返回大世界，避免从菜单或弹窗状态开始。
	/// </summary>
	[OperationNode("返回大世界", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 前往快捷手册日常页面。
	/// </summary>
	[NodeFrom("返回大世界")]
	[OperationNode("日常")]
	public OperationRoundResult ChooseDaily()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = RetryDelay;
		return RoundByGotoScreen(lastScreenshot, "快捷手册-日常", null, null, retryDelay);
	}

	/// <summary>
	/// 点击丽都周纪入口。
	/// </summary>
	[NodeFrom("日常")]
	[OperationNode("丽都周纪")]
	public OperationRoundResult ClickSchedule()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = ScheduleDelay;
		TimeSpan? retryDelay = RetryDelay;
		return RoundByFindAndClickArea(lastScreenshot, "丽都周纪", "丽都周纪", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 领取三行积分中的 100 积分。
	/// </summary>
	[NodeFrom("丽都周纪")]
	[OperationNode("领取积分")]
	public OperationRoundResult ClaimScore()
	{
		OperationRoundResult operationRoundResult = null;
		for (int i = 1; i <= 3; i++)
		{
			OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("丽都周纪", $"积分行-{i}");
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = null;
			TimeSpan? retryDelay = ScoreDelay;
			IReadOnlyList<IReadOnlyList<int>> scoreColorRange = ScoreColorRange;
			OperationRoundResult operationRoundResult2 = RoundByOcrAndClick(lastScreenshot, "100", area, 1.0, null, successDelay, retryDelay, scoreColorRange);
			if (operationRoundResult2.IsSuccess)
			{
				return RoundWait(operationRoundResult2.Status, null, ScoreDelay);
			}
			operationRoundResult = operationRoundResult2;
		}
		return RoundRetry(operationRoundResult?.Status, null, ScoreDelay);
	}

	/// <summary>
	/// 找不到 100 积分后领取周纪奖励。
	/// </summary>
	[NodeFrom("领取积分", Success = false)]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone)]
	[OperationNode("领取奖励")]
	public OperationRoundResult ClaimReward()
	{
		TimeSpan? successDelay = RetryDelay;
		TimeSpan? retryDelay = RetryDelay;
		return RoundByClickArea("丽都周纪", "领取奖励", clickLeftTop: false, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 完成后回到大世界。
	/// </summary>
	[NodeFrom("领取奖励")]
	[OperationNode("完成后返回")]
	public async Task<OperationRoundResult> Finish()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}
