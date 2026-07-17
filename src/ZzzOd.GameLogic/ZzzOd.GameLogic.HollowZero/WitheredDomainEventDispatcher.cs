using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.HollowZero;

/// <summary>
/// 枯萎之都专用事件分发边界。
/// 尚未移植 BaselineParity 专用 OCR 操作的事件必须显式失败，不能复用通用分发器的待领奖成功状态。
/// </summary>
public sealed class WitheredDomainEventDispatcher : IHollowEventDispatcher
{
	private readonly ZContext _context;

	private readonly WitheredDomainEventDataService _eventData;

	private readonly WitheredDomainRunRecord? _runRecord;

	private bool _pendingCriticalStageBattle;

	/// <summary>
	/// 初始化分发器。
	/// </summary>
	public WitheredDomainEventDispatcher(ZContext context, WitheredDomainRunRecord? runRecord = null)
	{
		_context = context ?? throw new ArgumentNullException("context");
		_eventData = new WitheredDomainEventDataService(context.Environment);
		_runRecord = runRecord;
	}

	/// <inheritdoc />
	public async Task<HollowEventHandleResult> DispatchAsync(string eventName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (eventName == HollowZeroSpecialEvent.MissionComplete.EventName)
		{
			if (_runRecord == null)
			{
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.MissionCompleted, Success: false, "枯萎之都运行记录未就绪");
			}
			OperationResult result = await new WitheredDomainMissionCompleteOperation(_context, _runRecord).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.MissionCompleted, result.IsSuccess, result.Status);
		}
		HollowZeroEvent normalEvent = _eventData.GetNormalEventByName(eventName);
		if (normalEvent != null)
		{
			return await WitheredDomainEventOperations.HandleNormalEventAsync(_context, normalEvent, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.InBattle.EventName)
		{
			if (_runRecord == null)
			{
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Unhandled, Success: false, "枯萎之都运行记录未就绪");
			}
			try
			{
				OperationResult battle = await new WitheredDomainHollowBattle(_context, _runRecord).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (_pendingCriticalStageBattle && battle.IsSuccess && string.Equals(battle.Status, "普通战斗-完成", StringComparison.Ordinal))
				{
					_runRecord.AddTimes();
				}
				_pendingCriticalStageBattle = false;
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.BattleStarted, battle.IsSuccess, battle.Status);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				_pendingCriticalStageBattle = false;
				_context.AutoBattleContext.StopAutoBattle();
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.BattleStarted, Success: false, "枯萎之都战斗已取消");
			}
			catch (Exception ex2)
			{
				_pendingCriticalStageBattle = false;
				_context.AutoBattleContext.StopAutoBattle();
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Unhandled, Success: false, "枯萎之都战斗失败: " + ex2.Message);
			}
		}
		if (eventName == HollowZeroSpecialEvent.CallForSupport.EventName)
		{
			return await WitheredDomainEventOperations.HandleCallForSupportAsync(_context, _eventData, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.ResoniumChoose.EventName || eventName == HollowZeroSpecialEvent.ResoniumUpgrade.EventName || eventName == HollowZeroSpecialEvent.ResoniumDrop.EventName || eventName == HollowZeroSpecialEvent.ResoniumDrop2.EventName || eventName == HollowZeroSpecialEvent.ResoniumSwitch.EventName)
		{
			return await WitheredDomainEventOperations.HandleResoniumAsync(_context, _eventData, eventName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.ResoniumConfirm1.EventName || eventName == HollowZeroSpecialEvent.ResoniumConfirm2.EventName)
		{
			return await WitheredDomainEventOperations.HandleConfirmOrCorruptionAsync(_context, eventName, "底部-选择列表", new string[2] { "确认", "确定" }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.CorruptionRemove.EventName)
		{
			return await WitheredDomainEventOperations.HandleConfirmOrCorruptionAsync(_context, eventName, "底部-清除列表", new string[] { "清除" }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.SwiftSupplyLife.EventName || eventName == HollowZeroSpecialEvent.SwiftSupplyCoin.EventName || eventName == HollowZeroSpecialEvent.SwiftSupplyPress.EventName)
		{
			return await WitheredDomainEventOperations.HandleSwiftSupplyAsync(_context, eventName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.FullInBag.EventName)
		{
			return await WitheredDomainEventOperations.HandleFullInBagAsync(_context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.OldCapital.EventName)
		{
			return await WitheredDomainEventOperations.HandleOldCapitalAsync(_context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		int num;
		if (!(eventName == HollowZeroSpecialEvent.ResoniumStore5.EventName))
		{
			num = ((eventName == HollowZeroSpecialEvent.ResoniumStore0.EventName || eventName == HollowZeroSpecialEvent.ResoniumStore1.EventName || eventName == HollowZeroSpecialEvent.ResoniumStore2.EventName || eventName == HollowZeroSpecialEvent.ResoniumStore3.EventName || eventName == HollowZeroSpecialEvent.ResoniumStore4.EventName) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		if (num != 0)
		{
			return await WitheredDomainEventOperations.HandleBambooMerchantAsync(_context, _eventData, eventName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.DoorBattleEntry.EventName)
		{
			return await WitheredDomainEventOperations.ClickEventTextAsync(_context, eventName, HollowZeroSpecialEvent.DoorBattleEntry.EventName, 0.5, TimeSpan.FromSeconds(3L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if ((eventName == "门扉禁闭-善战" || eventName == "门扉禁闭-侵蚀") ? true : false)
		{
			return await WitheredDomainEventOperations.HandleDoorBattleAsync(_context, _eventData, eventName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == "不宜久留")
		{
			return await WitheredDomainEventOperations.HandleLeaveRandomZoneAsync(_context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (eventName == HollowZeroSpecialEvent.CriticalStageEntry.EventName || eventName == HollowZeroSpecialEvent.CriticalStageEntry2.EventName)
		{
			HollowEventHandleResult result2 = await WitheredDomainEventOperations.ClickEventTextAsync(_context, eventName, eventName, 0.5, TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			_pendingCriticalStageBattle = result2.Success;
			return result2;
		}
		if (eventName == HollowZeroSpecialEvent.NeedInteract.EventName)
		{
			ControllerBase controller = _context.Controller;
			if (controller is IZzzControllerActions controller2)
			{
				controller2.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
				return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true);
			}
			return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: false, "controller-not-ready");
		}
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Unhandled, Success: false, "枯萎之都事件操作未实现: " + eventName);
	}
}
