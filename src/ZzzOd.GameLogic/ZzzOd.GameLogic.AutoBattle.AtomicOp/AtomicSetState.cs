using System;
using System.Collections.Generic;
using System.Globalization;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicSetState : AutoBattleAtomicOp
{
	public const string OP_NAME = "设置状态";

	public string? StateName { get; }

	public IReadOnlyList<string>? StateNameList { get; }

	public double DiffTime { get; }

	public double DiffTimeAdd { get; }

	public int? Value { get; }

	public int? ValueAdd { get; }

	public AtomicSetState(AutoBattleContext? context, OperationDef operationDef)
		: base(context, "设置状态 " + ResolveStateName(operationDef), operationDef)
	{
		StateName = ResolveStateName(operationDef);
		StateNameList = operationDef.StateNameList;
		DiffTime = ResolveDiffTime(operationDef);
		DiffTimeAdd = operationDef.StateSecondsAdd;
		Value = ResolveValue(operationDef);
		ValueAdd = operationDef.StateValueAdd;
	}

	public override void Execute()
	{
		base.Context.CustomContext.SetState(ResolveStateNames(), DiffTime, DiffTimeAdd, Value, ValueAdd);
	}

	private static string? ResolveStateName(OperationDef operationDef)
	{
		IReadOnlyList<string> data = operationDef.Data;
		return (data != null && data.Count > 0) ? operationDef.Data[0] : operationDef.StateName;
	}

	private static double ResolveDiffTime(OperationDef operationDef)
	{
		IReadOnlyList<string> data = operationDef.Data;
		return (data == null || data.Count <= 1) ? operationDef.StateSeconds : (AutoBattleAtomicOp.ParseDouble(operationDef.Data[1]) ?? operationDef.StateSeconds);
	}

	private static int? ResolveValue(OperationDef operationDef)
	{
		IReadOnlyList<string> data = operationDef.Data;
		if (data != null && data.Count > 2 && int.TryParse(operationDef.Data[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return operationDef.StateValue;
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
		throw new InvalidOperationException("设置状态缺少 state 或 state_list。");
	}
}
