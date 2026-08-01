using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Restores Auto HDR after the game window is available.
/// </summary>
public sealed class EnableAutoHdr : ZOperation
{
	private readonly IAutoHdrPreferenceStore _store;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the operation.
	/// </summary>
	public EnableAutoHdr(ZContext context, IAutoHdrPreferenceStore? store = null, TimeSpan? successDelay = null)
		: base(context, "恢复HDR", needCheckGameWindow: false)
	{
		_store = store ?? new WindowsAutoHdrPreferenceStore();
		_successDelay = successDelay ?? TimeSpan.FromMilliseconds(500L);
	}

	[OperationNode("启用自动HDR", IsStartNode = true, ScreenshotBeforeRound = false)]
	private OperationRoundResult Enable()
	{
		AutoHdrChangeResult autoHdrChangeResult = AutoHdrManager.Enable(base.ZContext.GameAccountConfig.GamePath, base.ZContext.GameConfig.OriginalHdrValue, _store);
		return autoHdrChangeResult.IsSuccess ? RoundSuccess(autoHdrChangeResult.Status, null, _successDelay) : RoundFail(autoHdrChangeResult.Status);
	}
}
