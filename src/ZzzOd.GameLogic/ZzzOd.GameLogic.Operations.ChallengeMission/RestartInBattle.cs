using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.ChallengeMission;

/// <summary>
/// 战斗中重新开始挑战。
/// </summary>
public sealed class RestartInBattle : ZOperation
{
	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化战斗中重新开始操作。
	/// </summary>
	public RestartInBattle(ZContext context, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "战斗中-重新开始")
	{
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
	private OperationRoundResult ClickRestart()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "战斗-菜单", "按钮-重新开始", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, new (string, string)[] { ("战斗-菜单", "按钮-退出战斗-确认") });
	}

	[NodeFrom("点击退出战斗")]
	[OperationNode("点击确认")]
	private OperationRoundResult ClickConfirm()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "战斗-菜单", "按钮-退出战斗-确认", _preClickDelay, _retryDelay, _retryDelay, cropFirst: true, centerX: false, null, new (string, string)[] { ("战斗-菜单", "按钮-退出战斗-确认") });
	}
}
