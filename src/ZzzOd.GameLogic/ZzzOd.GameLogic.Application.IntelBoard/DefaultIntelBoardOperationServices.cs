using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operation;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 默认情报板流程服务。
/// </summary>
public sealed class DefaultIntelBoardOperationServices : IIntelBoardOperationServices
{
	private sealed record OcrClickAttempt(OperationResult Result, OcrClickTargetSummary? Target);

	private sealed class OcrClickTargetSummary
	{
		public string TargetText { get; set; } = string.Empty;

		public string MatchedText { get; set; } = string.Empty;

		public double Confidence { get; set; }

		public int X { get; set; }

		public int Y { get; set; }

		public int Width { get; set; }

		public int Height { get; set; }

		public int ClickX { get; set; }

		public int ClickY { get; set; }

		public bool PcAlt { get; set; }

		public string? GamepadAction { get; set; }

		public bool ControllerAccepted { get; set; }
	}

	private readonly TimeSpan _preClickDelay;

	private double _lastBattleStatusLogTime;

	/// <summary>
	/// 初始化默认情报板服务。
	/// </summary>
	public DefaultIntelBoardOperationServices(TimeSpan? preClickDelay = null)
	{
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToVideoStoreAsync(ZContext context)
	{
		return new Transport(context, "录像店", "房间").ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> OpenBoardAsync(ZContext context, Mat? screen)
	{
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId("intel_board") + "-intel-board-open-function-guide");
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", screen) : null);
		object beforeRecognitionSummary = (isEnabled ? BuildFunctionGuideSummary(context, screen, "大世界-普通", "功能导览") : null);
		OperationResult operationResult = FindAndClickArea(context, "大世界-普通", "功能导览", screen);
		if (isEnabled)
		{
			using Mat mat = Screenshot(context);
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			object afterRecognitionSummary = BuildFunctionGuideSummary(context, mat, "大世界-普通", "功能导览");
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("intel_board"),
				OperationName = "情报板",
				NodeName = "打开情报板",
				DotNetMethod = "ZzzOd.GameLogic.Application.IntelBoard.DefaultIntelBoardOperationServices.OpenBoardAsync()",
				BaselineParityRequirement = "IntelBoard.open_board clicks area 大世界-普通/功能导览 before OCR clicking 情报板.",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = beforeRecognitionSummary,
				ActionKind = "click_area",
				ActionTarget = "大世界-普通/功能导览",
				ActionTargetDetails = GetAreaClickTargetSummary(context, screen, "大世界-普通", "功能导览"),
				ExpectedNextState = "功能导览 opened and 情报板 visible for OCR click",
				AfterScreenshotPath = afterScreenshotPath,
				AfterRecognitionSummary = afterRecognitionSummary,
				TransitionResult = (operationResult.IsSuccess ? "function_guide_click_sent" : "action_failed"),
				FailureReason = (operationResult.IsSuccess ? null : operationResult.Status),
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
		return Task.FromResult(operationResult);
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickBoardAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(ClickText(context, "情报板", null, screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> RefreshCommissionAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(FindAndClickArea(context, "情报板", "刷新按钮", screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> OpenFilterAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		return ScreenUtils.FindArea(context, screen, "情报板", "点数兑换") switch
		{
			FindAreaResultEnum.AreaNoConfig => Task.FromResult(new OperationResult(IsSuccess: false, "区域未配置 点数兑换")), 
			FindAreaResultEnum.True => Task.FromResult(ClickArea(context, "情报板", "筛选按钮")), 
			_ => Task.FromResult(new OperationResult(IsSuccess: false, "未找到筛选按钮")), 
		};
	}

	/// <inheritdoc />
	public Task<OperationResult> ResetFilterAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(ClickText(context, "重置", context.ScreenContext.GetArea("情报板", "重置按钮"), screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> SelectCommissionTypeAsync(ZContext context, IntelBoardCommissionType commissionType, Mat? screen)
	{
		return Task.FromResult(ClickText(context, commissionType.ToDisplayName(), context.ScreenContext.GetArea("情报板", "搜索区域"), screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> CloseFilterAsync(ZContext context)
	{
		return Task.FromResult(ClickArea(context, "情报板", "关闭筛选"));
	}

	/// <inheritdoc />
	public Task<IntelBoardCommissionType?> FindCommissionAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult<IntelBoardCommissionType?>(null);
		}
		List<OcrMatchResult> list = (from result in context.OcrService.GetOcrResultList(screen)
			where IntelBoardCommissionTypeExtensions.TryParseDisplayName(result.Text, out var _)
			orderby result.Y
			select result).ToList();
		if (list.Count == 0)
		{
			return Task.FromResult<IntelBoardCommissionType?>(null);
		}
		MatchResultList stars = context.TemplateMatcher.MatchTemplate(screen, "intel_board", "Star", "raw", 0.8, null, ignoreTemplateMask: false, onlyBest: false);
		List<OcrMatchResult> list2 = list.Where((OcrMatchResult commission) => !IsOwnCommission(commission, stars)).ToList();
		context.Logger.Information("情报板委托识别: 候选 {CandidateCount}, 我的帖子标记 {StarCount}, 可接取 {ValidCount}", list.Count, stars.Count, list2.Count);
		OcrMatchResult ocrMatchResult = list2.FirstOrDefault();
		if (ocrMatchResult == null || !IntelBoardCommissionTypeExtensions.TryParseDisplayName(ocrMatchResult.Text, out var commissionType))
		{
			return Task.FromResult<IntelBoardCommissionType?>(null);
		}
		context.Controller?.Click(ocrMatchResult.Center);
		context.Logger.Information("情报板选择委托: {CommissionType}, 点击 ({ClickX}, {ClickY})", commissionType.ToDisplayName(), ocrMatchResult.Center.X, ocrMatchResult.Center.Y);
		return Task.FromResult((IntelBoardCommissionType?)commissionType);
	}

	/// <inheritdoc />
	public Task ScrollCommissionListAsync(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("情报板", "搜索区域");
		if (area != null && context.Controller != null)
		{
			ScreenUtils.ScrollArea(context, area);
		}
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<OperationResult> AcceptCommissionAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		IReadOnlyList<OcrMatchResult> sortedOcrResults = GetSortedOcrResults(context, screen);
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string applicationId = ActionLevelDebugEvidenceWriter.GetApplicationId("intel_board");
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(applicationId + "-intel-board-accept-commission");
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", screen) : null);
		object beforeRecognitionSummary = (isEnabled ? BuildIntelBoardActionSummary(context, screen, sortedOcrResults) : null);
		OcrClickAttempt ocrClickAttempt = ClickTextByPriorityWithDetails(context, new string[3] { "接取委托", "前往", "委托代行中" }, null, screen, sortedOcrResults, requireClickSuccess: false);
		if (ocrClickAttempt.Result.IsSuccess)
		{
			context.Logger.Information("情报板接取动作: {Action}, 点击 ({ClickX}, {ClickY})", ocrClickAttempt.Result.Status, ocrClickAttempt.Target?.ClickX, ocrClickAttempt.Target?.ClickY);
		}
		if (ocrClickAttempt.Result.IsSuccess && (string.Equals(ocrClickAttempt.Result.Status, "接取委托", StringComparison.Ordinal) || string.Equals(ocrClickAttempt.Result.Status, "前往", StringComparison.Ordinal)))
		{
			if (isEnabled)
			{
				using Mat mat = Screenshot(context);
				IReadOnlyList<OcrMatchResult> readOnlyList;
				if (mat != null)
				{
					readOnlyList = GetSortedOcrResults(context, mat);
				}
				else
				{
					IReadOnlyList<OcrMatchResult> readOnlyList2 = Array.Empty<OcrMatchResult>();
					readOnlyList = readOnlyList2;
				}
				IReadOnlyList<OcrMatchResult> results = readOnlyList;
				string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
				ActionLevelDebugEvidence obj = new ActionLevelDebugEvidence
				{
					FileStem = fileStem,
					AppId = applicationId,
					OperationName = "情报板",
					NodeName = "接取委托",
					DotNetMethod = "ZzzOd.GameLogic.Application.IntelBoard.DefaultIntelBoardOperationServices.AcceptCommissionAsync()",
					BaselineParityRequirement = "IntelBoard.accept_commission OCR-clicks 接取委托 or 前往 as WAIT actions and succeeds only after 委托代行中 or the next screen appears.",
					BeforeScreenshotPath = beforeScreenshotPath,
					BeforeRecognitionSummary = beforeRecognitionSummary,
					ActionKind = "click_text"
				};
				string text = ocrClickAttempt.Result.Status;
				if (text == null)
				{
					string[] buffer = new string[2];
					buffer[0] = "接取委托";
					buffer[1] = "前往";
					text = string.Join("/", (ReadOnlySpan<string?>)buffer);
				}
				obj.ActionTarget = text;
				obj.ActionTargetDetails = ocrClickAttempt.Target;
				obj.ExpectedNextState = "委托代行中, or next_step handles 预备编队/接取失败/下一步/无报酬模式";
				obj.AfterScreenshotPath = afterScreenshotPath;
				obj.AfterRecognitionSummary = ((mat == null) ? new
				{
					failure_reason = "未获取截图"
				} : BuildIntelBoardActionSummary(context, mat, results));
				OcrClickTargetSummary? target = ocrClickAttempt.Target;
				obj.TransitionResult = ((target != null && !target.ControllerAccepted) ? "matched_action_controller_return_ignored_by_python_action_helper" : "click_sent_waiting_for_transition");
				obj.FailureReason = null;
				obj.RetryStoppedBecauseOfSuspectedLoop = false;
				ActionLevelDebugEvidenceWriter.Write(obj);
			}
			return Task.FromResult(ocrClickAttempt.Result);
		}
		if (ocrClickAttempt.Result.IsSuccess && string.Equals(ocrClickAttempt.Result.Status, "委托代行中", StringComparison.Ordinal))
		{
			return Task.FromResult(ocrClickAttempt.Result);
		}
		if (isEnabled)
		{
			using Mat mat2 = Screenshot(context);
			string afterScreenshotPath2 = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat2);
			ActionLevelDebugEvidence obj2 = new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = applicationId,
				OperationName = "情报板",
				NodeName = "接取委托",
				DotNetMethod = "ZzzOd.GameLogic.Application.IntelBoard.DefaultIntelBoardOperationServices.AcceptCommissionAsync()",
				BaselineParityRequirement = "IntelBoard.accept_commission OCR-clicks 接取委托 or 前往 as WAIT actions and succeeds only after 委托代行中 or the next screen appears.",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = beforeRecognitionSummary,
				ActionKind = "click_text",
				ActionTarget = "接取委托/前往",
				ActionTargetDetails = ocrClickAttempt.Target,
				ExpectedNextState = "委托代行中, 预备编队, 接取失败, 下一步, 无报酬模式, or another post-accept state",
				AfterScreenshotPath = afterScreenshotPath2,
				AfterRecognitionSummary = ((mat2 == null) ? new
				{
					failure_reason = "未获取截图"
				} : BuildIntelBoardActionSummary(context, mat2, GetSortedOcrResults(context, mat2)))
			};
			object transitionResult;
			if (!ocrClickAttempt.Result.IsSuccess)
			{
				transitionResult = "action_failed";
			}
			else
			{
				OcrClickTargetSummary? target2 = ocrClickAttempt.Target;
				transitionResult = ((target2 != null && !target2.ControllerAccepted) ? "matched_action_controller_return_ignored_by_python_action_helper" : "click_sent");
			}
			obj2.TransitionResult = (string)transitionResult;
			obj2.FailureReason = (ocrClickAttempt.Result.IsSuccess ? null : ocrClickAttempt.Result.Status);
			obj2.RetryStoppedBecauseOfSuspectedLoop = false;
			ActionLevelDebugEvidenceWriter.Write(obj2);
		}
		return Task.FromResult(ocrClickAttempt.Result.IsSuccess ? ocrClickAttempt.Result : new OperationResult(IsSuccess: false, "未匹配到目标文本"));
	}

	/// <inheritdoc />
	public Task<OperationResult> NextStepAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		IReadOnlyList<OcrMatchResult> sortedOcrResults = GetSortedOcrResults(context, screen);
		if (FindText(sortedOcrResults, "预备编队"))
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "预备编队"));
		}
		if (FindText(sortedOcrResults, "接取失败", 0.75))
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "接取失败"));
		}
		return Task.FromResult(ClickTextByPriority(context, new string[2] { "下一步", "无报酬模式" }, null, screen, sortedOcrResults, requireClickSuccess: false));
	}

