using System;
using System.Threading;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnQuickAssist(AutoBattleContext? context, OperationDef operationDef) : AutoBattleAtomicOp(context, "按键-快速支援", operationDef)
{
	public const string OP_NAME = "按键-快速支援";

	private readonly object _statusLock = new object();

	private AtomicBtnSpecialStatus _status = AtomicBtnSpecialStatus.Wait;

	public override void Execute()
	{
		lock (_statusLock)
		{
			if (_status == AtomicBtnSpecialStatus.Stop)
			{
				_status = AtomicBtnSpecialStatus.Wait;
				return;
			}
			if (_status != AtomicBtnSpecialStatus.Wait)
			{
				return;
			}
			_status = AtomicBtnSpecialStatus.Running;
		}
		try
		{
			if (!WaitWhileRunning(base.PreDelay))
			{
				return;
			}
			base.Context.QuickAssist();
			WaitWhileRunning(base.PostDelay);
		}
		finally
		{
			lock (_statusLock)
			{
				_status = AtomicBtnSpecialStatus.Wait;
			}
		}
	}

	public override void Stop()
	{
		lock (_statusLock)
		{
			_status = AtomicBtnSpecialStatus.Stop;
		}
	}

	private bool IsRunning()
	{
		lock (_statusLock)
		{
			return _status == AtomicBtnSpecialStatus.Running;
		}
	}

	private bool WaitWhileRunning(double delaySeconds)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(delaySeconds);
		while (IsRunning() && DateTime.UtcNow < deadline)
		{
			TimeSpan remaining = deadline - DateTime.UtcNow;
			Thread.Sleep(remaining > TimeSpan.FromMilliseconds(10.0) ? 10 : Math.Max(1, (int)remaining.TotalMilliseconds));
		}
		return IsRunning();
	}
}
