using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// Resolves the current real-game session into a runnable preflight state.
/// </summary>
public static class RealGamePreflightRunner
{
	/// <summary>
	/// Resolve the real-game session before running a selected application.
	/// </summary>
	public static async Task<OperationResult> ResolveSessionAsync(bool windowExists, bool windowReady, string? initializationFailure, Func<Task<OperationResult>> openAndEnterGameAsync, Func<Task<OperationResult>> waitNormalWorldAsync, Func<Task<OperationResult>> backToNormalWorldAsync, Func<Task<OperationResult>> enterGameAsync, ICollection<string>? recognitionSummary = null)
	{
		ArgumentNullException.ThrowIfNull(openAndEnterGameAsync, "openAndEnterGameAsync");
		ArgumentNullException.ThrowIfNull(waitNormalWorldAsync, "waitNormalWorldAsync");
		ArgumentNullException.ThrowIfNull(backToNormalWorldAsync, "backToNormalWorldAsync");
		ArgumentNullException.ThrowIfNull(enterGameAsync, "enterGameAsync");
		if (!windowExists)
		{
			recognitionSummary?.Add("Executing OpenAndEnterGame because no usable window was found.");
			return await openAndEnterGameAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		if (!windowReady)
		{
			string failure = initializationFailure ?? "Initial game window exists but is not usable.";
			recognitionSummary?.Add(failure);
			return new OperationResult(IsSuccess: false, failure);
		}
		return await ResolveExistingWindowAsync(waitNormalWorldAsync, backToNormalWorldAsync, enterGameAsync, recognitionSummary).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Resolve an existing usable game window before running selected applications.
	/// </summary>
	public static async Task<OperationResult> ResolveExistingWindowAsync(Func<Task<OperationResult>> waitNormalWorldAsync, Func<Task<OperationResult>> backToNormalWorldAsync, Func<Task<OperationResult>> enterGameAsync, ICollection<string>? recognitionSummary = null)
	{
		ArgumentNullException.ThrowIfNull(waitNormalWorldAsync, "waitNormalWorldAsync");
		ArgumentNullException.ThrowIfNull(backToNormalWorldAsync, "backToNormalWorldAsync");
		ArgumentNullException.ThrowIfNull(enterGameAsync, "enterGameAsync");
		OperationResult world = await waitNormalWorldAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (world.IsSuccess)
		{
			recognitionSummary?.Add("Current window is already in a runnable world state.");
			return world;
		}
		recognitionSummary?.Add("Trying BackToNormalWorld because current screen was not a confirmed world state: " + world.Status);
		OperationResult backToWorld = await backToNormalWorldAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (backToWorld.IsSuccess)
		{
			recognitionSummary?.Add("BackToNormalWorld reached a runnable state: " + backToWorld.Status);
			return backToWorld;
		}
		recognitionSummary?.Add("Executing EnterGame because BackToNormalWorld did not reach a runnable state: " + backToWorld.Status);
		return await enterGameAsync().ConfigureAwait(continueOnCapturedContext: false);
	}
}
