namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划双倍奖励检查结果类型。
/// </summary>
public enum ChargePlanDoubleRewardResultKind
{
	/// <summary>
	/// 检查成功。
	/// </summary>
	Success,
	/// <summary>
	/// 当前截图或 OCR 结果需要重试。
	/// </summary>
	Retry,
	/// <summary>
	/// 检查失败。
	/// </summary>
	Fail
}
