using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都战斗节点图，与 BaselineParity <c>hollow_battle.py</c> 保持同一状态消费顺序。
/// </summary>
public sealed class WitheredDomainHollowBattle : ZOperation
{
	/// <summary>需要向前移动。</summary>
	public const string StatusNeedSpecialMove = "需要移动";

	/// <summary>移动失败。</summary>
	public const string StatusFailToMove = "移动失败";

	private readonly WitheredDomainRunRecord _runRecord;

	private int _moveTimes;

	private int _turnTimes;

	private int _stuckMoveDirection;

	private float? _lastDistance;

	private float? _lastStuckDistance;

	private float? _lastDistanceToTurn;

	/// <summary>
	/// 初始化枯萎之都战斗。
	/// </summary>
	public WitheredDomainHollowBattle(ZContext context, WitheredDomainRunRecord runRecord)
		: base(context, "空洞战斗")
	{
		_runRecord = runRecord ?? throw new ArgumentNullException("runRecord");
	}

	/// <summary>加载当前挑战配置指定的自动战斗。</summary>
	[OperationNode("加载自动战斗指令", IsStartNode = true)]
	public OperationRoundResult LoadAutoOperation()
	{
		base.ZContext.AutoBattleContext.InitAutoOp(base.ZContext.WitheredDomain.GetAutoBattleName());
		base.ZContext.AutoBattleContext.LastCheckEndResult = null;
		return RoundSuccess();
	}

