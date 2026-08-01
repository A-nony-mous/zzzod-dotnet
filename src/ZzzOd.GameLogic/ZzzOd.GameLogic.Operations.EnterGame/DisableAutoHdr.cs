using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Disables Auto HDR before launching the game.
/// </summary>
public sealed class DisableAutoHdr : ZOperation
{
	private readonly IAutoHdrPreferenceStore _store;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public DisableAutoHdr(ZContext context, IAutoHdrPreferenceStore? store = null, TimeSpan? successDelay = null)
		: base(context, "禁用HDR", needCheckGameWindow: false)
	{
		_store = store ?? new WindowsAutoHdrPreferenceStore();
		_successDelay = successDelay ?? TimeSpan.FromMilliseconds(500L);
	}

	[OperationNode("禁用自动HDR", IsStartNode = true, ScreenshotBeforeRound = false)]
	private OperationRoundResult Disable()
	{
		AutoHdrChangeResult autoHdrChangeResult = AutoHdrManager.Disable(base.ZContext.GameAccountConfig.GamePath, _store);
		if (!autoHdrChangeResult.IsSuccess)
		{
			return RoundFail(autoHdrChangeResult.Status);
		}
		base.ZContext.GameConfig.OriginalHdrValue = autoHdrChangeResult.OriginalValue;
		return RoundSuccess(autoHdrChangeResult.Status, null, _successDelay);
	}
}
