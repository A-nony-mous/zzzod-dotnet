using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 默认自动战斗状态适配器。
/// </summary>
public sealed class AutoBattleChallengeFlow : IChallengeBattleFlow
{
	/// <summary>自动战斗仍在进行。</summary>
	public const string StatusRunning = "自动战斗中";

	/// <summary>
	/// 检查一次自动战斗状态。
	/// </summary>
	public OperationResult CheckBattleState(ZContext context, ChargePlanItem plan, string autoBattleName, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		string lastCheckEndResult = context.AutoBattleContext.LastCheckEndResult;
		if (!string.IsNullOrWhiteSpace(lastCheckEndResult))
		{
			context.AutoBattleContext.StopAutoBattle();
			return new OperationResult(IsSuccess: true, lastCheckEndResult);
		}
		if (screen == null || screen.Empty())
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		context.AutoBattleContext.CheckBattleState(screen, screenshotTimeUtc, checkBattleEndNormalResult: true);
		return new OperationResult(IsSuccess: false, "自动战斗中");
	}
}
