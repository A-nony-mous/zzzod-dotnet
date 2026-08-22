using System;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地交互处理结果。
/// </summary>
/// <remarks>
/// <c>Delay</c> 是固定延时，<c>DelayUntilRoundTime</c> 是本轮总时长的补足目标。
/// </remarks>
public sealed record LostVoidInteractResult(LostVoidInteractResultKind Kind, string? Status = null, object? Data = null, LostVoidInteractTarget? Target = null, string? HadBeenType = null, TimeSpan? Delay = null, TimeSpan? DelayUntilRoundTime = null)
{
	public static LostVoidInteractResult Retry(string status)
	{
			// 重试时把本轮总时长补足到 1 秒，不追加固定延时。
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
