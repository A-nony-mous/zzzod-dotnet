using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainChooseMissionOperation : ZOperation
{
	private readonly string _missionName;

	public WitheredDomainChooseMissionOperation(ZContext context, string missionName)
		: base(context, "枯萎之都 选择副本")
	{
		_missionName = missionName;
	}

	[OperationNode("选择副本", IsStartNode = true)]
	private OperationRoundResult ChooseMission()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("零号空洞-入口", "副本列表");
		Mat? lastScreenshot = base.LastScreenshot;
		string missionName = _missionName;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByOcrAndClick(lastScreenshot, missionName, area, 0.6, null, successDelay, retryDelay);
	}
}
