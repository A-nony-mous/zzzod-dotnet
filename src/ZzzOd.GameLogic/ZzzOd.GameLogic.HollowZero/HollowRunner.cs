using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;
using ZzzOd.GameLogic.Operations.HollowZero;

namespace ZzzOd.GameLogic.HollowZero;

public class HollowRunner : IDisposable
{
	public const string StatusMapSourceNotConfigured = "地图源未配置";

	public const string StatusMapNotDetected = "未识别到空洞地图";

	private readonly ZContext _ctx;

	private readonly IHollowEventSource _eventSource;

	private readonly IHollowMapSource _mapSource;

	private readonly IHollowMapNavigator _mapNavigator;

	private readonly IHollowEventDispatcher _eventDispatcher;

	private readonly WitheredDomainConfig? _witheredDomainConfig;

	private readonly WitheredDomainRunRecord? _witheredDomainRunRecord;

	private readonly WitheredDomainEventDataService _eventData;

	public const string StatusLeave = "离开空洞";

	private CancellationTokenSource? _cts;

	private int _periodicGeneration = 0;

	private readonly HashSet<string> _handledEvents = new HashSet<string>();

	private readonly HashSet<string> _ignoredEntryEvents = new HashSet<string>();

	private double _lastMoveTime;

	private static readonly HashSet<string> EntryEventNames = new HashSet<string>
	{
		HollowZeroSpecialEvent.ResoniumStore5.EventName,
		HollowZeroSpecialEvent.CriticalStageEntry.EventName,
		HollowZeroSpecialEvent.CriticalStageEntry2.EventName,
		HollowZeroSpecialEvent.DoorBattleEntry.EventName
	};

	public bool IsRunning { get; private set; }

	public IReadOnlyCollection<string> HandledEvents => _handledEvents;

	public HollowEventDetection? LastDetection { get; private set; }

	public HollowMapMoveResult? LastMoveResult { get; private set; }

	public HollowEventHandleResult? LastEventResult { get; private set; }

	public string? LastMapFailureReason { get; private set; }

	public bool UsesEmptyMapSource => _mapSource is EmptyHollowMapSource;

	public HollowRunner(ZContext ctx, IHollowEventSource? eventSource = null, IHollowMapSource? mapSource = null, IHollowMapNavigator? mapNavigator = null, IHollowEventDispatcher? eventDispatcher = null, WitheredDomainConfig? witheredDomainConfig = null, WitheredDomainRunRecord? witheredDomainRunRecord = null)
	{
		_ctx = ctx;
		_eventSource = eventSource ?? new YoloHollowEventSource(ctx);
		_mapSource = mapSource ?? new EmptyHollowMapSource();
		_mapNavigator = mapNavigator ?? new ControllerHollowMapNavigator(ctx);
		_eventDispatcher = eventDispatcher ?? new HollowEventDispatcher(ctx);
		_witheredDomainConfig = witheredDomainConfig;
		_witheredDomainRunRecord = witheredDomainRunRecord;
		_eventData = new WitheredDomainEventDataService(ctx.Environment);
	}

	public bool StartRunningAsync()
	{
		IsRunning = true;
		_cts = new CancellationTokenSource();
		_periodicGeneration++;
		int gen = _periodicGeneration;
		Task.Run(() => OperatePeriodicallyAsync(gen, _cts.Token));
		return true;
	}

	private async Task OperatePeriodicallyAsync(int generation, CancellationToken token)
	{
		while (IsRunning && _periodicGeneration == generation && !token.IsCancellationRequested)
		{
			try
			{
				await CheckScreenAsync(token);
				await Task.Delay(1000, token);
			}
			catch (TaskCanceledException)
			{
				break;
			}
			catch (Exception value)
			{
				Console.WriteLine($"[HollowRunner] Error in loop: {value}");
				await Task.Delay(1000, token);
			}
		}
	}

	public Task CheckScreenOnceAsync(CancellationToken token = default(CancellationToken))
	{
		return CheckScreenAsync(token);
	}

