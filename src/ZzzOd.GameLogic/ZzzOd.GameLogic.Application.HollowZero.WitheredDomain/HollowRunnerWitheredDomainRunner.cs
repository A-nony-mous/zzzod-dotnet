using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 基于 HollowRunner 的枯萎之。runner。
/// </summary>
public sealed class HollowRunnerWitheredDomainRunner : IWitheredDomainRunner
{
	private readonly IHollowEventSource? _eventSource;

	private readonly IHollowMapSource? _mapSource;

	private readonly IHollowMapNavigator? _mapNavigator;

	private readonly IHollowEventDispatcher? _eventDispatcher;

	/// <summary>
	/// 初始。runner。
	/// </summary>
	public HollowRunnerWitheredDomainRunner(IHollowEventSource? eventSource = null, IHollowMapSource? mapSource = null, IHollowMapNavigator? mapNavigator = null, IHollowEventDispatcher? eventDispatcher = null)
	{
		_eventSource = eventSource;
		_mapSource = mapSource;
		_mapNavigator = mapNavigator;
		_eventDispatcher = eventDispatcher;
	}

	/// <inheritdoc />
	public async Task<OperationResult> RunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, CancellationToken cancellationToken)
	{
		try
		{
			IHollowMapSource mapSource = _mapSource ?? new YoloHollowMapSource(context);
			IHollowMapNavigator mapNavigator = _mapNavigator ?? new WitheredDomainMapNavigator(context);
			IHollowEventSource eventSource = _eventSource ?? new WitheredDomainOcrEventSource(context);
			IHollowEventDispatcher eventDispatcher = _eventDispatcher ?? new WitheredDomainEventDispatcher(context, runRecord);
			using HollowRunner runner = new HollowRunner(context, eventSource, mapSource, mapNavigator, eventDispatcher, config, runRecord);
			string failedEventName = null;
			int failedEventRetryTimes = 0;
			string detectedEvent = default(string);
			HollowEventHandleResult eventFailure = default(HollowEventHandleResult);
			while (!cancellationToken.IsCancellationRequested)
			{
				await runner.CheckScreenOnceAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!string.IsNullOrWhiteSpace(runner.LastMapFailureReason) && !string.Equals(runner.LastMapFailureReason, "未识别到空洞地图", StringComparison.Ordinal) && !string.Equals(runner.LastMapFailureReason, "地图源未配置", StringComparison.Ordinal))
				{
					return new OperationResult(IsSuccess: false, runner.LastMapFailureReason);
				}
				HollowEventHandleResult battleFailure = runner.LastEventResult;
				if ((object)battleFailure != null && battleFailure.Outcome == HollowEventOutcomeKind.BattleStarted && !battleFailure.Success)
				{
					return new OperationResult(IsSuccess: false, battleFailure.Message ?? "枯萎之都战斗失败");
				}
				HollowEventDetection lastDetection = runner.LastDetection;
				int num;
				if ((object)lastDetection != null)
				{
					detectedEvent = lastDetection.EventName;
					if (detectedEvent != null)
					{
						eventFailure = runner.LastEventResult;
						if ((object)eventFailure != null && !eventFailure.Success)
						{
							num = (string.Equals(eventFailure.EventName, detectedEvent, StringComparison.Ordinal) ? 1 : 0);
							goto IL_02ad;
						}
					}
				}
				num = 0;
				goto IL_02ad;
				IL_02ad:
				if (num != 0)
				{
					failedEventRetryTimes = ((!string.Equals(failedEventName, detectedEvent, StringComparison.Ordinal)) ? 1 : (failedEventRetryTimes + 1));
					failedEventName = detectedEvent;
					if (failedEventRetryTimes > 60)
					{
						return new OperationResult(IsSuccess: false, eventFailure.Message ?? ("枯萎之都事件重试超限: " + detectedEvent));
					}
					await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				failedEventName = null;
				failedEventRetryTimes = 0;
				HollowEventHandleResult? lastEventResult = runner.LastEventResult;
				if ((object)lastEventResult != null && lastEventResult.Outcome == HollowEventOutcomeKind.MissionCompleted)
				{
					return runner.LastEventResult.Success ? new OperationResult(IsSuccess: true, "枯萎之都完成") : new OperationResult(IsSuccess: false, runner.LastEventResult.Message ?? "枯萎之都通关确认失败");
				}
				HollowEventHandleResult? lastEventResult2 = runner.LastEventResult;
				if ((object)lastEventResult2 != null && lastEventResult2.Outcome == HollowEventOutcomeKind.ExitedHollow)
				{
					return new OperationResult(IsSuccess: true, "离开空洞");
				}
				await Task.Delay(TimeSpan.FromMilliseconds(200L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				detectedEvent = null;
				eventFailure = null;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new OperationResult(IsSuccess: false, "枯萎之都已取消");
		}
		return new OperationResult(IsSuccess: false, "枯萎之都已取消");
	}
}
