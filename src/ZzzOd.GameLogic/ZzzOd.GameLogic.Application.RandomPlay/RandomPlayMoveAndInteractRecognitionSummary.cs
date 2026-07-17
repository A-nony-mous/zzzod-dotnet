using System;
using System.Collections.Generic;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.RandomPlay;

internal sealed class RandomPlayMoveAndInteractRecognitionSummary
{
	public string? ActiveScreenName { get; set; }

	public MiniMapAngleResult? MiniMapAngle { get; set; }

	public string BusinessStatusResult { get; set; } = string.Empty;

	public bool BusinessStatusVisible { get; set; }

	public string YesterdayLedgerResult { get; set; } = string.Empty;

	public bool YesterdayLedgerVisible { get; set; }

	public string RightOptionsResult { get; set; } = string.Empty;

	public bool RightOptionsVisible { get; set; }

	public IReadOnlyList<string> OcrTexts { get; set; } = Array.Empty<string>();

	public string? FailureReason { get; set; }
}
