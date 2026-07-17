using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.RandomPlay;

internal sealed class RandomPlayTurnActionTargetSummary
{
	public double TargetAngle { get; set; }

	public double AngleThreshold { get; set; }

	public MiniMapAngleResult BeforeMiniMap { get; set; } = new MiniMapAngleResult(PlayMaskFound: false, null);

	public MiniMapAngleResult? AfterMiniMap { get; set; }

	public double? AngleDiff { get; set; }

	public double? EffectiveAngleDiff { get; set; }

	public double ScaleBefore { get; set; }

	public double ScaleAfter { get; set; }

	public double TurnDx { get; set; }
}
