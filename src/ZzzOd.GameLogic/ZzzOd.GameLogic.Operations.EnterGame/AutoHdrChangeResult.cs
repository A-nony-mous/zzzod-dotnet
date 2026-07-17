using System;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Auto HDR change outcome.
/// </summary>
public sealed record AutoHdrChangeResult(bool IsSuccess, string Status, string? OriginalValue = null, Exception? Error = null);
