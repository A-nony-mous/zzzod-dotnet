using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicWait : AutoBattleAtomicOp
{
	public const string OP_NAME = "等待秒数";

	private readonly ManualResetEventSlim _stopEvent = new ManualResetEventSlim(initialState: false);

	public double WaitSeconds { get; }

	public AtomicWait(OperationDef operationDef)
		: base(null, "等待秒数 " + ResolveWaitSeconds(operationDef).ToString("F2", CultureInfo.InvariantCulture), operationDef)
	{
		WaitSeconds = ResolveWaitSeconds(operationDef);
	}

	public override void Execute()
	{
		_stopEvent.Reset();
		_stopEvent.Wait(TimeSpan.FromSeconds(Math.Max(0.0, WaitSeconds)));
	}

	public override void Stop()
	{
		_stopEvent.Set();
	}

	public override void Dispose()
	{
		_stopEvent.Dispose();
	}

	private static double ResolveWaitSeconds(OperationDef operationDef)
	{
		IReadOnlyList<string> data = operationDef.Data;
		if (data != null && data.Count > 0)
		{
			return AutoBattleAtomicOp.ParseDouble(operationDef.Data[0]) ?? operationDef.WaitSeconds;
		}
		return operationDef.WaitSeconds;
	}
}
