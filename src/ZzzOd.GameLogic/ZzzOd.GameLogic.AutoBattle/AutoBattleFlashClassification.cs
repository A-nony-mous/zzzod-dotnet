namespace ZzzOd.GameLogic.AutoBattle;

public sealed record AutoBattleFlashClassification(int ClassIndex, double Confidence, double ColorConversionElapsedMilliseconds, double PreprocessElapsedMilliseconds, double InferenceElapsedMilliseconds, double PostprocessElapsedMilliseconds, double TotalElapsedMilliseconds);
