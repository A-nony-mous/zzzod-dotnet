using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Operations.Arcade;

/// <summary>
/// 在电玩店开始一局街机游戏。
/// </summary>
public sealed class ArcadeStartGame : ZOperation
{
	private readonly string _gameName;

	private readonly Func<ZContext, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, Task<OperationResult>> _waitNormalWorldAsync;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化街机启动操作。
	/// </summary>
	public ArcadeStartGame(ZContext context, string gameName, Func<ZContext, Task<OperationResult>>? transportAsync = null, Func<ZContext, Task<OperationResult>>? waitNormalWorldAsync = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "开始街机游戏 " + gameName)
	{
		_gameName = gameName;
		_transportAsync = transportAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultTransportAsync);
		_waitNormalWorldAsync = waitNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultWaitNormalWorldAsync);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[OperationNode("传送", IsStartNode = true)]
	private async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _transportAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("传送")]
	[OperationNode("等待大世界加载")]
	private async Task<OperationRoundResult> WaitWorld()
	{
		return RoundByOperationResult(await _waitNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("等待大世界加载")]
	[OperationNode("移动交互")]
	private OperationRoundResult MoveAndInteract()
	{
		if (!(base.ZContext.Controller is IZzzControllerActions zzzControllerActions))
		{
			return RoundFail("控制器不支持 ZZZ 动作");
		}
		zzzControllerActions.MoveW(press: true, TimeSpan.FromSeconds(1.5), release: true);
		zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		return RoundSuccess();
	}

	[NodeFrom("移动交互")]
	[OperationNode("等待加载", NodeMaxRetryTimes = 10)]
	private OperationRoundResult WaitArcadeLoad()
	{
		return RoundByFindArea(base.LastScreenshot, "菜单", "返回", _retryDelay, _retryDelay);
	}

	[NodeFrom("等待加载")]
	[OperationNode("选择模式")]
	private OperationRoundResult ChooseMode()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("电玩店", "模式列表");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(3L);
		TimeSpan? retryDelay = _retryDelay;
		return RoundByOcrAndClick(lastScreenshot, "街机模式", area, 0.6, null, successDelay, retryDelay);
	}

	[NodeFrom("选择模式")]
	[OperationNode("选择游戏")]
	private OperationRoundResult ChooseGame()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("电玩店", "游戏名称");
		OperationRoundResult operationRoundResult = RoundByOcr(base.LastScreenshot, _gameName, area, 0.5, _retryDelay, _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		RoundByClickArea("电玩店", "下一个游戏", clickLeftTop: false, _preClickDelay);
		return RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("选择游戏")]
	[OperationNode("点击选择")]
	private OperationRoundResult ClickChoose()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "电玩店", "选择", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("点击选择")]
	[OperationNode("点击开始游戏")]
	private OperationRoundResult ClickStart()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "电玩店", "开始游戏", _preClickDelay, _retryDelay, _retryDelay);
	}

	private static Task<OperationResult> DefaultTransportAsync(ZContext context)
	{
		return new Transport(context, "六分街", "电玩店").ExecuteAsync();
	}

	private static Task<OperationResult> DefaultWaitNormalWorldAsync(ZContext context)
	{
		return new WaitNormalWorld(context).ExecuteAsync();
	}
}
