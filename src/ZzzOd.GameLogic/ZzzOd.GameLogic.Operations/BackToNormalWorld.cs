using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations.HollowZero;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 尝试从任意常见界面返回大世界。
/// </summary>
public sealed class BackToNormalWorld : ZOperation
{
	private const string UnrecognizedScreenStatus = "未能识别当前画面";

	private static readonly string[] WorldScreens = new string[2] { "大世界-普通", "大世界-勘域" };

	private readonly bool _ensureNormalWorld;

	private readonly bool _allowBattle;

	private readonly Func<ZContext, Task<OperationResult>>? _transportToVideoStoreAsync;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private bool _clickExitBattle;

	private bool _preferDialogConfirm;

	/// <summary>
	/// 初始化返回大世界操作。
	/// </summary>
	public BackToNormalWorld(ZContext context, bool ensureNormalWorld = false, bool allowBattle = false, Func<ZContext, Task<OperationResult>>? transportToVideoStoreAsync = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null, TimeSpan? timeout = null)
		: base(context, "返回大世界", 3, timeout?.TotalSeconds ?? (-1.0))
	{
		_ensureNormalWorld = ensureNormalWorld;
		_allowBattle = allowBattle;
		_transportToVideoStoreAsync = transportToVideoStoreAsync;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_clickExitBattle = false;
		_preferDialogConfirm = false;
		return Task.CompletedTask;
	}

