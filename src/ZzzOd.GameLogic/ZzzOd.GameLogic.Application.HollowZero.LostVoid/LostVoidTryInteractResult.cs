using System;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed record LostVoidTryInteractResult(LostVoidTryInteractKind Kind, string? Status = null, LostVoidInteractTarget? Target = null, bool InteractAttempted = false, TimeSpan? Delay = null)
{
	public static LostVoidTryInteractResult Retry(string status, TimeSpan? delay = null)
	{
		return new LostVoidTryInteractResult(LostVoidTryInteractKind.Retry, status, null, InteractAttempted: false, delay);
	}

	public static LostVoidTryInteractResult Wait(string status, LostVoidInteractTarget? target = null)
	{
		return new LostVoidTryInteractResult(LostVoidTryInteractKind.Wait, status, target, InteractAttempted: true, TimeSpan.FromMilliseconds(500L));
	}

	public static LostVoidTryInteractResult Success(string status)
	{
		return new LostVoidTryInteractResult(LostVoidTryInteractKind.Success, status);
	}

	public static LostVoidTryInteractResult Fail(string status)
	{
		return new LostVoidTryInteractResult(LostVoidTryInteractKind.Fail, status);
	}
}
