namespace ZzzOd.GameLogic.Application.ChargePlan;

internal sealed record ChargePlanDoubleRewardOcrResult(ChargePlanDoubleRewardOcrResultKind Kind, int TimesLeft, string Status)
{
	public static ChargePlanDoubleRewardOcrResult Activity(int timesLeft)
	{
		return new ChargePlanDoubleRewardOcrResult(ChargePlanDoubleRewardOcrResultKind.Activity, timesLeft, string.Empty);
	}

	public static ChargePlanDoubleRewardOcrResult NoActivity()
	{
		return new ChargePlanDoubleRewardOcrResult(ChargePlanDoubleRewardOcrResultKind.NoActivity, 0, "无双倍活动");
	}

	public static ChargePlanDoubleRewardOcrResult Retry(string status)
	{
		return new ChargePlanDoubleRewardOcrResult(ChargePlanDoubleRewardOcrResultKind.Retry, 0, status);
	}
}
