using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 挑战类手册 Operation 依赖的子流程。
/// </summary>
public sealed class ChallengeMissionServices
{
	/// <summary>恢复电量流程。</summary>
	public Func<ZContext, Task<OperationResult>>? RestoreChargeAsync { get; set; }

	/// <summary>选择预备编队流程。</summary>
	public Func<ZContext, ChargePlanItem, Task<OperationResult>>? ChoosePredefinedTeamAsync { get; set; }

	/// <summary>出战流程。</summary>
	public Func<ZContext, Task<OperationResult>>? DeployAsync { get; set; }

	/// <summary>恶名狩猎战前移动流程。</summary>
	public Func<ZContext, ChargePlanItem, Task<OperationResult>>? BeforeBattleMoveAsync { get; set; }

	/// <summary>加载自动战斗指令流程。</summary>
	public Func<ZContext, ChargePlanItem, string, OperationResult>? InitializeAutoBattle { get; set; }

	/// <summary>自动战斗状态检查流程。</summary>
	public IChallengeBattleFlow BattleFlow { get; set; } = new AutoBattleChallengeFlow();
}
