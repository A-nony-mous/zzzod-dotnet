using System;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public abstract class AutoBattleButtonAtomicOp : AutoBattleAtomicOp
{
	public string ActionName { get; }

	public bool Press { get; }

	public bool Release { get; }

	public double? PressTimeSeconds { get; }

	public TimeSpan? PressTime { get; }

	protected AutoBattleButtonAtomicOp(AutoBattleContext? context, string actionName, OperationDef operationDef, bool press, double? pressTimeSeconds, bool release)
		: base(context, CreateOpName(actionName, press, release), operationDef, press && !pressTimeSeconds.HasValue)
	{
		ActionName = actionName;
		Press = press;
		Release = release;
		PressTimeSeconds = pressTimeSeconds;
		PressTime = AutoBattleAtomicOp.ToTimeSpan(pressTimeSeconds);
	}

	private static string CreateOpName(string actionName, bool press, bool release)
	{
		if (press)
		{
			return actionName + "按下";
		}
		if (release)
		{
			return actionName + "松开";
		}
		return actionName;
	}

	public override void Execute()
	{
		base.Context.ExecuteButtonAction(ActionName, Press, PressTime, Release);
	}

	public override void Stop()
	{
		if (Press)
		{
			base.Context.ExecuteButtonAction(ActionName, press: false, null, release: true);
		}
	}
}
