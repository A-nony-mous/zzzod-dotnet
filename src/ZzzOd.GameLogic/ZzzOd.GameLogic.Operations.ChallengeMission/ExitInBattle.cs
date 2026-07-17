using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.ChallengeMission;

/// <summary>
/// 战斗中退出挑战。
/// </summary>
public sealed class ExitInBattle : ZOperation
{
	private readonly string? _waitScreenName;

	private readonly string? _waitAreaName;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化战斗中退出操作。
	/// </summary>
	public ExitInBattle(ZContext context, string? waitScreenName = null, string? waitAreaName = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "战斗中退出")
	{
		_waitScreenName = waitScreenName;
		_waitAreaName = waitAreaName;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 10)]
	private OperationRoundResult CheckScreen()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗-菜单", "按钮-退出战斗", _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult2 = RoundByClickArea("战斗画面", "菜单", clickLeftTop: false, null, successDelay, retryDelay);
		return RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("画面识别")]
	[OperationNode("点击退出战斗")]
	private OperationRoundResult ClickExitBattle()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "战斗-菜单", "按钮-退出战斗", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, new (string, string)[] { ("战斗-菜单", "按钮-退出战斗-确认") });
	}

	[NodeFrom("点击退出战斗")]
	[OperationNode("点击确认")]
	private OperationRoundResult ClickConfirm()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "战斗-菜单", "按钮-退出战斗-确认", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, null, new (string, string)[] { ("战斗-菜单", "按钮-退出战斗-确认") });
	}

	[NodeFrom("点击确认")]
	[OperationNode("退出后等待", NodeMaxRetryTimes = 60)]
	private OperationRoundResult WaitAfterExit()
	{
		if (string.IsNullOrWhiteSpace(_waitScreenName) || string.IsNullOrWhiteSpace(_waitAreaName))
		{
			return RoundSuccess();
		}
		Mat? lastScreenshot = base.LastScreenshot;
		string? waitScreenName = _waitScreenName;
		string? waitAreaName = _waitAreaName;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindArea(lastScreenshot, waitScreenName, waitAreaName, null, retryDelay);
	}
}
