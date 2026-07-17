using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Hdd;

/// <summary>
/// 从 HDD 入口进入指定副本。
/// </summary>
public sealed class EnterHddMission : ZOperation
{
	private readonly string _chapter;

	private readonly string _missionType;

	private readonly string _missionName;

	private readonly int _predefinedTeamIndex;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化 HDD 副本进入操作。
	/// </summary>
	public EnterHddMission(ZContext context, string chapter, string missionType, string missionName, int predefinedTeamIndex = -1, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "进入 HDD 副本")
	{
		_chapter = chapter;
		_missionType = missionType;
		_missionName = missionName;
		_predefinedTeamIndex = predefinedTeamIndex;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("选择章节", IsStartNode = true)]
	private OperationRoundResult ChooseChapter()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("HDD", "章节列表");
		Mat? lastScreenshot = base.LastScreenshot;
		string chapter = _chapter;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(lastScreenshot, chapter, area, 0.6, null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, _retryDelay);
		}
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("HDD", "章节显示");
		OperationRoundResult operationRoundResult2 = RoundByOcr(base.LastScreenshot, _chapter, area2, 0.5, _retryDelay, _retryDelay);
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status);
		}
		OperationRoundResult operationRoundResult3 = RoundByFindArea(base.LastScreenshot, "HDD", "下一步", TimeSpan.FromSeconds(2L), _retryDelay);
		if (operationRoundResult3.IsSuccess)
		{
			return RoundSuccess(operationRoundResult3.Status, null, TimeSpan.FromSeconds(2L));
		}
		OperationRoundResult operationRoundResult4 = RoundByClickArea("HDD", "章节显示", clickLeftTop: false, _preClickDelay, _retryDelay, _retryDelay);
		return RoundRetry(operationRoundResult4.Status, null, _retryDelay);
	}

	[NodeFrom("选择章节")]
	[OperationNode("选择委托")]
	private OperationRoundResult ChooseMissionType()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("HDD", "委托区域");
		Mat? lastScreenshot = base.LastScreenshot;
		string missionType = _missionType;
		TimeSpan? successDelay = TimeSpan.FromSeconds(2L);
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(lastScreenshot, missionType, area, 0.6, null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, TimeSpan.FromSeconds(2L));
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "HDD", "下一步", TimeSpan.FromSeconds(2L), _retryDelay);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, TimeSpan.FromSeconds(2L)) : RoundRetry(operationRoundResult2.Status, null, _retryDelay);
	}

	[NodeFrom("选择章节", Status = "下一步")]
	[NodeFrom("选择委托")]
	[OperationNode("选择副本", NodeMaxRetryTimes = 10)]
	private OperationRoundResult ChooseMission()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("HDD", "副本区域");
		if (area == null)
		{
			return RoundFail("区域未配置 副本区域");
		}
		Mat? lastScreenshot = base.LastScreenshot;
		string missionName = _missionName;
		TimeSpan? successDelay = _retryDelay;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(lastScreenshot, missionName, area, 0.6, null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			Mat? lastScreenshot2 = base.LastScreenshot;
			string missionName2 = _missionName;
			retryDelay = _retryDelay;
			RoundByOcrAndClick(lastScreenshot2, missionName2, area, 0.6, null, retryDelay);
			return RoundSuccess(operationRoundResult.Status, null, _retryDelay);
		}
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(0, -200);
		base.ZContext.Controller?.DragTo(end, center);
		return RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("选择副本")]
	[OperationNode("下一步")]
	private OperationRoundResult ClickNext()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "HDD", "下一步", _preClickDelay, TimeSpan.FromSeconds(2L), _retryDelay);
	}

	[NodeFrom("下一步")]
	[OperationNode("选择预备编队")]
	private async Task<OperationRoundResult> ChoosePredefinedTeam()
	{
		if (_predefinedTeamIndex == -1)
		{
			return RoundSuccess("无需选择预备编队");
		}
		return RoundByOperationResult(await new ChoosePredefinedTeam(base.ZContext, new int[] { _predefinedTeamIndex }, _retryDelay, _preClickDelay).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("选择预备编队")]
	[OperationNode("出战")]
	private OperationRoundResult ClickDeploy()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "HDD", "出战", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("出战")]
	[OperationNode("识别低等级")]
	private OperationRoundResult CheckLevel()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "HDD", "确定并出战", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("识别低等级")]
	[NodeFrom("识别低等级", Success = false)]
	[OperationNode("进入成功")]
	private OperationRoundResult Finish()
	{
		return RoundSuccess();
	}
}
