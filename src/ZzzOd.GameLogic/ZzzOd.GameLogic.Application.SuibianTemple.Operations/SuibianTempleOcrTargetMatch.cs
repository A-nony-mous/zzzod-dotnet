using OneDragon.Core.Ocr;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

internal sealed class SuibianTempleOcrTargetMatch
{
	public string? TargetText { get; set; }

	public string Text { get; set; } = string.Empty;

	public double Confidence { get; set; }

	public int X { get; set; }

	public int Y { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }

	public int CenterX { get; set; }

	public int CenterY { get; set; }

	public bool Ignored { get; set; }

	public static SuibianTempleOcrTargetMatch From(string? targetText, OcrMatchResult result, bool ignored)
	{
		return new SuibianTempleOcrTargetMatch
		{
			TargetText = targetText,
			Text = result.Text,
			Confidence = result.Confidence,
			X = result.X,
			Y = result.Y,
			Width = result.Width,
			Height = result.Height,
			CenterX = result.Center.X,
			CenterY = result.Center.Y,
			Ignored = ignored
		};
	}
}
