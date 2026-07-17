using System;
using System.Threading;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicBtnCommon : AutoBattleAtomicOp
{
	private readonly object _statusLock = new object();

	private bool _running;

	private bool _stopRequested;

	public string BtnName { get; }

	public BtnWayEnum BtnWay { get; }

	public bool IsPress { get; }

	public bool IsRelease { get; }

	public double? PressTimeSeconds { get; }

	public TimeSpan? PressTime { get; }

	public int RepeatTimes { get; }

	public AtomicBtnCommon(AutoBattleContext? context, OperationDef operationDef)
		: base(context, operationDef.OpName ?? string.Empty, operationDef, BtnWayEnumExtensions.FromValue(operationDef.BtnWay) == BtnWayEnum.Press && !operationDef.BtnPress.HasValue)
	{
		string text = operationDef.OpName ?? string.Empty;
		int num = text.IndexOf('-');
		BtnName = ((num >= 0 && num < text.Length - 1) ? text.Substring(num + 1) : text);
		if (!ZzzAtomicButtonActions.IsKnownAction(text))
		{
			throw new ArgumentException("非法按键 " + BtnName, "operationDef");
		}
		BtnWay = BtnWayEnumExtensions.FromValue(operationDef.BtnWay);
		IsPress = BtnWay == BtnWayEnum.Press;
		IsRelease = BtnWay == BtnWayEnum.Release;
		PressTimeSeconds = operationDef.BtnPress;
		PressTime = AutoBattleAtomicOp.ToTimeSpan(operationDef.BtnPress);
		RepeatTimes = operationDef.BtnRepeatTimes;
	}

	public override void Execute()
	{
		lock (_statusLock)
		{
			if (_running)
			{
				return;
			}
			_running = true;
			_stopRequested = false;
		}
		try
		{
			for (int i = 0; i < RepeatTimes; i++)
			{
				if (IsStopRequested())
				{
					break;
				}
				SleepDelay(base.PreDelay);
				if (IsStopRequested())
				{
					break;
				}
				base.Context.ExecuteButtonAction(base.OperationDef.OpName, IsPress, PressTime, IsRelease);
				if (IsStopRequested())
				{
					break;
				}
				SleepDelay(base.PostDelay);
			}
		}
		finally
		{
			lock (_statusLock)
			{
				_running = false;
				_stopRequested = false;
			}
		}
	}

	public override void Stop()
	{
		lock (_statusLock)
		{
			if (_running)
			{
				_stopRequested = true;
			}
		}
		if (IsPress)
		{
			base.Context.ExecuteButtonAction(base.OperationDef.OpName, press: false, null, release: true);
		}
	}

	private bool IsStopRequested()
	{
		lock (_statusLock)
		{
			return _stopRequested;
		}
	}

	private static void SleepDelay(double seconds)
	{
		if (seconds > 0.0)
		{
			Thread.Sleep(TimeSpan.FromSeconds(seconds));
		}
	}
}
