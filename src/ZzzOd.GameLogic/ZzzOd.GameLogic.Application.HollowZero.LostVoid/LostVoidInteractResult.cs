using System;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地交互处理结果。
/// </summary>
/// <remarks>
/// <c>Delay</c> 是固定延时（对应参考实现 <c>wait</c>），<c>DelayUntilRoundTime</c> 是补足制目标（对应 <c>wait_round_time</c>）。
/// </remarks>
public sealed record LostVoidInteractResult(LostVoidInteractResultKind Kind, string? Status = null, object? Data = null, LostVoidInteractTarget? Target = null, string? HadBeenType = null, TimeSpan? Delay = null, TimeSpan? DelayUntilRoundTime = null)
{
	public static LostVoidInteractResult Retry(string status)
	{
		// 对应 lost_void_run_level.py:655 的 wait_round_time=1（补足制，非固定延时）。
		return new LostVoidInteractResult(LostVoidInteractResultKind.Retry, status, null, null, null, null, TimeSpan.FromSeconds(1L));
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
