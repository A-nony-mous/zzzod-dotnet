using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class HollowEventDispatcher : IHollowEventDispatcher
{
	private static readonly HashSet<string> RewardEvents = new HashSet<string>
	{
		HollowZeroSpecialEvent.ResoniumChoose.EventName,
		HollowZeroSpecialEvent.ResoniumConfirm1.EventName,
		HollowZeroSpecialEvent.ResoniumConfirm2.EventName,
		HollowZeroSpecialEvent.ResoniumUpgrade.EventName,
		HollowZeroSpecialEvent.ResoniumDrop.EventName,
		HollowZeroSpecialEvent.ResoniumDrop2.EventName,
		HollowZeroSpecialEvent.ResoniumSwitch.EventName,
		HollowZeroSpecialEvent.SwiftSupplyLife.EventName,
		HollowZeroSpecialEvent.SwiftSupplyCoin.EventName,
		HollowZeroSpecialEvent.SwiftSupplyPress.EventName,
		HollowZeroSpecialEvent.CorruptionRemove.EventName,
		HollowZeroSpecialEvent.CallForSupport.EventName,
		HollowZeroSpecialEvent.ResoniumStore0.EventName,
		HollowZeroSpecialEvent.ResoniumStore1.EventName,
		HollowZeroSpecialEvent.ResoniumStore2.EventName,
		HollowZeroSpecialEvent.ResoniumStore3.EventName,
		HollowZeroSpecialEvent.ResoniumStore4.EventName,
		HollowZeroSpecialEvent.FullInBag.EventName,
		HollowZeroSpecialEvent.OldCapital.EventName
	};

	private readonly ZContext _context;

	public HollowEventDispatcher(ZContext context)
	{
		_context = context ?? throw new ArgumentNullException("context");
	}

	public Task<HollowEventHandleResult> DispatchAsync(string eventName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (eventName == HollowZeroSpecialEvent.MissionComplete.EventName)
		{
			return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.MissionCompleted, Success: true));
		}
		if (eventName == HollowZeroSpecialEvent.InBattle.EventName)
		{
			_context.AutoBattleContext.StartContextAsync();
			return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.BattleStarted, Success: true));
		}
		if (eventName == HollowZeroSpecialEvent.NeedInteract.EventName)
		{
			if (_context.Controller is IZzzControllerActions zzzControllerActions)
			{
				zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
				return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true));
			}
			return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: false, "controller-not-ready"));
		}
		if (RewardEvents.Contains(eventName))
		{
			return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.RewardPending, Success: true));
		}
		return Task.FromResult(new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Unhandled, Success: false, "unhandled-event"));
	}
}
