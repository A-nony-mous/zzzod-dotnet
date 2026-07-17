using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

internal sealed class WitheredDomainFindAreaOperation : ZOperation
{
	private readonly string _screenName;

	private readonly string _areaName;

	public WitheredDomainFindAreaOperation(ZContext context, string operationName, string screenName, string areaName, int nodeMaxRetryTimes)
		: base(context, operationName, nodeMaxRetryTimes)
	{
		_screenName = screenName;
		_areaName = areaName;
	}

	[OperationNode("查找区域", IsStartNode = true)]
	private OperationRoundResult FindArea()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		string screenName = _screenName;
		string areaName = _areaName;
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindArea(lastScreenshot, screenName, areaName, null, retryDelay);
	}
}
