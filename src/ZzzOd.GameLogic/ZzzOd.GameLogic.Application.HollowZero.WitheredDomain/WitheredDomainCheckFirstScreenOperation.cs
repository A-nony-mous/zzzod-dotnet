using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainCheckFirstScreenOperation : ZOperation
{
	public WitheredDomainCheckFirstScreenOperation(ZContext context)
		: base(context, "枯萎之都 初始画面识别")
	{
	}

	[OperationNode("初始画面识别", IsStartNode = true)]
	private OperationRoundResult CheckFirstScreen()
	{
		Mat lastScreenshot = base.LastScreenshot;
		if (IsInHollow(lastScreenshot))
		{
			return RoundSuccess("在空洞内");
		}
		string a = CheckAndUpdateCurrentScreen(lastScreenshot, new string[] { "零号空洞-入口" });
		if (string.Equals(a, "零号空洞-入口", StringComparison.Ordinal))
		{
			return RoundSuccess("零号空洞-入口");
		}
		a = CheckAndUpdateCurrentScreen(lastScreenshot);
		if (a != null)
		{
			ScreenRoute screenRoute = base.ZContext.ScreenContext.GetScreenRoute(a, "快捷手册-作战");
			if (screenRoute != null && screenRoute.CanGo)
			{
				return RoundSuccess("可前往快捷手册");
			}
		}
		return RoundSuccess("未识别初始画面", null, TimeSpan.FromSeconds(1L));
	}

	private bool IsInHollow(Mat? screen)
	{
		if (screen == null)
		{
			return false;
		}
		string text = WitheredDomainOcrEventSource.DetectEventName(base.ZContext, screen);
		return !string.IsNullOrEmpty(text) && !string.Equals(text, HollowZeroSpecialEvent.OldCapital.EventName, StringComparison.Ordinal);
	}
}