	private async Task CheckScreenAsync(CancellationToken token)
	{
		HollowEventDetection detection = await _eventSource.DetectAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		using Mat screen = detection?.Screen;
		LastDetection = (((object)detection == null) ? null : detection with
		{
			Screen = null
		});
		string eventName = detection?.EventName;
		if (string.IsNullOrWhiteSpace(eventName))
		{
			TryClickEventBlank();
			await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (eventName == HollowZeroSpecialEvent.HollowInside.EventName || eventName == HollowZeroSpecialEvent.ResoniumStore5.EventName || eventName == HollowZeroSpecialEvent.DoorBattleEntry.EventName || _ignoredEntryEvents.Contains(eventName))
		{
			await TryMoveByMapAsync(detection, screen, token);
		}
		else
		{
			await HandleEventAsync(eventName, token);
		}
	}

	private async Task TryMoveByMapAsync(HollowEventDetection? detection, Mat? screen, CancellationToken token)
	{
		HollowZeroMap currentMap = await _mapSource.DetectMapAsync(detection, token).ConfigureAwait(continueOnCapturedContext: false);
		if (currentMap == null)
		{
			LastMoveResult = null;
			LastMapFailureReason = (UsesEmptyMapSource ? "地图源未配置" : "未识别到空洞地图");
			TryClickEventBlank();
			await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		LastMapFailureReason = null;
		bool handleEntryEvent = false;
		if (_mapNavigator is WitheredDomainMapNavigator)
		{
			HollowZeroMapNode nextNode = _ctx.WitheredDomain.GetNextToMove(currentMap)?.NextNodeToMove;
			if (nextNode == null || nextNode.PathStepCnt == 999)
			{
				if (nextNode == null)
				{
					LastMoveResult = null;
					await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
					return;
				}
				double runTime = detection?.RunTime ?? 0.0;
				if (runTime - _lastMoveTime < 1.0)
				{
					await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
					return;
				}
			}
			handleEntryEvent = IsWitheredDomainEntry(nextNode.Entry.EntryName) && !_ctx.WitheredDomain.HadBeenEntry(nextNode.Entry.EntryName);
			_lastMoveTime = detection?.RunTime ?? _lastMoveTime;
			CheckInfoBeforeMove(screen, currentMap);
			if (await TryExitWitheredDomainForExtraTaskAsync(currentMap, nextNode, token).ConfigureAwait(continueOnCapturedContext: false))
			{
				return;
			}
		}
		LastMoveResult = _mapNavigator.MoveNext(currentMap, screen);
		HollowMapMoveResult moved = LastMoveResult;
		if (!(moved?.Clicked ?? false))
		{
			return;
		}
		_handledEvents.Clear();
		_ignoredEntryEvents.Clear();
		int pathNodeCount = Math.Max(1, moved.NextNode.PathNodeCnt);
		await Task.Delay(TimeSpan.FromMilliseconds((double)pathNodeCount * 250.0 + 1000.0), token).ConfigureAwait(continueOnCapturedContext: false);
		if (!handleEntryEvent)
		{
			return;
		}
		await HandleWitheredDomainEntryEventAsync(moved.NextNode.Entry.EntryName, token).ConfigureAwait(continueOnCapturedContext: false);
		if (LastEventResult?.Success ?? false)
		{
			foreach (string eventName in GetHandledEventsAfterEntry(moved.NextNode.Entry.EntryName))
			{
				_handledEvents.Add(eventName);
				_ignoredEntryEvents.Add(eventName);
			}
		}
		else
		{
			await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task<bool> TryExitWitheredDomainForExtraTaskAsync(HollowZeroMap currentMap, HollowZeroMapNode nextNode, CancellationToken token)
	{
		if (_witheredDomainConfig == null || _witheredDomainRunRecord == null)
		{
			return false;
		}
		if (!WitheredDomainExtraTaskEvaluator.ShouldLeave(_witheredDomainConfig, _witheredDomainRunRecord, _ctx.WitheredDomain, currentMap))
		{
			return false;
		}
		if (nextNode.PathStepCnt == 999)
		{
			return false;
		}
		OperationResult exitResult = await new HollowExitByMenu(_ctx).ExecuteAsync(token).ConfigureAwait(continueOnCapturedContext: false);
		LastEventResult = new HollowEventHandleResult("离开空洞", HollowEventOutcomeKind.ExitedHollow, exitResult.IsSuccess, exitResult.Status);
		LastMoveResult = null;
		if (!exitResult.IsSuccess)
		{
			LastMapFailureReason = exitResult.Status;
			return true;
		}
		_witheredDomainRunRecord.AddDailyTimes();
		StopRunning();
		return true;
	}

	private async Task HandleWitheredDomainEntryEventAsync(string entryName, CancellationToken token)
	{
		if (_eventDispatcher is WitheredDomainEventDispatcher)
		{
			if (1 == 0)
			{
			}
			string text;
			switch (entryName)
			{
			case "邦布商人":
				text = HollowZeroSpecialEvent.ResoniumStore5.EventName;
				break;
			case "守门人":
				text = HollowZeroSpecialEvent.CriticalStageEntry.EventName;
				break;
			case "门扉禁闭-善战":
			case "门扉禁闭-侵蚀":
			case "不宜久留":
				text = entryName;
				break;
			default:
				text = null;
				break;
			}
			if (1 == 0)
			{
			}
			string eventName = text;
			if (eventName != null)
			{
				LastEventResult = await _eventDispatcher.DispatchAsync(eventName, token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private async Task HandleEventAsync(string eventName, CancellationToken token)
	{
		_handledEvents.Add(eventName);
		if (IsEntryEvent(eventName))
		{
			_ignoredEntryEvents.Add(eventName);
		}
		LastEventResult = await _eventDispatcher.DispatchAsync(eventName, token).ConfigureAwait(continueOnCapturedContext: false);
		if (LastEventResult.Outcome == HollowEventOutcomeKind.MissionCompleted)
		{
			StopRunning();
		}
		else if (!LastEventResult.Success)
		{
			await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private bool IsEntryEvent(string eventName)
	{
		return EntryEventNames.Contains(eventName) || (_eventData.GetNormalEventByName(eventName)?.IsEntryOpt ?? false);
	}

	private static bool IsWitheredDomainEntry(string entryName)
	{
		switch (entryName)
		{
		case "邦布商人":
		case "守门人":
		case "门扉禁闭-善战":
		case "门扉禁闭-侵蚀":
		case "不宜久留":
			return true;
		default:
			return false;
		}
	}

	private static IReadOnlyList<string> GetHandledEventsAfterEntry(string entryName)
	{
		if (1 == 0)
		{
		}
		IReadOnlyList<string> result = ((!(entryName == "邦布商人")) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[] { HollowZeroSpecialEvent.ResoniumStore5.EventName }));
		if (1 == 0)
		{
		}
		return result;
	}

	private bool TryClickEventBlank()
	{
		OneDragon.Core.Screen.ScreenArea area = _ctx.ScreenContext.GetArea("零号空洞-事件", "空白");
		return area != null && (_ctx.Controller?.Click(area.Center) ?? false);
	}

	private void CheckInfoBeforeMove(Mat? screen, HollowZeroMap currentMap)
	{
		if (screen == null)
		{
			return;
		}
		_ctx.WitheredDomain.CheckAgentList(screen, skipIfChecked: true);
		HollowLevelInfo levelInfo = _ctx.WitheredDomain.LevelInfo;
		if (levelInfo.Level == -1)
		{
			OneDragon.Core.Screen.ScreenArea area = _ctx.ScreenContext.GetArea("零号空洞-事件", "当前层数");
			if (area != null)
			{
				using Mat image = CvImageUtils.Crop(screen, area.Rect);
				levelInfo.Level = StringUtils.GetPositiveDigits(_ctx.OcrService.Matcher.RunOcrSingleLine(image), -1) ?? (-1);
			}
		}
		if (levelInfo.Phase == -1)
		{
			HollowLevelInfo hollowLevelInfo = levelInfo;
			int level = levelInfo.Level;
			bool flag = (uint)(level - 2) <= 1u;
			hollowLevelInfo.Phase = ((!flag) ? 1 : (currentMap.ContainsEntry("传送点") ? 1 : 2));
		}
		bool flag2 = levelInfo.MissionTypeName == null;
		bool flag3 = flag2;
		if (flag3)
		{
			int level = levelInfo.Level;
			bool flag = (uint)(level - 2) <= 1u;
			flag3 = flag;
		}
		if (flag3 && levelInfo.Phase == 1 && currentMap.ContainsEntry("假面研究者"))
		{
			levelInfo.MissionTypeName = "旧都列车";
		}
		if (levelInfo.MissionTypeName == null && (currentMap.ContainsEntry("投机客") || currentMap.ContainsEntry("门扉禁闭-财富")))
		{
			levelInfo.MissionTypeName = "施工废墟";
		}
	}

	public void StopRunning()
	{
		IsRunning = false;
		if (_cts != null && !_cts.IsCancellationRequested)
		{
			_cts.Cancel();
		}
	}

	public void Dispose()
	{
		StopRunning();
		_cts?.Dispose();
	}
}
