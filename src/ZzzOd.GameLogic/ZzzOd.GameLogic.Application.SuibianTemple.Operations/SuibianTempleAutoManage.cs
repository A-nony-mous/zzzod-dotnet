using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleAutoManage : SuibianTempleSubOperation
{
	private static readonly string[] AutoManageTargetTexts = new string[10] { "停止托管", "开始托管", "领取收益", "确认", "获得奖励", "托管中", "自动托管中", "可关闭自动托管进行手动操作", "经营方针", "经营" };

	private static readonly string[] AutoManageIgnoreTexts = new string[3] { "自动托管中", "可关闭自动托管进行手动操作", "经营" };

	public SuibianTempleAutoManage(ZContext context, SuibianTempleConfig config)
		: base(context, config, "随便观 自动托管")
	{
	}

	[OperationNode("检查并停止托管", IsStartNode = true)]
	public OperationRoundResult CheckAndStopHosting()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("随便观-入口", "区域-左半屏");
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem("suibian_temple-auto-manage-stop-hosting");
		SuibianTempleAutoManageRecognitionSummary beforeSummary = (isEnabled ? GetAutoManageRecognitionSummary(base.LastScreenshot, area) : null);
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot) : null);
		Mat? lastScreenshot = base.LastScreenshot;
		string[] autoManageTargetTexts = AutoManageTargetTexts;
		TimeSpan? successDelay = SuibianTempleSubOperation.OneSecond;
		TimeSpan? retryDelay = SuibianTempleSubOperation.ShortDelay;
		IReadOnlyList<string> autoManageIgnoreTexts = AutoManageIgnoreTexts;
		OperationRoundResult operationRoundResult = RoundByOcrAndClickByPriority(lastScreenshot, autoManageTargetTexts, area, 0.5, null, successDelay, retryDelay, null, cropFirst: true, autoManageIgnoreTexts);
		SuibianTempleAutoManageRecognitionSummary afterSummary = null;
		string afterScreenshotPath = null;
		bool flag = false;
		if (operationRoundResult.IsSuccess && string.Equals(operationRoundResult.Status, "停止托管", StringComparison.Ordinal))
		{
			if (isEnabled)
			{
				Thread.Sleep(SuibianTempleSubOperation.OneSecond);
				Mat mat = Screenshot();
				afterSummary = GetAutoManageRecognitionSummary(mat, area);
				afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			}
		}
		else if (isEnabled)
		{
			Mat mat2 = Screenshot();
			afterSummary = GetAutoManageRecognitionSummary(mat2, area);
			afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat2);
		}
		if (isEnabled)
		{
			WriteAutoManageEvidence(fileStem, beforeScreenshotPath, beforeSummary, afterScreenshotPath, afterSummary, operationRoundResult, flag);
		}
		if (flag)
		{
			return RoundFail("疑似停止托管循环");
		}
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry("未识别有效按钮", null, SuibianTempleSubOperation.OneSecond);
		}
		string status = operationRoundResult.Status;
		if (1 == 0)
		{
		}
		OperationRoundResult result;
		switch (status)
		{
		case "停止托管":
			result = RoundWait("点击停止", null, SuibianTempleSubOperation.OneSecond);
			break;
		case "开始托管":
			result = RoundSuccess("开始托管");
			break;
		case "领取收益":
		case "确认":
		case "获得奖励":
			result = RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
			break;
		case "托管中":
		case "经营方针":
			result = RoundWait("点击进入托管详情", null, SuibianTempleSubOperation.OneSecond);
			break;
		default:
			result = RoundRetry("未识别有效按钮", null, SuibianTempleSubOperation.OneSecond);
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private SuibianTempleAutoManageRecognitionSummary GetAutoManageRecognitionSummary(Mat? screen, OneDragon.Core.Screen.ScreenArea? area)
	{
		if (screen == null)
		{
			return new SuibianTempleAutoManageRecognitionSummary
			{
				ActiveScreenName = null,
				AreaRect = area?.Rect.ToString(),
				FailureReason = "未获取截图"
			};
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(screen, area?.ColorRange, area?.Rect);
		IReadOnlyDictionary<string, OcrMatchResult> matchMap = ZOperation.MatchOcrResultsByTargetPriority(ocrResultList, AutoManageTargetTexts, 0.5);
		IReadOnlyList<SuibianTempleOcrTargetMatch> ignoredMatches = (from target in AutoManageTargetTexts.Where((string target) => AutoManageIgnoreTexts.Contains<string>(target, StringComparer.Ordinal)).Where(matchMap.ContainsKey)
			select SuibianTempleOcrTargetMatch.From(target, matchMap[target], ignored: true)).ToArray();
		IReadOnlyList<SuibianTempleOcrTargetMatch> readOnlyList = (from target in AutoManageTargetTexts.Where((string target) => !AutoManageIgnoreTexts.Contains<string>(target, StringComparer.Ordinal)).Where(matchMap.ContainsKey)
			select SuibianTempleOcrTargetMatch.From(target, matchMap[target], ignored: false)).ToArray();
		SuibianTempleOcrTargetMatch selectedTarget = null;
		using (IEnumerator<SuibianTempleOcrTargetMatch> enumerator = readOnlyList.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				SuibianTempleOcrTargetMatch current = enumerator.Current;
				selectedTarget = current;
			}
		}
		return new SuibianTempleAutoManageRecognitionSummary
		{
			ActiveScreenName = CheckAndUpdateCurrentScreen(screen, new string[] { "随便观-入口" }),
			AreaRect = area?.Rect.ToString(),
			OcrTexts = ocrResultList.Select((OcrMatchResult result) => result.Text).ToArray(),
			IgnoredMatches = ignoredMatches,
			TargetMatches = readOnlyList,
			SelectedTarget = selectedTarget,
			StopHostingVisible = readOnlyList.Any((SuibianTempleOcrTargetMatch match) => string.Equals(match.TargetText, "停止托管", StringComparison.Ordinal)),
			StartHostingVisible = readOnlyList.Any((SuibianTempleOcrTargetMatch match) => string.Equals(match.TargetText, "开始托管", StringComparison.Ordinal)),
			ConfirmVisible = readOnlyList.Any((SuibianTempleOcrTargetMatch match) => string.Equals(match.TargetText, "确认", StringComparison.Ordinal))
		};
	}

	private static void WriteAutoManageEvidence(string fileStem, string? beforeScreenshotPath, SuibianTempleAutoManageRecognitionSummary? beforeSummary, string? afterScreenshotPath, SuibianTempleAutoManageRecognitionSummary? afterSummary, OperationRoundResult result, bool suspectedLoop)
	{
		string text;
		if (suspectedLoop)
		{
			text = "suspected_loop";
		}
		else
		{
			string text3;
			if (result.IsSuccess)
			{
				string status = result.Status;
				if (1 == 0)
				{
				}
				string text2 = ((!(status == "停止托管")) ? ((!(status == "开始托管")) ? "ocr_click_waiting_next_state" : "auto_manage_ready_to_return") : ((afterSummary != null && afterSummary.ConfirmVisible) ? "stop_clicked_confirmation_visible" : ((afterSummary == null || afterSummary.StopHostingVisible) ? "stop_clicked_state_unchanged" : "stop_clicked_state_changed")));
				if (1 == 0)
				{
				}
				text3 = text2;
			}
			else
			{
				text3 = "action_failed";
			}
			text = text3;
		}
		string transitionResult = text;
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("suibian_temple"),
			OperationName = "随便观 自动托管",
			NodeName = "检查并停止托管",
			DotNetMethod = "ZzzOd.GameLogic.Application.SuibianTemple.Operations.SuibianTempleAutoManage.CheckAndStopHosting()",
			BaselineParityRequirement = "SuibianTemple auto_manage checks OCR targets in priority order inside 随便观-入口/区域-左半屏 and ignores status texts before clicking.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = beforeSummary,
			ActionKind = (result.IsSuccess ? "ocr_click" : "ocr_click_failed"),
			ActionTarget = (result.Status ?? "停止托管/开始托管/领取收益/确认/获得奖励/托管中/经营方针"),
			ActionTargetDetails = beforeSummary?.SelectedTarget,
			ExpectedNextState = "自动托管停止后出现确认/领取/开始托管状态，或返回随便观入口",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = afterSummary,
			TransitionResult = transitionResult,
			FailureReason = (result.IsSuccess ? null : result.Status),
			RetryStoppedBecauseOfSuspectedLoop = suspectedLoop
		});
	}

	[NodeFrom("检查并停止托管", Status = "开始托管")]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[] { "随便观-入口" });
		if (text != null)
		{
			return RoundSuccess(text);
		}
		OperationRoundResult operationRoundResult = ClickText("确认");
		if (operationRoundResult.IsSuccess)
		{
			return RoundWait(operationRoundResult.Status, null, SuibianTempleSubOperation.OneSecond);
		}
		return BackToEntry();
	}
}
