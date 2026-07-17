using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.CityFund;

/// <summary>
/// 丽都城募领取流程。
/// </summary>
public sealed class CityFundOperation : ZOperation
{
	private static readonly TimeSpan WaitDelay = TimeSpan.FromSeconds(1L);

	/// <summary>
	/// 初始化丽都城募领取流程。
	/// </summary>
	public CityFundOperation(ZContext context)
		: base(context, "丽都城募", 1)
	{
	}

	/// <summary>
	/// 打开菜单。
	/// </summary>
	[OperationNode("打开菜单", IsStartNode = true)]
	public async Task<OperationRoundResult> OpenMenu()
	{
		GotoMenu operation = new GotoMenu(base.ZContext);
		return RoundByOperationResult(await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 点击丽都城募。
	/// </summary>
	[NodeFrom("打开菜单")]
	[OperationNode("点击丽都城募")]
	public OperationRoundResult ClickFund()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("菜单", "底部列表");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByOcrAndClick(lastScreenshot, "丽都城募", area, 0.6, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 点击成长任务。
	/// </summary>
	[NodeFrom("点击丽都城募")]
	[NodeFrom("点击成长任务", Status = "按钮-确认")]
	[OperationNode("点击成长任务")]
	public OperationRoundResult ClickTask()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "丽都城募", "开启丽都城募", null, successDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, WaitDelay);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		successDelay = WaitDelay;
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(lastScreenshot2, "丽都城募", "按钮-确认", null, successDelay);
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status, null, WaitDelay);
		}
		Mat? lastScreenshot3 = base.LastScreenshot;
		successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot3, "丽都城募", "成长任务", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 任务全部领取。
	/// </summary>
	[NodeFrom("点击成长任务")]
	[OperationNode("任务全部领取")]
	public OperationRoundResult ClickTaskClaim()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot, "丽都城募", "任务-全部领取", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 点击等级回馈。
	/// </summary>
	[NodeFrom("任务全部领取")]
	[OperationNode("点击等级回馈")]
	public OperationRoundResult ClickLevel()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot, "丽都城募", "等级回馈", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 等级全部领取。
	/// </summary>
	[NodeFrom("点击等级回馈")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
	[OperationNode("等级全部领取")]
	public OperationRoundResult ClickLevelClaim()
	{
		foreach (var levelClaimArea in CityFundClaimAreas.LevelClaimAreas)
		{
			string item = levelClaimArea.ScreenName;
			string item2 = levelClaimArea.AreaName;
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = WaitDelay;
			OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, item, item2, null, successDelay);
			if (operationRoundResult.IsSuccess)
			{
				return RoundRetry(operationRoundResult.Status, null, WaitDelay);
			}
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("等级全部领取")]
	[NodeFrom("等级全部领取", Success = false)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		BackToNormalWorld operation = new BackToNormalWorld(base.ZContext);
		return RoundByOperationResult(await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}
}
