using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.EmailApp;

/// <summary>
/// 邮件领取流程。
/// </summary>
public sealed class EmailOperation : ZOperation
{
	private static readonly TimeSpan WaitDelay = TimeSpan.FromSeconds(1L);

	/// <summary>
	/// 初始化邮件领取流程。
	/// </summary>
	public EmailOperation(ZContext context)
		: base(context, "邮件", 1)
	{
	}

	/// <summary>
	/// 打开邮件画面。
	/// </summary>
	[OperationNode("打开邮件", IsStartNode = true)]
	public OperationRoundResult GotoEmail()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByGotoScreen(lastScreenshot, "邮件", null, null, retryDelay);
	}

	/// <summary>
	/// 点击全部领取。
	/// </summary>
	[NodeFrom("打开邮件")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
	[OperationNode("全部领取")]
	public OperationRoundResult ClickGetAll()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot, "邮件", "全部领取", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 点击确认或确定。
	/// </summary>
	[NodeFrom("全部领取")]
	[OperationNode("确认")]
	public OperationRoundResult ClickConfirm()
	{
		string[] targetTextList = new string[2] { "确认", "确定" };
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByOcrAndClickByPriority(lastScreenshot, targetTextList, null, 0.5, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 返回菜单。
	/// </summary>
	[NodeFrom("确认")]
	[NodeFrom("确认", Success = false)]
	[NodeFrom("全部领取", Success = false)]
	[OperationNode("返回菜单")]
	public OperationRoundResult BackToMenu()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByFindAndClickArea(lastScreenshot, "菜单", "返回", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("返回菜单")]
	[NodeFrom("返回菜单", Success = false)]
	[OperationNode("返回大世界", ScreenshotBeforeRound = false)]
	public async Task<OperationRoundResult> BackToWorld()
	{
		BackToNormalWorld operation = new BackToNormalWorld(base.ZContext);
		return RoundByOperationResult(await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false));
	}
}
