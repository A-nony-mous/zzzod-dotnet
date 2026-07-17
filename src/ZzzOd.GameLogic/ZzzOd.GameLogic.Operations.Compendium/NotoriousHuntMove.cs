using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 恶名狩猎进入战斗前，根据距离提示靠近目标并选择 buff。
/// </summary>
public sealed class NotoriousHuntMove : ZOperation
{
	private static readonly int[] SearchDirections = new int[18]
	{
		2, 3, 3, 2, 0, 1, 1, 0, 2, 0,
		3, 3, 1, 1, 2, 2, 0, 3
	};

	private readonly int _buffNum;

	private readonly INotoriousHuntMoveDetector _detector;

	private readonly bool _ownsDetector;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _actionDelay;

	private int _moveTimes;

	private int _noDistanceTimes;

	private IZzzControllerActions? ControllerActions => base.ZContext.Controller as IZzzControllerActions;

	/// <summary>
	/// 初始化恶名狩猎战前移动操作。
	/// </summary>
	public NotoriousHuntMove(ZContext context, int buffNum = 3, INotoriousHuntMoveDetector? detector = null, TimeSpan? retryDelay = null, TimeSpan? actionDelay = null)
		: base(context, "恶名狩猎战斗")
	{
		_buffNum = buffNum;
		if (detector == null)
		{
			_detector = new DefaultNotoriousHuntMoveDetector(context);
			_ownsDetector = true;
		}
		else
		{
			_detector = detector;
		}
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_actionDelay = actionDelay ?? TimeSpan.FromMilliseconds(500L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_moveTimes = 0;
		_noDistanceTimes = 0;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		if (_ownsDetector && _detector is IDisposable disposable)
		{
			disposable.Dispose();
		}
		return Task.CompletedTask;
	}

	[OperationNode("初始化模型", IsStartNode = true)]
	private OperationRoundResult InitModel()
	{
		_detector.Initialize();
		return RoundSuccess();
	}

	[NodeFrom("初始化模型")]
	[OperationNode("移动靠近交互", NodeMaxRetryTimes = 10)]
	private OperationRoundResult MoveClose()
	{
		OperationRoundResult operationRoundResult = MoveByHint();
		if (operationRoundResult.IsSuccess)
		{
			_noDistanceTimes = 0;
		}
		else if (operationRoundResult.Kind == OperationRoundResultKind.Retry)
		{
			_noDistanceTimes++;
		}
		return operationRoundResult;
	}

	[NodeFrom("移动靠近交互", Status = "按键-交互")]
	[OperationNode("交互")]
	private OperationRoundResult MoveAndInteract()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-交互");
		if (operationRoundResult.IsSuccess)
		{
			IZzzControllerActions controllerActions = ControllerActions;
			if (controllerActions == null)
			{
				return RoundFail("控制器不支持绝区零动作");
			}
			controllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
			return RoundRetry(null, null, TimeSpan.FromSeconds(2L));
		}
		return RoundSuccess();
	}

