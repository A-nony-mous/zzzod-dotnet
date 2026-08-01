using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidLotteryOperation : ZOperation
{
	internal static IReadOnlyList<string>? CurrentScreenCandidates => null;

	public const string StatusNoTimesLeft = "无剩余次数";

	public const string StatusContinue = "继续抽奖";

	private readonly LostVoidInteractService _service;

	public LostVoidLotteryOperation(ZContext context, LostVoidInteractService? service = null)
		: base(context, "迷失之地-抽奖机")
	{
		_service = service ?? LostVoidInteractService.Instance;
	}

	[NodeFrom("点击后确定", Status = "继续抽奖")]
	[OperationNode("点击开始", IsStartNode = true)]
	public OperationRoundResult ClickStart()
	{
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-抽奖机", "文本-剩余次数");
		IReadOnlyList<string> ocrTexts = (from result in base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area?.ColorRange, area?.Rect)
			select result.Text).ToArray();
		if (!_service.HasLotteryTimesLeft(ocrTexts))
		{
			return RoundSuccess("无剩余次数");
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(4L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-抽奖机", "按钮-开始", null, successDelay, retryDelay);
	}

	[NodeFrom("点击开始")]
	[OperationNode("点击后确定")]
	public OperationRoundResult ConfirmAfterClick()
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, CurrentScreenCandidates);
		if (text == "迷失之地-通用选择")
		{
			LostVoidChooseCommonOperation lostVoidChooseCommonOperation = new LostVoidChooseCommonOperation(base.ZContext);
			OperationResult result = lostVoidChooseCommonOperation.ExecuteAsync().GetAwaiter().GetResult();
			return result.IsSuccess ? RoundWait(result.Status, null, TimeSpan.FromSeconds(1L)) : RoundFail(result.Status);
		}
		if (text == "迷失之地-抽奖机")
		{
			return RoundSuccess("继续抽奖");
		}
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "迷失之地-抽奖机", "按钮-获取确定");
		return operationRoundResult.IsSuccess ? RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry("未能识别当前画面", null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("点击开始", Status = "无剩余次数")]
	[NodeFrom("点击后确定", Success = false)]
	[OperationNode("返回大世界")]
	public OperationRoundResult BackToWorld()
	{
		if (base.LastScreenshot != null && LostVoidMoveByDetectionService.Instance.IsInNormalWorld(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess();
		}
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "迷失之地-抽奖机", "按钮-返回");
		return RoundRetry(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
	}
}