	[NodeFrom("打开地图", Success = false)]
	[NodeFrom("执行传送")]
	[NodeFrom("执行传送", Success = false)]
	[NodeFrom("确认脱离卡死", Success = false)]
	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 60)]
	private async Task<OperationRoundResult> CheckScreenAndRun()
	{
		string currentScreen = CheckAndUpdateCurrentScreen();
		if (currentScreen != null && WorldScreens.Contains<string>(currentScreen, StringComparer.Ordinal))
		{
			if (string.Equals(currentScreen, "大世界-勘域", StringComparison.Ordinal))
			{
				bool alreadyTransport = string.Equals(base.PreviousNode.Name, "执行传送", StringComparison.Ordinal) && base.PreviousNode.IsSuccess;
				if (_ensureNormalWorld && !alreadyTransport)
				{
					return RoundSuccess("传送到录像店");
				}
			}
			return RoundSuccess(currentScreen);
		}
		OperationRoundResult gotoNormalWorld = RoundByGotoScreen(base.LastScreenshot, "大世界-普通");
		if (gotoNormalWorld.IsSuccess)
		{
			return RoundSuccess(gotoNormalWorld.Status);
		}
		if (!gotoNormalWorld.IsFail && base.ZContext.ScreenContext.CurrentScreenName != null && !string.Equals(gotoNormalWorld.Status, "未能识别当前画面", StringComparison.Ordinal))
		{
			return RoundWait(gotoNormalWorld.Status, null, _retryDelay);
		}
		WorldPatrolMiniMapSnapshot miniMap = base.ZContext.WorldPatrolService.CutMiniMap(base.ZContext, base.LastScreenshot);
		if (miniMap.PlayMaskFound)
		{
			return RoundSuccess("发现地图");
		}
		OperationRoundResult commonStreet = FindAndClick("画面-通用", "左上角-街区");
		if (commonStreet.IsSuccess)
		{
			return RoundRetry(commonStreet.Status, null, _retryDelay);
		}
		OperationRoundResult exitBattle = FindAndClickWithEvidence("战斗-菜单", "按钮-退出战斗", "画面识别", "战斗-菜单/按钮-退出战斗", "退出战斗确认弹窗");
		if (exitBattle.IsSuccess)
		{
			_clickExitBattle = true;
			return RoundRetry(exitBattle.Status, null, _retryDelay);
		}
		if (_clickExitBattle)
		{
			OperationRoundResult confirmExitBattle = FindAndClickWithEvidence("战斗-菜单", "按钮-退出战斗-确认", "画面识别", "战斗-菜单/按钮-退出战斗-确认", "退出战斗后回到大世界或结算页面");
			if (confirmExitBattle.IsSuccess)
			{
				return RoundRetry(confirmExitBattle.Status, null, _retryDelay);
			}
		}
		_clickExitBattle = false;
		OperationRoundResult escapeStuck = FindAndClick("战斗-菜单", "按钮-脱离卡死");
		if (escapeStuck.IsSuccess)
		{
			return RoundSuccess("脱离卡死", null, _retryDelay);
		}
		OperationRoundResult back = FindAndClick("画面-通用", "返回");
		if (back.IsSuccess)
		{
			return RoundRetry(back.Status, null, _retryDelay);
		}
		OperationRoundResult close = FindAndClick("画面-通用", "关闭");
		if (close.IsSuccess)
		{
			return RoundRetry(close.Status, null, _retryDelay);
		}
		OperationRoundResult done = FindAndClick("画面-通用", "完成");
		if (done.IsSuccess)
		{
			return RoundRetry(done.Status, null, _retryDelay);
		}
		OperationRoundResult hollowExit = await TryExitHollowAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (hollowExit != null)
		{
			return hollowExit;
		}
		string firstArea = (_preferDialogConfirm ? "对话框确认" : "对话框取消");
		string secondArea = (_preferDialogConfirm ? "对话框取消" : "对话框确认");
		OperationRoundResult firstDialog = FindAndClick("大世界", firstArea);
		if (firstDialog.IsSuccess)
		{
			_preferDialogConfirm = !_preferDialogConfirm;
			return RoundRetry(firstDialog.Status, null, _retryDelay);
		}
		OperationRoundResult secondDialog = FindAndClick("大世界", secondArea);
		if (secondDialog.IsSuccess)
		{
			_preferDialogConfirm = !_preferDialogConfirm;
			return RoundRetry(secondDialog.Status, null, _retryDelay);
		}
		OperationRoundResult compendiumExit = CheckCompendium();
		if (compendiumExit != null)
		{
			return compendiumExit;
		}
		if (CheckAgentDialog())
		{
			OperationRoundResult agentDialog = HandleAgentDialog();
			if (agentDialog != null)
			{
				return agentDialog;
			}
		}
		OperationRoundResult battle = RoundByFindArea(base.LastScreenshot, "战斗画面", "按键-普通攻击");
		if (battle.IsSuccess)
		{
			if (_allowBattle)
			{
				return RoundSuccess("大世界-战斗");
			}
			return ClickBattleMenuFromHud();
		}
		OperationRoundResult clickBack = RoundByClickArea("画面-通用", "返回", clickLeftTop: false, TimeSpan.Zero);
		return clickBack.IsSuccess ? RoundRetry(clickBack.Status, null, TimeSpan.FromMilliseconds(500L)) : RoundFail();
	}

	[NodeFrom("画面识别", Status = "脱离卡死")]
	[OperationNode("确认脱离卡死")]
	private OperationRoundResult ConfirmEscapeStuck()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		return RoundByFindAndClickArea(lastScreenshot, "战斗-菜单", "按钮-脱离卡死-确认", preDelay, null, retryDelay);
	}

	[NodeFrom("画面识别", Status = "传送到录像店")]
	[NodeFrom("确认脱离卡死")]
	[OperationNode("打开地图", NodeMaxRetryTimes = 60)]
	private OperationRoundResult OpenMap()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		return RoundByFindAndClickArea(lastScreenshot, "大世界", "地图", preDelay, null, retryDelay);
	}

	[NodeFrom("打开地图")]
	[OperationNode("执行传送")]
	private async Task<OperationRoundResult> DoTransport()
	{
		OperationResult result;
		if (_transportToVideoStoreAsync != null)
		{
			result = await _transportToVideoStoreAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			MapTransport operation = new MapTransport(base.ZContext, "录像店", "房间", _retryDelay, _preClickDelay);
			result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		return RoundByOperationResult(result);
	}

	private OperationRoundResult FindAndClick(string screenName, string areaName)
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindAndClickArea(lastScreenshot, screenName, areaName, preDelay, null, retryDelay);
	}

	private async Task<OperationRoundResult?> TryExitHollowAsync()
	{
		if (base.LastScreenshot == null)
		{
			return null;
		}
		OperationRoundResult backpack = RoundByFindArea(base.LastScreenshot, "零号空洞-事件", "背包");
		if (!backpack.IsSuccess)
		{
			return null;
		}
		await new HollowExitByMenu(base.ZContext, _retryDelay).ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
		return RoundRetry("空洞内", null, _retryDelay);
	}

	private OperationRoundResult? CheckCompendium()
	{
		if (base.LastScreenshot == null)
		{
			return null;
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("快捷手册", "TAB列表");
		if (area == null)
		{
			return null;
		}
		string[] targetWords = (from tab in base.ZContext.CompendiumService.Data.TabList
			select tab.TabName into tab
			where !string.IsNullOrWhiteSpace(tab)
			select tab).ToArray();
		int num = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect).Count((OcrMatchResult result) => StringUtils.FindBestMatchByDifflib(result.Text, targetWords).HasValue);
		if (num < 2)
		{
			return null;
		}
		OperationRoundResult operationRoundResult = RoundByClickArea("快捷手册", "按钮-退出");
		return operationRoundResult.IsSuccess ? RoundRetry(operationRoundResult.Status, null, _retryDelay) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	private bool CheckAgentDialog()
	{
		if (base.LastScreenshot == null)
		{
			return false;
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("大世界", "好感度标题");
		if (area == null)
		{
			return false;
		}
		string[] array = AgentEnum.Values.Select((AgentEnum agent) => agent.Value.AgentName).Append("小黑").ToArray();
		string[] array2 = (from result in base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect)
			select result.Text).ToArray();
		string[] array3 = array2;
		foreach (string text in array3)
		{
			int? num2 = StringUtils.FindBestMatchByDifflib(text, array);
			if (num2.HasValue)
			{
				int? num3 = StringUtils.FindBestMatchByDifflib(array[num2.Value], array2);
				if (num3.HasValue && string.Equals(array2[num3.Value], text, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		return false;
	}

	private OperationRoundResult? HandleAgentDialog()
	{
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return null;
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("大世界", "好感度选项");
		if (area == null)
		{
			return null;
		}
		OcrMatchResult[] array = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect).ToArray();
		if (array.Length == 0)
		{
			OperationRoundResult operationRoundResult = RoundByClickArea("菜单", "返回");
			return RoundWait(operationRoundResult.IsSuccess ? "对话无选项" : operationRoundResult.Status, null, _retryDelay);
		}
		OcrMatchResult ocrMatchResult = ((array.Length > 1) ? array[1] : array[0]);
		return base.ZContext.Controller.Click(ocrMatchResult.Center) ? RoundWait(ocrMatchResult.Text, null, _retryDelay) : RoundRetry("点击失败 " + ocrMatchResult.Text, null, _retryDelay);
	}

	private OperationRoundResult ClickBattleMenuFromHud()
	{
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId() + "-back-to-normal-world-open-battle-menu");
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot) : null);
		object beforeRecognitionSummary = (isEnabled ? BuildBattleRecoverySummary(base.LastScreenshot) : null);
		AreaClickTargetSummary actionTargetDetails = (isEnabled ? GetAreaClickTargetSummary("战斗画面", "菜单") : null);
		OperationRoundResult operationRoundResult = RoundByClickArea("战斗画面", "菜单", clickLeftTop: false, TimeSpan.Zero);
		if (isEnabled)
		{
			Thread.Sleep(_retryDelay);
			using Mat mat = CaptureScreenshotForEvidence();
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			object afterRecognitionSummary = BuildBattleRecoverySummary(mat);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
				OperationName = "返回大世界",
				NodeName = "画面识别",
				DotNetMethod = "ZzzOd.GameLogic.Operations.BackToNormalWorld.ClickBattleMenuFromHud()",
				BaselineParityRequirement = "BackToNormalWorld clicks 战斗画面/菜单 before clicking 战斗-菜单/按钮-退出战斗.",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = beforeRecognitionSummary,
				ActionKind = "click_area",
				ActionTarget = "战斗画面/菜单",
				ActionTargetDetails = actionTargetDetails,
				ExpectedNextState = "战斗-菜单 with 按钮-退出战斗 visible",
				AfterScreenshotPath = afterScreenshotPath,
				AfterRecognitionSummary = afterRecognitionSummary,
				TransitionResult = (IsBattleMenuVisible(mat) ? "battle_menu_visible" : (operationRoundResult.IsSuccess ? "click_sent_waiting_for_battle_menu" : "action_failed")),
				FailureReason = (operationRoundResult.IsSuccess ? null : operationRoundResult.Status),
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
		return operationRoundResult.IsSuccess ? RoundRetry(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L)) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	private OperationRoundResult FindAndClickWithEvidence(string screenName, string areaName, string nodeName, string actionTarget, string expectedNextState)
	{
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId() + "-back-to-normal-world-" + SanitizeFilePart(nodeName));
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot) : null);
		object beforeRecognitionSummary = (isEnabled ? BuildBattleRecoverySummary(base.LastScreenshot) : null);
		AreaClickTargetSummary actionTargetDetails = (isEnabled ? GetAreaClickTargetSummary(screenName, areaName) : null);
		OperationRoundResult operationRoundResult = FindAndClick(screenName, areaName);
		if (isEnabled)
		{
			Thread.Sleep(_retryDelay);
			using Mat mat = CaptureScreenshotForEvidence();
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			object afterRecognitionSummary = BuildBattleRecoverySummary(mat);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
				OperationName = "返回大世界",
				NodeName = nodeName,
				DotNetMethod = "ZzzOd.GameLogic.Operations.BackToNormalWorld.FindAndClickWithEvidence()",
				BaselineParityRequirement = "BackToNormalWorld clicks configured battle menu areas to exit battle.",
				BeforeScreenshotPath = beforeScreenshotPath,
				BeforeRecognitionSummary = beforeRecognitionSummary,
				ActionKind = "click_area",
				ActionTarget = actionTarget,
				ActionTargetDetails = actionTargetDetails,
				ExpectedNextState = expectedNextState,
				AfterScreenshotPath = afterScreenshotPath,
				AfterRecognitionSummary = afterRecognitionSummary,
				TransitionResult = (operationRoundResult.IsSuccess ? "click_sent" : "action_failed"),
				FailureReason = (operationRoundResult.IsSuccess ? null : operationRoundResult.Status),
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
		return operationRoundResult;
	}

	private object BuildBattleRecoverySummary(Mat? screen)
	{
		if (screen == null)
		{
			return new
			{
				failure_reason = "未获取截图"
			};
		}
		IReadOnlyList<string> ocr_texts;
		try
		{
			ocr_texts = (from result in base.ZContext.OcrService.GetOcrResultList(screen)
				orderby result.Y, result.X
				select result.Text).Take(30).ToArray();
		}
		catch (Exception ex)
		{
			ocr_texts = new string[] { "OCR failed: " + ex.Message };
		}
		return new
		{
			active_screen = ScreenUtils.GetMatchScreenName(base.ZContext, screen),
			battle_hud_normal_attack = SafeFindArea(screen, "战斗画面", "按键-普通攻击"),
			battle_hud_menu = SafeFindArea(screen, "战斗画面", "菜单"),
			battle_menu_exit = SafeFindArea(screen, "战斗-菜单", "按钮-退出战斗"),
			battle_menu_confirm = SafeFindArea(screen, "战斗-菜单", "按钮-退出战斗-确认"),
			world_screen = ScreenUtils.GetMatchScreenName(base.ZContext, screen, WorldScreens),
			ocr_texts = ocr_texts
		};
	}

	private string SafeFindArea(Mat screen, string screenName, string areaName)
	{
		try
		{
			return ScreenUtils.FindArea(base.ZContext, screen, screenName, areaName).ToString();
		}
		catch (Exception ex)
		{
			return "error: " + ex.Message;
		}
	}

	private AreaClickTargetSummary GetAreaClickTargetSummary(string screenName, string areaName)
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return new AreaClickTargetSummary
			{
				ScreenName = screenName,
				AreaName = areaName,
				FailureReason = "area not configured"
			};
		}
		return new AreaClickTargetSummary
		{
			ScreenName = screenName,
			AreaName = areaName,
			AreaKind = (string.IsNullOrWhiteSpace(area.Text) ? "configured_area" : "ocr_or_template_area"),
			ClickX = area.Center.X,
			ClickY = area.Center.Y,
			PcAlt = area.PcAlt,
			GamepadAction = area.GamepadKey
		};
	}

	private Mat? CaptureScreenshotForEvidence()
	{
		if (base.ZContext.Controller == null)
		{
			return null;
		}
		return base.ZContext.Controller.Screenshot().Screen;
	}

	private bool IsBattleMenuVisible(Mat? screen)
	{
		return screen != null && ScreenUtils.FindArea(base.ZContext, screen, "战斗-菜单", "按钮-退出战斗") == FindAreaResultEnum.True;
	}

	private static string SanitizeFilePart(string value)
	{
		char[] value2 = value.Select((char ch) => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
		return new string(value2).Trim('-');
	}
}
