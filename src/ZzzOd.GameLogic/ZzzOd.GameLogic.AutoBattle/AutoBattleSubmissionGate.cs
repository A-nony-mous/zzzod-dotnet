using System.Threading;

namespace ZzzOd.GameLogic.AutoBattle;

internal sealed class AutoBattleSubmissionGate
{
	private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

	private long _dropped;

	public bool TryEnter()
	{
		if (_gate.Wait(0))
		{
			return true;
		}
		Interlocked.Increment(ref _dropped);
		return false;
	}

	public void Exit()
	{
		_gate.Release();
	}

	public long ConsumeDropped()
	{
		return Interlocked.Exchange(ref _dropped, 0L);
	}

	public void ResetDropped()
	{
		Interlocked.Exchange(ref _dropped, 0L);
	}
}
