using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 挑战战斗段协作接口。
/// </summary>
public interface IChallengeBattleFlow
{
	/// <summary>
	/// 检查一次自动战斗状态。
	/// </summary>
	OperationResult CheckBattleState(ZContext context, ChargePlanItem plan, string autoBattleName, Mat? screen, DateTimeOffset? screenshotTimeUtc);
}
