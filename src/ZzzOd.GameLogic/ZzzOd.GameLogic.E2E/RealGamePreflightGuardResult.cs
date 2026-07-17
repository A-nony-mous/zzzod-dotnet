namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// Real-game preflight guard decision.
/// </summary>
public sealed record RealGamePreflightGuardResult(bool IsBlocked, string? Reason)
{
	/// <summary>
	/// Allows preflight to continue.
	/// </summary>
	public static RealGamePreflightGuardResult Allow()
	{
		return new RealGamePreflightGuardResult(IsBlocked: false, null);
	}

	/// <summary>
	/// Blocks preflight with a concrete reason.
	/// </summary>
	public static RealGamePreflightGuardResult Block(string reason)
	{
		return new RealGamePreflightGuardResult(IsBlocked: true, reason);
	}
}
