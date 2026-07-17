namespace ZzzOd.GameLogic.AutoBattle;

public sealed record TargetStateCheckResult(string StateName, bool IsHit, int? Value = null, bool IsClear = false)
{
	public static TargetStateCheckResult Hit(string stateName)
	{
		return new TargetStateCheckResult(stateName, IsHit: true);
	}

	public static TargetStateCheckResult HitValue(string stateName, int value)
	{
		return new TargetStateCheckResult(stateName, IsHit: true, value);
	}

	public static TargetStateCheckResult Clear(string stateName)
	{
		return new TargetStateCheckResult(stateName, IsHit: false, null, IsClear: true);
	}

	public static TargetStateCheckResult Miss(string stateName)
	{
		return new TargetStateCheckResult(stateName, IsHit: false);
	}
}
