using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.EngagementReward;

/// <summary>
/// 活跃度奖励领取流程。
/// </summary>
public sealed class EngagementRewardOperation : ZOperation
{
	/// <summary>无奖励可领取。</summary>
	public const string StatusNoReward = "无奖励可领取";

	/// <summary>日常奖励领取成功。</summary>
	public const string StatusClaimSuccess = "日常奖励领取成功";

	private static readonly TimeSpan WaitDelay = TimeSpan.FromSeconds(1L);

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	/// <summary>
	/// 初始化活跃度奖励领取流程。
	/// </summary>
	public EngagementRewardOperation(ZContext context, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null)
		: base(context, "活跃度奖励")
	{
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
	}

	/// <summary>
	/// 执行前返回大世界，避免从菜单或弹窗状态开始。
	/// </summary>
	[OperationNode("返回大世界", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 前往快捷手册日常页面。
	/// </summary>
	[NodeFrom("返回大世界")]
	[OperationNode("快捷手册-日常")]
	public OperationRoundResult GotoCompendiumDaily()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByGotoScreen(lastScreenshot, "快捷手册-日常", null, null, retryDelay);
	}

	/// <summary>
	/// 点击今日最大活跃度奖励。
	/// </summary>
	[NodeFrom("快捷手册-日常")]
	[NodeFrom("查看奖励结果", Success = false)]
	[OperationNode("点击奖励")]
	public OperationRoundResult ClickReward()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot, "快捷手册", "今日最大活跃度", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 确认领取结果。
	/// </summary>
	[NodeFrom("点击奖励")]
	[OperationNode("查看奖励结果")]
	public OperationRoundResult CheckReward()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "快捷手册", "活跃度奖励-确认", null, successDelay, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess("日常奖励领取成功", null, operationRoundResult.Delay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "快捷手册", "活跃度奖励-奖励预览");
		if (operationRoundResult2.IsSuccess)
		{
			Mat? lastScreenshot2 = base.LastScreenshot;
			retryDelay = WaitDelay;
			successDelay = WaitDelay;
			OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(lastScreenshot2, "画面-通用", "关闭", null, retryDelay, successDelay);
			if (operationRoundResult3.IsSuccess)
			{
				return RoundSuccess("日常奖励已领取或活跃度未满", null, operationRoundResult3.Delay);
			}
		}
		return RoundFail("未找到确认按钮或奖励预览");
	}

	/// <summary>
	/// 识别活跃度是否已满。
	/// </summary>
	[NodeFrom("查看奖励结果")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("识别活跃度")]
	public OperationRoundResult CheckEngagement()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "快捷手册", "活跃度奖励-4");
		WriteCheckEngagementEvidence(operationRoundResult);
		return operationRoundResult.IsSuccess ? RoundSuccess("活跃度已满") : RoundFail("活跃度未满");
	}

	/// <summary>
	/// 完成后回到大世界。
	/// </summary>
	[NodeFrom("识别活跃度")]
	[NodeFrom("识别活跃度", Success = false)]
	[OperationNode("完成后返回大世界")]
	public async Task<OperationRoundResult> BackAfterwards()
	{
		await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		return base.PreviousNode.IsSuccess ? RoundSuccess() : RoundFail();
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private void WriteCheckEngagementEvidence(OperationRoundResult result)
	{
		if (ActionLevelDebugEvidenceWriter.IsEnabled && base.LastScreenshot != null)
		{
			string applicationId = ActionLevelDebugEvidenceWriter.GetApplicationId("engagement_reward");
			string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(applicationId + "-engagement-check");
			string text = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", base.LastScreenshot);
			FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "快捷手册", "活跃度奖励-4");
			MatchResult matchResult = ScreenUtils.FindTemplateCoordInArea(base.ZContext, base.LastScreenshot, "快捷手册", "活跃度奖励-4");
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = applicationId,
				OperationName = "活跃度奖励",
				NodeName = "识别活跃度",
				DotNetMethod = "EngagementRewardOperation.CheckEngagement",
				BaselineParityRequirement = "EngagementReward.check_engagement uses find_area 快捷手册/活跃度奖励-4 and succeeds only when the completed reward template is found.",
				BeforeScreenshotPath = text,
				BeforeRecognitionSummary = new
				{
					AreaResult = findAreaResultEnum.ToString(),
					RoundResult = result.Kind.ToString(),
					RoundStatus = result.Status,
					TemplateMatch = ((matchResult == null) ? null : new { matchResult.Confidence, matchResult.X, matchResult.Y, matchResult.Width, matchResult.Height })
				},
				ActionKind = "recognition",
				ActionTarget = "快捷手册/活跃度奖励-4",
				ExpectedNextState = "活跃度已满 when completed template is visible",
				AfterScreenshotPath = text,
				AfterRecognitionSummary = null,
				TransitionResult = (result.IsSuccess ? "completed_reward_template_found" : "completed_reward_template_missing"),
				FailureReason = (result.IsSuccess ? null : result.Status),
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
	}
}
