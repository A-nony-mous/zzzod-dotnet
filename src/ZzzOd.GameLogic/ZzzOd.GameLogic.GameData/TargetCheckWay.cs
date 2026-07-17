namespace ZzzOd.GameLogic.GameData;

public enum TargetCheckWay
{
	ContourCountInRange,
	ConnectedAreaWidthRatio,
	ConnectedAreaLengthRatio,
	TemplateMatchConfidence,
	OcrResultAsNumber,
	OcrTextContains,
	OcrTextSimilarity,
	ContourLengthAsRatio,
	MapContourLengthToPercent
}
