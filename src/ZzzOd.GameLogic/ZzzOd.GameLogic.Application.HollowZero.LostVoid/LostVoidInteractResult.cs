using System;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed record LostVoidInteractResult(LostVoidInteractResultKind Kind, string? Status = null, object? Data = null, LostVoidInteractTarget? Target = null, string? HadBeenType = null, TimeSpan? Delay = null)
{
	public static LostVoidInteractResult Retry(string status)
	{
		return new LostVoidInteractResult(LostVoidInteractResultKind.Retry, status, null, null, null, TimeSpan.FromSeconds(1L));
	}

	public static LostVoidInteractResult Wait(string status, string? hadBeenType = null, LostVoidInteractTarget? target = null)
	{
		return new LostVoidInteractResult(LostVoidInteractResultKind.Wait, status, null, target, hadBeenType, TimeSpan.FromSeconds(2L));
	}

	public static LostVoidInteractResult Success(string status, object? data = null)
	{
		return new LostVoidInteractResult(LostVoidInteractResultKind.Success, status, data);
	}

	public static LostVoidInteractResult Fail(string status)
	{
		return new LostVoidInteractResult(LostVoidInteractResultKind.Fail, status);
	}
}
