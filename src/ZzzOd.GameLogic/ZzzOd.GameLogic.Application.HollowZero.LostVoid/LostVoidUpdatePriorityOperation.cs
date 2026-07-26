using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidUpdatePriorityOperation : ZOperation
{
	public LostVoidUpdatePriorityOperation(ZContext context)
		: base(context, "更新动态优先级")
	{
	}

	[OperationNode("进入藏品页面", IsStartNode = true)]
	private OperationRoundResult EnterCollections()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(null, "迷失之地-大世界", "迷失之地-TAB", null, null, null);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(null, "迷失之地-藏品面板", "藏品", null, null, null);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
	}

	[NodeFrom("进入藏品页面")]
	[OperationNode("识别并存储优先级")]
	private OperationRoundResult RecognizeAndStore()
	{
		if (base.LastScreenshot == null)
		{
			return RoundFail("未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-藏品面板", "藏品清单");
		if (area == null)
		{
			return RoundFail("区域未配置 藏品清单");
		}
		using Mat mat = CvImageUtils.Crop(base.LastScreenshot, area.Rect);
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.BGR2HSV);
		using Mat mat3 = new Mat();
		Cv2.InRange(mat2, new Scalar(0.0, 0.0, 55.0), new Scalar(180.0, 255.0, 255.0), mat3);
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultListForCrop(
			mat3,
			base.LastScreenshot.Width,
			base.LastScreenshot.Height,
			area.X1,
			area.Y1);
		IReadOnlyList<string> readOnlyList = LostVoidPriorityUpdater.ExtractDynamicPriorities(LostVoidPriorityUpdater.FromOcrResults(ocrResultList));
		LostVoidPriorityUpdater.AppendDynamicPriorities(base.ZContext.LostVoid, readOnlyList);
		base.ZContext.Logger.Information("迷失之地动态优先级识别: Ocr={Ocr}, Added={Added}, Current={Current}", string.Join(" | ", ocrResultList.Select((OcrMatchResult result) => result.Text)), string.Join(", ", readOnlyList), string.Join(", ", base.ZContext.LostVoid.DynamicPriorityList));
		return RoundSuccess((readOnlyList.Count == 0) ? "未发现需优先的藏品" : "动态优先级存储成功");
	}

	[NodeFrom("识别并存储优先级")]
	[OperationNode("关闭菜单")]
	private OperationRoundResult CloseMenu()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-藏品面板", "返回按钮") };
		return RoundByFindAndClickArea(null, "迷失之地-藏品面板", "返回按钮", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}
}
