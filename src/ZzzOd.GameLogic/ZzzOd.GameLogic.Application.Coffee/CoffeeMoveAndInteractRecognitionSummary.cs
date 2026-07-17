using System;
using System.Collections.Generic;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.Coffee;

internal sealed class CoffeeMoveAndInteractRecognitionSummary
{
	public string? ActiveScreenName { get; set; }

	public MiniMapAngleResult? MiniMapAngle { get; set; }

	public string PointOrderResult { get; set; } = string.Empty;

	public bool PointOrderVisible { get; set; }

	public IReadOnlyList<string> OcrTexts { get; set; } = Array.Empty<string>();

	public string? FailureReason { get; set; }
}