	/// <summary>等待普通攻击按键出现。</summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("等待战斗画面加载", NodeMaxRetryTimes = 60)]
	public OperationRoundResult WaitBattleScreen()
	{
		// 对应参考实现 hollow_zero/hollow_battle.py:65 的 retry_wait_round=1（补足制，非固定延时）。
		return RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-普通攻击", null, null, cropFirst: true, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>检查是否需要战斗前特殊移动。</summary>
	[NodeFrom("等待战斗画面加载")]
	[OperationNode("识别特殊移动")]
	public OperationRoundResult CheckSpecialMove()
	{
		CheckDistance();
		if (base.ZContext.AutoBattleContext.WithDistanceTimes >= 10)
		{
			return RoundSuccess("需要移动");
		}
		if (base.ZContext.AutoBattleContext.WithoutDistanceTimes >= 10)
		{
			base.ZContext.AutoBattleContext.StartAutoBattle();
			return RoundSuccess("不需要移动");
		}
		return RoundWait();
	}

	/// <summary>执行副本规定的向前移动。</summary>
	[NodeFrom("识别特殊移动", Status = "需要移动")]
	[OperationNode("副本特殊移动")]
	public OperationRoundResult SpecialMove()
	{
		if (!(base.ZContext.Controller is IZzzControllerActions zzzControllerActions))
		{
			return RoundFail("控制器不支持移动");
		}
		zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1.5), release: true);
		return RoundSuccess();
	}

	/// <summary>按距离标记向战斗点移动并执行脱困。</summary>
	[NodeFrom("副本特殊移动")]
	[NodeFrom("自动战斗", Status = "需要移动")]
	[OperationNode("向前移动准备战斗")]
	public OperationRoundResult MoveToBattle()
	{
		CheckDistance();
		if (!TryGetDistancePosition(base.LastScreenshot, out var position))
		{
			if (base.ZContext.AutoBattleContext.WithoutDistanceTimes >= 10)
			{
				base.ZContext.AutoBattleContext.StartAutoBattle();
				return RoundSuccess();
			}
			return RoundWait();
		}
		if (_moveTimes >= 20 || _turnTimes >= 60)
		{
			return RoundFail("移动失败");
		}
		if (!(base.ZContext.Controller is IZzzControllerActions zzzControllerActions))
		{
			return RoundFail("移动失败");
		}
		float lastCheckDistance = base.ZContext.AutoBattleContext.LastCheckDistance;
		float? lastDistance = _lastDistance;
		if (lastDistance.HasValue)
		{
			float valueOrDefault = lastDistance.GetValueOrDefault();
			if (Math.Abs(valueOrDefault - lastCheckDistance) < 0.5f)
			{
				lastDistance = _lastStuckDistance;
				if (lastDistance.HasValue)
				{
					float valueOrDefault2 = lastDistance.GetValueOrDefault();
					if (Math.Abs(valueOrDefault2 - lastCheckDistance) < 0.5f)
					{
						_stuckMoveDirection = (_stuckMoveDirection + 1) % 6;
					}
				}
				_lastDistance = lastCheckDistance;
				_lastStuckDistance = lastCheckDistance;
				GetRidOfStuck(zzzControllerActions);
				return RoundWait(null, null, TimeSpan.FromMilliseconds(500L));
			}
		}
		if (position.Value.X < 900)
		{
			zzzControllerActions.TurnByDistance(-50f);
			_turnTimes++;
			return RoundWait(null, null, TimeSpan.FromMilliseconds(500L));
		}
		if (position.Value.X > 1100)
		{
			zzzControllerActions.TurnByDistance(50f);
			_turnTimes++;
			return RoundWait(null, null, TimeSpan.FromMilliseconds(500L));
		}
		_lastDistance = lastCheckDistance;
		zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds((double)lastCheckDistance / 7.2), release: true);
		_moveTimes++;
		_lastDistanceToTurn = null;
		return RoundWait(null, null, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>消费上一帧战斗结束结果，再提交本帧异步检测。</summary>
	[NodeFrom("识别特殊移动", Status = "不需要移动")]
	[NodeFrom("向前移动准备战斗")]
	[OperationNode("自动战斗", TimeoutSeconds = 600.0, Mute = true)]
	public OperationRoundResult AutoBattle()
	{
		_moveTimes = 0;
		_turnTimes = 0;
		string lastCheckEndResult = base.ZContext.AutoBattleContext.LastCheckEndResult;
		if (lastCheckEndResult != null)
		{
			base.ZContext.AutoBattleContext.StopAutoBattle();
			return RoundSuccess(lastCheckEndResult);
		}
		if (base.ZContext.AutoBattleContext.WithDistanceTimes >= 5)
		{
			base.ZContext.AutoBattleContext.StopAutoBattle();
			return RoundSuccess("需要移动");
		}
		if (base.LastScreenshot == null || base.LastScreenshot.Empty())
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromSeconds(1L));
		}
		base.ZContext.AutoBattleContext.CheckBattleState(base.LastScreenshot, base.LastScreenshotTimeUtc, checkBattleEndNormalResult: true, checkBattleEndHollowResult: true, checkBattleEndDefenseResult: false, checkDistance: true);
		return RoundWait(null, null, TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
	}

	/// <summary>处理周期奖励领满提示。</summary>
	[NodeFrom("自动战斗", Status = "零号空洞-结算周期上限")]
	[OperationNode("结算周期上限")]
	public OperationRoundResult PeriodRewardFull()
	{
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		_runRecord.SetPeriodRewardComplete(complete: true);
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-战斗", "结算周期上限-确认", null, successDelay, retryDelay);
	}

	/// <summary>确认挑战结算。</summary>
	[NodeFrom("结算周期上限")]
	[NodeFrom("自动战斗", Status = "零号空洞-挑战结果")]
	[OperationNode("战斗结果-确定")]
	public OperationRoundResult AfterBattle()
	{
		Thread.Sleep(TimeSpan.FromSeconds(2L));
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "零号空洞-战斗", "结算周期上限-确认");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("零号空洞-战斗", "战斗结果-确定");
		OperationRoundResult operationRoundResult2 = RoundByOcrAndClick(base.LastScreenshot, "确定", area);
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
		}
		RoundByClickArea("零号空洞-战斗", "战斗结果-确定");
		return RoundRetry(operationRoundResult2.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>普通战斗完成后记录周期奖励状态。</summary>
	[NodeFrom("自动战斗", Status = "普通战斗-完成")]
	[OperationNode("普通战斗-完成")]
	public OperationRoundResult MissionComplete()
	{
		Thread.Sleep(TimeSpan.FromSeconds(2L));
		using Mat mat = Screenshot();
		FindAreaResultEnum findAreaResultEnum = ((mat != null) ? ScreenUtils.FindArea(base.ZContext, mat, "零号空洞-战斗", "通关-丁尼奖励") : FindAreaResultEnum.False);
		_runRecord.SetPeriodRewardComplete(findAreaResultEnum != FindAreaResultEnum.True);
		return RoundSuccess("普通战斗-完成");
	}

	/// <summary>挑战结算后进入下一层。</summary>
	[NodeFrom("战斗结果-确定")]
	[OperationNode("更新楼层信息")]
	public OperationRoundResult UpdateLevelInfo()
	{
		base.ZContext.WitheredDomain.UpdateToNextLevel();
		return RoundSuccess();
	}

	/// <summary>普通战斗撤退。</summary>
	[NodeFrom("自动战斗", Status = "普通战斗-撤退")]
	[OperationNode("战斗撤退")]
	public OperationRoundResult BattleFail()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "战斗画面", "战斗结果-撤退", null, successDelay, retryDelay);
	}

	/// <summary>移动或超时失败时停止输入并打开退出菜单。</summary>
	[NodeFrom("自动战斗", Success = false, Status = "执行超时")]
	[NodeFrom("向前移动准备战斗", Success = false, Status = "移动失败")]
	[OperationNode("移动失败")]
	public OperationRoundResult MoveFail()
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
		if (base.LastScreenshot != null && ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "零号空洞-战斗", "退出战斗") == FindAreaResultEnum.True)
		{
			return RoundSuccess(null, null, TimeSpan.FromMilliseconds(500L));
		}
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByClickArea("战斗画面", "菜单", clickLeftTop: false, null, successDelay, retryDelay);
	}

	/// <summary>点击退出战斗。</summary>
	[NodeFrom("移动失败")]
	[OperationNode("点击退出")]
	public OperationRoundResult ClickExit()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-战斗", "退出战斗", null, successDelay, retryDelay);
	}

	/// <summary>确认退出战斗。</summary>
	[NodeFrom("点击退出")]
	[OperationNode("点击退出确认")]
	public OperationRoundResult ClickExitConfirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByFindAndClickArea(lastScreenshot, "零号空洞-战斗", "退出战斗-确认", null, successDelay, retryDelay);
	}

	/// <summary>等待返回通关画面。</summary>
	[NodeFrom("点击退出确认")]
	[OperationNode("等待退出", NodeMaxRetryTimes = 20)]
	public OperationRoundResult WaitExit()
	{
		return RoundByFindArea(base.LastScreenshot, "零号空洞-事件", "通关-完成", TimeSpan.FromSeconds(2L), TimeSpan.FromSeconds(1L));
	}

	/// <summary>暂停时停止自动战斗及按键。</summary>
	public void PauseAutoBattle()
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
	}

	/// <summary>仅在自动战斗节点恢复暂停前的操作。</summary>
	public void ResumeAutoBattle()
	{
		if (string.Equals(base.CurrentNode.Name, "自动战斗", StringComparison.Ordinal))
		{
			base.ZContext.AutoBattleContext.ResumeAutoBattle();
		}
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		base.ZContext.AutoBattleContext.StopAutoBattle();
		return base.OnAfterOperationDoneAsync(cancellationToken);
	}

	private void CheckDistance()
	{
		if (base.LastScreenshot != null)
		{
			base.ZContext.AutoBattleContext.CheckBattleDistance(base.LastScreenshot, _lastDistanceToTurn);
		}
	}

	private bool TryGetDistancePosition(Mat? screen, out OneDragon.Core.Abstractions.Geometry.Point? position)
	{
		position = null;
		if (screen == null)
		{
			return false;
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("战斗画面", "距离显示区域");
		if (area == null)
		{
			return false;
		}
		int num = base.ZContext.ProjectConfig.ScreenStandardWidth / 2;
		OcrMatchResult ocrMatchResult = null;
		foreach (OcrMatchResult ocrResult in base.ZContext.OcrService.GetOcrResultList(screen, null, area.Rect))
		{
			Match match = Regex.Match(ocrResult.Text, "\\d+(\\.\\d+)?(?=m)");
			if (match.Success && float.TryParse(match.Value, CultureInfo.InvariantCulture, out var result) && (ocrMatchResult == null || Math.Abs(ocrResult.Center.X - num) < Math.Abs(ocrMatchResult.Center.X - num)))
			{
				ocrMatchResult = ocrResult;
				_lastDistanceToTurn = result;
			}
		}
		if (ocrMatchResult == null)
		{
			return false;
		}
		position = ocrMatchResult.Center;
		return true;
	}

	private void GetRidOfStuck(IZzzControllerActions actions)
	{
		switch (_stuckMoveDirection)
		{
		case 0:
			actions.MoveA(press: true, TimeSpan.FromSeconds(1L), release: true);
			break;
		case 1:
			actions.MoveD(press: true, TimeSpan.FromSeconds(1L), release: true);
			break;
		case 2:
			MoveStuckPath(actions, 1.0, left: true);
			break;
		case 3:
			MoveStuckPath(actions, 1.0, left: false);
			break;
		case 4:
			MoveStuckPath(actions, 2.0, left: true);
			break;
		default:
			MoveStuckPath(actions, 2.0, left: false);
			break;
		}
	}

	private static void MoveStuckPath(IZzzControllerActions actions, double seconds, bool left)
	{
		TimeSpan value = TimeSpan.FromSeconds(seconds);
		actions.MoveS(press: true, value, release: true);
		if (left)
		{
			actions.MoveA(press: true, value, release: true);
		}
		else
		{
			actions.MoveD(press: true, value, release: true);
		}
		actions.MoveW(press: true, value, release: true);
	}
}