	/// <inheritdoc />
	public Task<OperationResult> ConfirmAcceptFailedAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(ClickText(context, "确认", null, screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> ChooseTeamAsync(ZContext context, int teamIndex)
	{
		return new ChoosePredefinedTeam(context, new int[] { teamIndex }).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> DeployAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(ClickText(context, "出战", null, screen));
	}

	/// <inheritdoc />
	public Task<OperationResult> ConfirmCommissionAgentAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		IReadOnlyList<OcrMatchResult> sortedOcrResults = GetSortedOcrResults(context, screen);
		if (FindText(sortedOcrResults, "至少选择1位代理人出战"))
		{
			OperationResult result = ClickTextByPriority(context, new string[] { "确认" }, null, screen, sortedOcrResults);
			return Task.FromResult(result.IsSuccess ? new OperationResult(IsSuccess: true, "未选择代理人", result.Data) : result);
		}
		return Task.FromResult(FindText(sortedOcrResults, "委托代行中") ? ClickTextByPriority(context, new string[] { "确认" }, null, screen, sortedOcrResults) : new OperationResult(IsSuccess: true, "无弹窗"));
	}

	/// <inheritdoc />
	public void InitAutoBattle(ZContext context, IntelBoardConfig config)
	{
		string opName = ((config.PredefinedTeamIndex == -1) ? config.AutoBattleConfig : context.TeamConfig.TeamList[config.PredefinedTeamIndex].AutoBattle);
		context.AutoBattleContext.InitAutoOp(opName);
	}

	/// <inheritdoc />
	public OperationResult CheckBattleScreenReady(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(context, screen, "战斗画面", "按键-普通攻击");
		if (findAreaResultEnum == FindAreaResultEnum.True)
		{
			return new OperationResult(IsSuccess: true, "按键-普通攻击");
		}
		FindAreaResultEnum findAreaResultEnum2 = ScreenUtils.FindArea(context, screen, "战斗画面", "按键-交互");
		if (1 == 0)
		{
		}
		OperationResult result = findAreaResultEnum2 switch
		{
			FindAreaResultEnum.True => new OperationResult(IsSuccess: true, "按键-交互"), 
			FindAreaResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 按键-交互"), 
			_ => new OperationResult(IsSuccess: false, "未找到 按键-交互"), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <inheritdoc />
	public Task<OperationResult> PreBattleMoveAsync(ZContext context, IntelBoardCommissionType? commissionType)
	{
		if (commissionType == IntelBoardCommissionType.NotoriousHunt)
		{
			return new NotoriousHuntMove(context).ExecuteAsync();
		}
		if (context.Controller is IZzzControllerActions zzzControllerActions)
		{
			zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1.5), release: true);
			return Task.FromResult(new OperationResult(IsSuccess: true, IntelBoardCommissionType.ExpertChallenge.ToDisplayName()));
		}
		return Task.FromResult(new OperationResult(IsSuccess: false, "控制器不可用"));
	}

	/// <inheritdoc />
	public void StartAutoBattle(ZContext context)
	{
		context.AutoBattleContext.StartAutoBattle();
	}

	/// <inheritdoc />
	public Task<OperationResult> RunBattleAsync(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		string lastCheckEndResult = context.AutoBattleContext.LastCheckEndResult;
		if (lastCheckEndResult != null)
		{
			context.AutoBattleContext.StopAutoBattle();
			context.Logger.Information("情报板战斗结束: {BattleResult}", lastCheckEndResult);
			return Task.FromResult(new OperationResult(IsSuccess: true, lastCheckEndResult));
		}
		if (screen == null || screen.Empty())
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		context.AutoBattleContext.CheckBattleState(screen, screenshotTimeUtc, checkBattleEndNormalResult: true);
		LogBattleStatus(context);
		return Task.FromResult(new OperationResult(IsSuccess: false, "自动战斗中"));
	}

	private void LogBattleStatus(ZContext context)
	{
		double num = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
		if (!(num - _lastBattleStatusLogTime < 1.0))
		{
			_lastBattleStatusLogTime = num;
			AutoBattleOperator autoOp = context.AutoBattleContext.AutoOp;
			AutoBattleOperatorRuntimeSnapshot autoBattleOperatorRuntimeSnapshot = autoOp?.GetRuntimeSnapshot();
			string text = string.Join(", ", from snapshot in (from snapshot in context.AutoBattleContext.StateRecordService.GetSnapshot().Values
					where snapshot.LastRecordTime > 0.0
					orderby snapshot.LastRecordTime descending
					select snapshot).Take(8)
				select snapshot.LastValue.HasValue ? $"{snapshot.StateName}={snapshot.LastValue.Value}" : snapshot.StateName);
			context.Logger.Information("情报板自动战斗状态: InBattle={InBattle}, EndResult={EndResult}, RuntimeRunning={RuntimeRunning}, Template={Template}, Trigger={Trigger}, States={States}", context.AutoBattleContext.LastCheckInBattle, context.AutoBattleContext.LastCheckEndResult ?? "无", autoBattleOperatorRuntimeSnapshot?.IsRunning ?? false, autoOp?.TemplateName ?? "无", autoBattleOperatorRuntimeSnapshot?.TriggerDisplay ?? "无", string.IsNullOrEmpty(text) ? "无" : text);
		}
	}

	/// <inheritdoc />
	public Task<OperationResult> CheckBackToListAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult((screen != null && FindText(GetSortedOcrResults(context, screen), "周期内可获取")) ? new OperationResult(IsSuccess: true, "周期内可获取") : new OperationResult(IsSuccess: false, "未回到列表"));
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickSettlementButtonAsync(ZContext context, Mat? screen)
	{
		return Task.FromResult(ClickTextByPriority(context, new string[3] { "完成", "下一步", "确认" }, null, screen, null, requireClickSuccess: false));
	}

	/// <inheritdoc />
	public Task<OperationResult> ReadProgressAsync(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("情报板", "进度文本");
		if (area == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "区域未配置 进度文本"));
		}
		using Mat image = CvImageUtils.Crop(screen, area.Rect);
		string text = context.OcrService.RunOcrSingleLineForCrop(
			image,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
		context.Logger.Information("情报板进度 OCR: {ProgressText}", text);
		string source = text.Replace('／', '/');
		string text2 = new string(source.Where((char ch) => char.IsDigit(ch) || ch == '/').ToArray());
		if (!text2.Contains('/', StringComparison.Ordinal))
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, null, 0));
		}
		string s = text2.Split('/')[0];
		int result;
		return int.TryParse(s, out result) ? Task.FromResult(new OperationResult(IsSuccess: true, null, result)) : Task.FromResult(new OperationResult(IsSuccess: false, "解析进度文本失败: " + text));
	}

	private static Mat? Screenshot(ZContext context)
	{
		return context.Controller?.Screenshot().Screen;
	}

	private OperationResult FindAndClickArea(ZContext context, string screenName, string areaName, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		DelayBeforeClick();
		return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, screenName, areaName), areaName);
	}

	private static bool FindArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		return ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private OperationResult ClickArea(ZContext context, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 " + areaName);
		}
		ControllerBase? controller = context.Controller;
		OperationResult result;
		if (controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (controller.Click(position, null, pcAlt, gamepadKey))
			{
				result = new OperationResult(IsSuccess: true, areaName);
				goto IL_0088;
			}
		}
		result = new OperationResult(IsSuccess: false, "点击失败 " + areaName);
		goto IL_0088;
		IL_0088:
		return result;
	}

	private OperationResult ClickText(ZContext context, string targetText, OneDragon.Core.Screen.ScreenArea? area = null, Mat? screen = null)
	{
		return ClickTextByPriority(context, new string[] { targetText }, area, screen);
	}

	private OperationResult ClickTextByPriority(ZContext context, IReadOnlyList<string> targetTextList, OneDragon.Core.Screen.ScreenArea? area = null, Mat? screen = null, IReadOnlyList<OcrMatchResult>? results = null, bool requireClickSuccess = true)
	{
		return ClickTextByPriorityWithDetails(context, targetTextList, area, screen, results, requireClickSuccess).Result;
	}

	private OcrClickAttempt ClickTextByPriorityWithDetails(ZContext context, IReadOnlyList<string> targetTextList, OneDragon.Core.Screen.ScreenArea? area = null, Mat? screen = null, IReadOnlyList<OcrMatchResult>? results = null, bool requireClickSuccess = true)
	{
		if (screen == null)
		{
			return new OcrClickAttempt(new OperationResult(IsSuccess: false, "未获取截图"), null);
		}
		if (results == null)
		{
			results = context.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		}
		Dictionary<string, OcrMatchResult> dictionary = new Dictionary<string, OcrMatchResult>();
		foreach (OcrMatchResult result in results)
		{
			int? num = StringUtils.FindBestMatchByDifflib(result.Text, targetTextList);
			if (num.HasValue)
			{
				string key = targetTextList[num.Value];
				if (!dictionary.TryGetValue(key, out var value) || result.Confidence > value.Confidence)
				{
					dictionary[key] = result;
				}
			}
		}
		foreach (string targetText in targetTextList)
		{
			if (!dictionary.TryGetValue(targetText, out var value2))
			{
				continue;
			}
			DelayBeforeClick();
			ControllerBase? controller = context.Controller;
			int num2;
			if (controller == null)
			{
				num2 = 0;
			}
			else
			{
				OneDragon.Core.Abstractions.Geometry.Point? position = value2.Center;
				bool pcAlt = area?.PcAlt ?? false;
				string gamepadAction = area?.GamepadKey;
				num2 = (controller.Click(position, null, pcAlt, gamepadAction) ? 1 : 0);
			}
			bool flag = (byte)num2 != 0;
			OcrClickTargetSummary target = new OcrClickTargetSummary
			{
				TargetText = targetText,
				MatchedText = value2.Text,
				Confidence = value2.Confidence,
				X = value2.X,
				Y = value2.Y,
				Width = value2.Width,
				Height = value2.Height,
				ClickX = value2.Center.X,
				ClickY = value2.Center.Y,
				PcAlt = (area?.PcAlt ?? false),
				GamepadAction = area?.GamepadKey,
				ControllerAccepted = flag
			};
			return new OcrClickAttempt((flag || !requireClickSuccess) ? new OperationResult(IsSuccess: true, targetText) : new OperationResult(IsSuccess: false, "点击失败 " + targetText), target);
		}
		return new OcrClickAttempt(new OperationResult(IsSuccess: false, "找不到 " + string.Join("/", targetTextList)), null);
	}

	private static bool FindText(IReadOnlyList<OcrMatchResult> results, string targetText, double lcsPercent = 0.5)
	{
		return results.Any((OcrMatchResult result) => StringUtils.FindByLcs(targetText, result.Text, lcsPercent));
	}

	private static bool FindButtonText(IReadOnlyList<OcrMatchResult> results, string targetText)
	{
		return results.Any((OcrMatchResult result) => StringUtils.FindBestMatchByDifflib(result.Text, new string[] { targetText }).HasValue);
	}

	private static IReadOnlyList<OcrMatchResult> GetSortedOcrResults(ZContext context, Mat screen)
	{
		return (from result in context.OcrService.GetOcrResultList(screen)
			orderby result.Y, result.X
			select result).ToArray();
	}

	private static object BuildIntelBoardActionSummary(ZContext context, Mat screen, IReadOnlyList<OcrMatchResult> results)
	{
		return new
		{
			active_screen_name = ScreenUtils.GetMatchScreenName(context, screen),
			ocr_texts = results.Select((OcrMatchResult result) => result.Text).ToArray(),
			has_accept_button = FindButtonText(results, "接取委托"),
			has_go_button = FindButtonText(results, "前往"),
			has_commission_agent = FindText(results, "委托代行中"),
			has_predefined_team = FindText(results, "预备编队"),
			has_accept_failed = FindText(results, "接取失败", 0.75),
			has_next_step = FindButtonText(results, "下一步"),
			has_no_reward_mode = FindButtonText(results, "无报酬模式")
		};
	}

	private static object BuildFunctionGuideSummary(ZContext context, Mat? screen, string screenName, string areaName)
	{
		if (screen == null)
		{
			return new
			{
				active_screen_name = (string)null,
				area_result = "未获取截图",
				ocr_texts = Array.Empty<string>()
			};
		}
		IReadOnlyList<string> ocr_texts = (from result in context.OcrService.GetOcrResultList(screen)
			orderby result.Y, result.X
			select result.Text).ToArray();
		return new
		{
			active_screen_name = ScreenUtils.GetMatchScreenName(context, screen),
			area_result = ScreenUtils.FindArea(context, screen, screenName, areaName).ToString(),
			ocr_texts = ocr_texts
		};
	}

	private static AreaClickTargetSummary GetAreaClickTargetSummary(ZContext context, Mat? screen, string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new AreaClickTargetSummary
			{
				ScreenName = screenName,
				AreaName = areaName,
				FailureReason = "area not configured"
			};
		}
		AreaClickTargetSummary areaClickTargetSummary = new AreaClickTargetSummary
		{
			ScreenName = screenName,
			AreaName = areaName,
			AreaKind = (area.IsTextArea ? "text" : (area.IsTemplateArea ? "template" : "static")),
			ClickX = area.Center.X,
			ClickY = area.Center.Y,
			PcAlt = area.PcAlt,
			GamepadAction = area.GamepadKey
		};
		if (screen != null && area.IsTemplateArea)
		{
			MatchResult matchResult = ScreenUtils.FindTemplateCoordInArea(context, screen, screenName, areaName);
			if (matchResult != null)
			{
				areaClickTargetSummary.ClickX = matchResult.Center.X;
				areaClickTargetSummary.ClickY = matchResult.Center.Y;
				areaClickTargetSummary.MatchConfidence = matchResult.Confidence;
			}
			else
			{
				areaClickTargetSummary.FailureReason = "template target not found; configured area center is diagnostic metadata only and is not clicked";
			}
		}
		return areaClickTargetSummary;
	}

	private static OperationResult ConvertClickResult(OcrClickResultEnum result, string targetName)
	{
		if (1 == 0)
		{
		}
		OperationResult result2 = result switch
		{
			OcrClickResultEnum.OcrClickSuccess => new OperationResult(IsSuccess: true, targetName), 
			OcrClickResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 " + targetName), 
			OcrClickResultEnum.OcrClickFail => new OperationResult(IsSuccess: false, "点击失败 " + targetName), 
			_ => new OperationResult(IsSuccess: false, "未找到 " + targetName), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}

	private static bool IsOwnCommission(OcrMatchResult commission, MatchResultList stars)
	{
		foreach (MatchResult star in stars)
		{
			if (commission.X < star.X + star.Width + 30 && star.X < commission.X + commission.Width)
			{
				return true;
			}
		}
		return false;
	}

	private void DelayBeforeClick()
	{
		if (_preClickDelay > TimeSpan.Zero)
		{
			Thread.Sleep(_preClickDelay);
		}
	}
}
