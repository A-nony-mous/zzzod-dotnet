using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleEntryNavigation : SuibianTempleSubOperation
{
	public SuibianTempleEntryNavigation(ZContext context, SuibianTempleConfig config)
		: base(context, config, "前往随便观")
	{
	}

	[OperationNode("前往随便观", IsStartNode = true, TimeoutSeconds = 60.0, NodeMaxRetryTimes = 999)]
	public OperationRoundResult GoToSuibianTemple()
	{
		OperationRoundResult operationRoundResult = ClickText("前往随便观", "确认", "领取收益");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-入口" });
		if (text != null)
		{
			return RoundSuccess(text);
		}
		if (base.Config.AutoManageEnabled)
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
			OperationRoundResult operationRoundResult2 = RoundByOcr(lastScreenshot, "开始托管", null, 0.75, null, retryDelay);
			if (operationRoundResult2.IsSuccess)
			{
				return RoundSuccess(operationRoundResult2.Status);
			}
		}
		OperationRoundResult operationRoundResult3 = FindAndClickArea("菜单", "返回");
		return operationRoundResult3.IsSuccess ? RoundWait(operationRoundResult3.Status, null, SuibianTempleSubOperation.OneSecond) : RoundRetry("未识别当前画面", null, SuibianTempleSubOperation.OneSecond);
	}
}
