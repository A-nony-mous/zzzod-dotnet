using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码输入流程。
/// </summary>
public sealed class RedemptionCodeOperation : ZOperation
{
	private static readonly TimeSpan WaitDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan InputDelay = TimeSpan.FromSeconds(6L);

	private readonly RedemptionCodeRunRecord _runRecord;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly Action<ZContext, string> _inputText;

	private readonly Action<TimeSpan> _delay;

	private readonly Func<OperationRoundResult> _clickInputBox;

	private readonly Func<OperationRoundResult> _clickRedeem;

	private readonly Func<OperationRoundResult> _confirmRedeem;

	private IReadOnlyList<string> _unusedCodeList = Array.Empty<string>();

	private int _codeIndex;

	/// <summary>
	/// 初始化兑换码输入流程。
	/// </summary>
	public RedemptionCodeOperation(ZContext context, RedemptionCodeRunRecord runRecord, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, Action<ZContext, string>? inputText = null, Func<OperationRoundResult>? clickInputBox = null, Func<OperationRoundResult>? clickRedeem = null, Func<OperationRoundResult>? confirmRedeem = null, Action<TimeSpan>? delay = null)
		: base(context, "兑换码")
	{
		_runRecord = runRecord;
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_inputText = inputText ?? new Action<ZContext, string>(DefaultInputText);
		_clickInputBox = clickInputBox ?? ((Func<OperationRoundResult>)(() => RoundByClickArea("菜单", "兑换码输入框")));
		_clickRedeem = clickRedeem ?? ((Func<OperationRoundResult>)delegate
		{
			Mat? lastScreenshot = base.LastScreenshot;
			TimeSpan? successDelay = WaitDelay;
			TimeSpan? retryDelay = WaitDelay;
			return RoundByFindAndClickArea(lastScreenshot, "菜单", "兑换码兑换", null, successDelay, retryDelay);
		});
		_confirmRedeem = confirmRedeem ?? ((Func<OperationRoundResult>)(() => RoundByFindAndClickArea(base.LastScreenshot, "菜单", "兑换码兑换")));
		_delay = delay ?? new Action<TimeSpan>(Thread.Sleep);
	}

	/// <summary>
	/// 检测是否有新兑换码。
	/// </summary>
	[OperationNode("检测新兑换码", IsStartNode = true)]
	public OperationRoundResult CheckNewCode()
	{
		_unusedCodeList = _runRecord.GetUnusedCodeList(_runRecord.Dt);
		return (_unusedCodeList.Count == 0) ? RoundSuccess("无新的兑换码") : RoundSuccess("有新的兑换码");
	}

	/// <summary>
	/// 打开菜单。
	/// </summary>
	[NodeFrom("检测新兑换码", Status = "有新的兑换码")]
	[OperationNode("打开菜单")]
	public OperationRoundResult OpenMenu()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByGotoScreen(lastScreenshot, "菜单", null, null, retryDelay);
	}

	/// <summary>
	/// 点击更多。
	/// </summary>
	[NodeFrom("打开菜单")]
	[OperationNode("点击更多")]
	public OperationRoundResult ClickMore()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("菜单", "底部列表");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByOcrAndClick(lastScreenshot, "更多", area, 0.6, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 点击兑换码入口。
	/// </summary>
	[NodeFrom("点击更多")]
	[OperationNode("点击兑换码")]
	public OperationRoundResult ClickCode()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("菜单", "更多功能区域");
		_codeIndex = 0;
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = WaitDelay;
		TimeSpan? retryDelay = WaitDelay;
		return RoundByOcrAndClick(lastScreenshot, "兑换码", area, 0.6, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 输入当前兑换码并点击兑换。
	/// </summary>
	[NodeFrom("点击兑换码")]
	[NodeFrom("兑换后确认")]
	[OperationNode("输入兑换码")]
	public OperationRoundResult InputCode()
	{
		if (_codeIndex >= _unusedCodeList.Count)
		{
			return RoundSuccess("全部兑换完毕");
		}
		_clickInputBox();
		_delay(WaitDelay);
		_inputText(base.ZContext, _unusedCodeList[_codeIndex]);
		_delay(InputDelay);
		return _clickRedeem();
	}

	/// <summary>
	/// 确认兑换结果并记录已使用兑换码。
	/// </summary>
	[NodeFrom("输入兑换码", Status = "兑换码兑换")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
	[OperationNode("兑换后确认")]
	public OperationRoundResult ConfirmCode()
	{
		OperationRoundResult operationRoundResult = _confirmRedeem();
		if (operationRoundResult.IsSuccess)
		{
			_runRecord.AddUsedCode(_unusedCodeList[_codeIndex]);
			_codeIndex++;
			return RoundSuccess(operationRoundResult.Status, null, WaitDelay);
		}
		return RoundRetry(operationRoundResult.Status, null, WaitDelay);
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("输入兑换码", Status = "全部兑换完毕")]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> Back()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private static void DefaultInputText(ZContext context, string code)
	{
		context.Controller?.InputText(code);
	}
}
