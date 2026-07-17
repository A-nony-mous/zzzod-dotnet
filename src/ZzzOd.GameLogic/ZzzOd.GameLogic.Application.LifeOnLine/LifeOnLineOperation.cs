using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线主流程。
/// </summary>
public sealed class LifeOnLineOperation : ZOperation
{
	/// <summary>达到指定次数。</summary>
	public const string StatusTimesFinished = "完成指定次数";

	/// <summary>继续挑战。</summary>
	public const string StatusContinue = "继续";

	/// <summary>过夜后继续挑战。</summary>
	public const string StatusContinueOverNight = "过夜后继续";

	private readonly LifeOnLineConfig _config;

	private readonly LifeOnLineRunRecord _runRecord;

	private readonly ILifeOnLineOperationServices _services;

	private bool _isOverNight;

	private bool _chosenTeam;

	/// <summary>
	/// 是否已经选过预备编队。
	/// </summary>
	public bool ChosenTeam => _chosenTeam;

	/// <summary>
	/// 本次结束是否触发过夜。
	/// </summary>
	public bool IsOverNight => _isOverNight;

	/// <summary>
	/// 初始化生命热线主流程。
	/// </summary>
	public LifeOnLineOperation(ZContext context, LifeOnLineConfig config, LifeOnLineRunRecord runRecord, ILifeOnLineOperationServices? services = null)
		: base(context, "真·拿命验收")
	{
		_config = config;
		_runRecord = runRecord;
		_services = services ?? new DefaultLifeOnLineOperationServices();
	}

	/// <summary>
	/// 传送到录像店 HDD。
	/// </summary>
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _services.TransportToHddAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待加载到大世界。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("等待加载")]
	public async Task<OperationRoundResult> WaitWorld()
	{
		return RoundByOperationResult(await _services.WaitNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 与 HDD 入口交互。
	/// </summary>
	[NodeFrom("等待加载")]
	[NodeFrom("检查运行次数", Status = "过夜后继续")]
	[OperationNode("交互")]
	public OperationRoundResult Interact()
	{
		if (_services.IsHddStreetVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess();
		}
		_services.Interact(base.ZContext);
		return RoundWait(null, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 进入真拿命验收副本。
	/// </summary>
	[NodeFrom("交互")]
	[NodeFrom("检查运行次数", Status = "继续")]
	[OperationNode("进入副本")]
	public async Task<OperationRoundResult> EnterMission()
	{
		int teamIndex = (_chosenTeam ? (-1) : _config.PredefinedTeamIndex);
		return RoundByOperationResult(await _services.EnterMissionAsync(base.ZContext, teamIndex).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待战斗画面加载。
	/// </summary>
	[NodeFrom("进入副本")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	public OperationRoundResult WaitBattleScreen()
	{
		_chosenTeam = true;
		return _services.IsBattleScreenReady(base.ZContext, base.LastScreenshot) ? RoundSuccess() : RoundRetry("未进入战斗画面", null, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 执行真拿命验收按键脚本。
	/// </summary>
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("模拟按键")]
	public async Task<OperationRoundResult> RunKeySim()
	{
		return RoundByOperationResult(await _services.RunKeySimAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 通关后交互。
	/// </summary>
	[NodeFrom("模拟按键")]
	[OperationNode("通关交互", NodeMaxRetryTimes = 10)]
	public OperationRoundResult InteractAfterMission()
	{
		if (_services.IsDialogPersonVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess();
		}
		_services.Interact(base.ZContext);
		return RoundRetry(null, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 处理通关后的对话。
	/// </summary>
	[NodeFrom("通关交互")]
	[OperationNode("对话", NodeMaxRetryTimes = 30)]
	public OperationRoundResult TalkAfterMission()
	{
		if (_services.IsBattleResultCompleteVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess(null, null, TimeSpan.FromSeconds(1L));
		}
		string text = _services.ClickFirstDialogOption(base.ZContext, base.LastScreenshot);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return RoundWait(text, null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult = _services.ClickMenuBack(base.ZContext);
		return operationResult.IsSuccess ? RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 点击完成并判断是否过夜。
	/// </summary>
	[NodeFrom("对话")]
	[OperationNode("完成", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> ClickFinished()
	{
		if (_services.IsHddStreetVisible(base.ZContext, base.LastScreenshot))
		{
			_isOverNight = false;
			_runRecord.AddTimes();
			return RoundSuccess("街区");
		}
		OperationResult clickFinished = _services.ClickBattleResultComplete(base.ZContext, base.LastScreenshot);
		if (clickFinished.IsSuccess)
		{
			return RoundWait(clickFinished.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		OperationResult waitWorld = await _services.WaitNormalWorldOnceAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		if (waitWorld.IsSuccess)
		{
			_isOverNight = true;
			_runRecord.AddTimes();
			return RoundSuccess(waitWorld.Status);
		}
		OperationResult clickBlank = _services.ClickHddBlank(base.ZContext);
		return RoundRetry(clickBlank.Status ?? waitWorld.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 检查运行次数。
	/// </summary>
	[NodeFrom("完成")]
	[NodeFrom("点击退出战斗确认")]
	[OperationNode("检查运行次数")]
	public OperationRoundResult CheckTimes()
	{
		_runRecord.CheckAndUpdateStatus();
		if (_runRecord.IsFinishedByTimes())
		{
			return RoundSuccess("完成指定次数");
		}
		return RoundSuccess(_isOverNight ? "过夜后继续" : "继续");
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("检查运行次数", Status = "完成指定次数")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _services.BackToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 通关交互失败后的退出战斗处理。
	/// </summary>
	[NodeFrom("通关交互", Success = false)]
	[OperationNode("交互失败")]
	public OperationRoundResult FailToInteract()
	{
		if (_services.IsExitBattleVisible(base.ZContext, base.LastScreenshot))
		{
			return RoundSuccess(null, null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult = _services.ClickBattleMenu(base.ZContext);
		return operationResult.IsSuccess ? RoundWait(operationResult.Status, null, TimeSpan.FromSeconds(2L)) : RoundFail(operationResult.Status);
	}

	/// <summary>
	/// 点击退出战斗。
	/// </summary>
	[NodeFrom("交互失败")]
	[OperationNode("点击退出战斗")]
	public OperationRoundResult ClickExitBattle()
	{
		OperationResult operationResult = _services.ClickExitBattle(base.ZContext, base.LastScreenshot);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 点击退出战斗确认。
	/// </summary>
	[NodeFrom("点击退出战斗")]
	[OperationNode("点击退出战斗确认")]
	public OperationRoundResult ClickExitBattleConfirm()
	{
		OperationResult operationResult = _services.ClickExitBattleConfirm(base.ZContext, base.LastScreenshot);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(5L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}
}
