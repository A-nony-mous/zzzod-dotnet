using System;
using System.Collections.Generic;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicClearState : AutoBattleAtomicOp, IStateClearOp
{
	public const string OP_NAME = "清除状态";

	public string? StateName { get; }

	public IReadOnlyList<string>? StateNameList { get; }

	public AtomicClearState(AutoBattleContext? context, OperationDef operationDef)
		: base(context, "清除状态", operationDef)
	{
		IReadOnlyList<string> data = operationDef.Data;
		StateName = ((data != null && data.Count > 0) ? operationDef.Data[0] : operationDef.StateName);
		StateNameList = operationDef.StateNameList;
	}

	public override void Execute()
	{
		base.Context.CustomContext.ClearState(ResolveStateNames());
	}

	private IReadOnlyList<string> ResolveStateNames()
	{
		if (StateNameList != null)
		{
			return StateNameList;
		}
		if (!string.IsNullOrWhiteSpace(StateName))
		{
			return new string[] { StateName };
		}
		throw new InvalidOperationException("清除状态缺少 state 或 state_list。");
	}
}
