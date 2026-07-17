using OneDragon.Core.Yolo;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class ZzzFlashClassifierAdapter : IAutoBattleFlashClassifier
{
	private readonly ZContext _ctx;

	public ZzzFlashClassifierAdapter(ZContext ctx)
	{
		_ctx = ctx;
	}

	public AutoBattleFlashClassification Classify(object? screen)
	{
		if (!(screen is Mat image))
		{
			return new AutoBattleFlashClassification(-1, 0.0, 0.0, 0.0, 0.0, 0.0);
		}
		YoloClassificationRunDiagnostics yoloClassificationRunDiagnostics = _ctx.FlashClassifier.CoreClassifier.RunWithDiagnostics(image);
		return new AutoBattleFlashClassification(yoloClassificationRunDiagnostics.Result.ClassIndex, 0.0, yoloClassificationRunDiagnostics.PreprocessElapsedMilliseconds, yoloClassificationRunDiagnostics.InferenceElapsedMilliseconds, yoloClassificationRunDiagnostics.PostprocessElapsedMilliseconds, yoloClassificationRunDiagnostics.TotalElapsedMilliseconds);
	}

	public bool InitModel()
	{
		return _ctx.FlashClassifier.InitModel();
	}
}
