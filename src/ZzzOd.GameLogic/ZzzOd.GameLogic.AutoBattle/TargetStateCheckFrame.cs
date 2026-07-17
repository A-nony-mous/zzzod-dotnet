using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class TargetStateCheckFrame
{
	public bool IsSuccess { get; init; } = true;

	public bool IsTimedOut { get; init; }

	public IReadOnlyList<Point[]> Contours { get; init; } = Array.Empty<Point[]>();

	public Mat? MaskImage { get; init; }

	public string OcrText { get; init; } = string.Empty;
}