	[NodeFrom("交互")]
	[OperationNode("选择")]
	private OperationRoundResult ChooseBuff()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-普通攻击");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess();
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-交互");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess();
		}
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("未识别到鸣徽选择", null, _retryDelay);
		}
		List<OcrMatchResult> list = (from result in base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot)
			where StringUtils.FindByLcs("选择", result.Text, 1.0)
			orderby result.Center.X
			select result).ToList();
		base.ZContext.Logger.Information("当前识别鸣徽选项数量 {ChoiceCount}", list.Count);
		if (list.Count == 0)
		{
			return RoundRetry("未识别到鸣徽选择", null, _retryDelay);
		}
		int num = _buffNum - 1;
		if (num >= list.Count)
		{
			num = 0;
		}
		else if (num < 0)
		{
			num = list.Count + num;
		}
		base.ZContext.Controller.Click(list[num].Center);
		return RoundWait(null, null, _retryDelay);
	}

	[NodeFrom("选择")]
	[OperationNode("选择后移动", NodeMaxRetryTimes = 18)]
	private OperationRoundResult MoveAfterBuff()
	{
		OperationRoundResult operationRoundResult = MoveByHint();
		if (operationRoundResult.Kind == OperationRoundResultKind.Retry)
		{
			if (!MoveBySearchDirection(SearchDirections[_noDistanceTimes % SearchDirections.Length]))
			{
				return RoundFail("控制器不支持绝区零动作");
			}
			_noDistanceTimes++;
		}
		else
		{
			_noDistanceTimes = 0;
		}
		return operationRoundResult;
	}

	[NodeFrom("移动靠近交互", Status = "标识-BOSS血条")]
	[NodeFrom("选择", Success = false)]
	[NodeFrom("选择后移动")]
	[OperationNode("移动完成")]
	private OperationRoundResult MoveDone()
	{
		return RoundSuccess();
	}

	private OperationRoundResult MoveByHint()
	{
		if (_moveTimes >= 10)
		{
			return RoundFail();
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-交互");
		if (operationRoundResult.IsSuccess)
		{
			IZzzControllerActions controllerActions = ControllerActions;
			if (controllerActions == null)
			{
				return RoundFail("控制器不支持绝区零动作");
			}
			controllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
			return RoundSuccess(operationRoundResult.Status, null, TimeSpan.FromSeconds(2L));
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry(null, null, _retryDelay);
		}
		string fileStem = null;
		string beforeScreenshotPath = null;
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId() + "-notorious-hunt-move-by-hint");
			beforeScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot);
		}
		NotoriousHuntDistanceHint notoriousHuntDistanceHint = _detector.DetectDistanceHint(base.LastScreenshot);
		base.ZContext.AutoBattleContext.CheckBattleDistance(base.LastScreenshot);
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "恶名狩猎", "标识-BOSS血条");
		if (operationRoundResult2.IsSuccess)
		{
			return RoundSuccess(operationRoundResult2.Status);
		}
		if ((object)notoriousHuntDistanceHint == null)
		{
			IZzzControllerActions controllerActions2 = ControllerActions;
			if (controllerActions2 == null)
			{
				return RoundFail("控制器不支持绝区零动作");
			}
			controllerActions2.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
			WriteMoveByHintEvidence(fileStem, beforeScreenshotPath, notoriousHuntDistanceHint, "key_press", "key=w", "distance_dot_missing", "retry", null);
			return RoundRetry(null, null, _retryDelay);
		}
		float? num = ResolveTurnDistance(notoriousHuntDistanceHint.Position.X);
		if (num.HasValue)
		{
			IZzzControllerActions controllerActions3 = ControllerActions;
			if (controllerActions3 == null)
			{
				return RoundFail("控制器不支持绝区零动作");
			}
			controllerActions3.TurnByDistance(num.Value);
			WriteMoveByHintEvidence(fileStem, beforeScreenshotPath, notoriousHuntDistanceHint, "turn_by_distance", $"distance={num.Value}", "distance_dot_centered_or_interact", "wait", null);
			return RoundWait(null, null, _actionDelay);
		}
		double num2 = Math.Min((double)base.ZContext.AutoBattleContext.LastCheckDistance / 7.2, 5.0);
		base.ZContext.Logger.Information("识别距离: {Distance}", base.ZContext.AutoBattleContext.LastCheckDistance);
		if (num2 <= 0.0)
		{
			WriteMoveByHintEvidence(fileStem, beforeScreenshotPath, notoriousHuntDistanceHint, "retry", "distance_ocr", "positive_distance", "retry", "识别距离失败");
			return RoundRetry("识别距离失败", null, _retryDelay);
		}
		IZzzControllerActions controllerActions4 = ControllerActions;
		if (controllerActions4 == null)
		{
			return RoundFail("控制器不支持绝区零动作");
		}
		controllerActions4.MoveW(press: true, TimeSpan.FromSeconds(num2), release: true);
		_moveTimes++;
		WriteMoveByHintEvidence(fileStem, beforeScreenshotPath, notoriousHuntDistanceHint, "key_press", $"key=w; seconds={num2:0.###}", "closer_to_interact_or_boss_bar", "wait", null);
		return RoundWait(null, null, _actionDelay);
	}

	private void WriteMoveByHintEvidence(string? fileStem, string? beforeScreenshotPath, NotoriousHuntDistanceHint? hint, string actionKind, string actionTarget, string expectedNextState, string transitionResult, string? failureReason)
	{
		if (fileStem != null)
		{
			Mat image = Screenshot();
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", image);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
				OperationName = "恶名狩猎战斗",
				NodeName = "移动靠近交互",
				DotNetMethod = "ZzzOd.GameLogic.Operations.Compendium.NotoriousHuntMove.MoveByHint()",
				BaselineParityRequirement = "NotoriousHuntMove reads distance only from 战斗画面/距离显示区域 OCR texts containing m; no distance dot moves W for 1 second and retries after 1 second.",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = new
				{
					distance_dot = ((object)hint != null),
					distance_position = (((object)hint == null) ? null : new
					{
						x = hint.Position.X,
						y = hint.Position.Y
					}),
					distance = base.ZContext.AutoBattleContext.LastCheckDistance,
					turn_distance = (((object)hint == null) ? ((float?)null) : ResolveTurnDistance(hint.Position.X))
				},
				ActionKind = actionKind,
				ActionTarget = actionTarget,
				ExpectedNextState = expectedNextState,
				AfterScreenshotPath = afterScreenshotPath,
				AfterRecognitionSummary = new
				{
					evidence_scope = "fresh screenshot after input dispatch"
				},
				TransitionResult = transitionResult,
				FailureReason = failureReason,
				RetryStoppedBecauseOfSuspectedLoop = string.Equals(failureReason, "识别距离失败", StringComparison.Ordinal)
			});
		}
	}

	private bool MoveBySearchDirection(int direction)
	{
		IZzzControllerActions controllerActions = ControllerActions;
		if (controllerActions == null)
		{
			return false;
		}
		TimeSpan value = TimeSpan.FromMilliseconds(500L);
		switch (direction)
		{
		case 0:
			controllerActions.MoveW(press: true, value, release: true);
			break;
		case 1:
			controllerActions.MoveS(press: true, value, release: true);
			break;
		case 2:
			controllerActions.MoveA(press: true, value, release: true);
			break;
		case 3:
			controllerActions.MoveD(press: true, value, release: true);
			break;
		}
		return true;
	}

	/// <summary>
	/// 将距离提示点的横坐标转换为转向距离。
	/// </summary>
	public static float? ResolveTurnDistance(int x)
	{
		if (x < 760)
		{
			return -100f;
		}
		if (x < 860)
		{
			return -50f;
		}
		if (x < 910)
		{
			return -25f;
		}
		if (x > 1160)
		{
			return 100f;
		}
		if (x > 1060)
		{
			return 50f;
		}
		if (x > 1010)
		{
			return 25f;
		}
		return null;
	}
}
