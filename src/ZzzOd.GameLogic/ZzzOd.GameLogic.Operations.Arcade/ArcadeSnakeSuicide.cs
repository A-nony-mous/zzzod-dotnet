using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Operations.Arcade;

/// <summary>
/// 蛇对蛇自杀刷指定次数。
/// </summary>
public sealed class ArcadeSnakeSuicide : ZOperation
{
	private readonly int _totalCount;

	private readonly Func<ZContext, Task<OperationResult>> _startGameAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>已完成次数。</summary>
	public int FinishCount { get; private set; }

	/// <summary>
	/// 初始化蛇对蛇自杀操作。
	/// </summary>
	public ArcadeSnakeSuicide(ZContext context, int totalCount, Func<ZContext, Task<OperationResult>>? startGameAsync = null, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "蛇对蛇自杀")
	{
		_totalCount = Math.Max(0, totalCount);
		_startGameAsync = startGameAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultStartGameAsync);
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		FinishCount = 0;
		return Task.CompletedTask;
	}

	[OperationNode("进入游戏", IsStartNode = true)]
	private async Task<OperationRoundResult> StartGame()
	{
		return RoundByOperationResult(await _startGameAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("进入游戏")]
	[NodeFrom("点击空白处继续", Status = "蛇对蛇-点击空白处继续")]
	[OperationNode("等待加载", NodeMaxRetryTimes = 20)]
	private OperationRoundResult WaitGameLoad()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = _retryDelay;
		return RoundByFindArea(lastScreenshot, "电玩店", "蛇对蛇-加载完成", null, retryDelay);
	}

	[NodeFrom("等待加载")]
	[OperationNode("点击空白处继续", NodeMaxRetryTimes = 20)]
	private OperationRoundResult ClickEmpty()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "电玩店", "蛇对蛇-点击空白处继续");
		if (operationRoundResult.IsSuccess)
		{
			FinishCount++;
			if (FinishCount >= _totalCount)
			{
				return RoundSuccess();
			}
			return RoundByFindAndClickArea(base.LastScreenshot, "电玩店", "蛇对蛇-点击空白处继续", _preClickDelay, TimeSpan.FromSeconds(3L), _retryDelay);
		}
		if (!(base.ZContext.Controller is IZzzControllerActions zzzControllerActions))
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		zzzControllerActions.MoveW(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		return RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("点击空白处继续")]
	[OperationNode("返回大世界")]
	private async Task<OperationRoundResult> BackToNormalWorld()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultStartGameAsync(ZContext context)
	{
		return new ArcadeStartGame(context, "蛇对蛇").ExecuteAsync();
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}
