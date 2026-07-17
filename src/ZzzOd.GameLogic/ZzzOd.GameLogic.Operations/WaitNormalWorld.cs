using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 等待进入 ZZZ 大世界画面。
/// </summary>
public sealed class WaitNormalWorld : ZOperation
{
	private static readonly string[] WorldScreens = new string[2] { "大世界-普通", "大世界-勘域" };

	private readonly bool _checkOnce;

	/// <summary>
	/// 初始化等待大世界操作。
	/// </summary>
	public WaitNormalWorld(ZContext context, bool checkOnce = false)
		: base(context, "等待大世界画面")
	{
		_checkOnce = checkOnce;
	}

	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private OperationRoundResult CheckScreen()
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, WorldScreens);
		if (text != null && WorldScreens.Contains<string>(text, StringComparer.Ordinal))
		{
			return RoundSuccess(text);
		}
		OperationRoundResult operationRoundResult = RoundByFindAreaBinary(base.LastScreenshot, "大世界", "信息");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "大世界", "星期");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status);
		}
		if (base.LastScreenshot != null && HasNormalWorldOcrText(base.LastScreenshot))
		{
			return RoundSuccess("大世界");
		}
		return _checkOnce ? RoundFail("未到达大世界") : RoundRetry("未到达大世界", null, TimeSpan.FromSeconds(1L));
	}

	private bool HasNormalWorldOcrText(Mat screen)
	{
		string[] source = new string[3] { "前往任务目标", "快捷轮盘", "相机" };
		IReadOnlyList<OcrMatchResult> results = base.ZContext.OcrService.GetOcrResultList(screen);
		return source.Any((string targetWord) => results.Any((OcrMatchResult result) => StringUtils.FindByLcs(targetWord, result.Text, 0.6)));
	}
}
