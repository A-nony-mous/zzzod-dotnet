using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战主流程服务。
/// </summary>
public interface IShiyuDefenseOperationServices
{
	/// <summary>传送到式舆防卫战。</summary>
	Task<OperationResult> TransportAsync(ZContext context);

	/// <summary>等待式舆防卫战主界面。</summary>
	Task<OperationResult> WaitForMainScreenAsync(ZContext context, Mat? screen);

	/// <summary>获取下一节点。</summary>
	Task<int?> GetNextNodeIndexAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, Mat? screen);

	/// <summary>选择节点。</summary>
	Task<OperationResult> SelectNodeAsync(ZContext context, int nodeIndex, Mat? screen);

	/// <summary>计算普通节点配队。</summary>
	Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen);

	/// <summary>点击普通节点角色头像进入预备编队。</summary>
	Task<OperationResult> EnterTeamSelectionAsync(ZContext context);

	/// <summary>准备多间模式选择界面。</summary>
	Task<OperationResult> PrepareMultiRoomAsync(ZContext context, Mat? screen);

	/// <summary>计算多间模式配队。</summary>
	Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateMultiRoomTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen);

	/// <summary>选择预备编队。</summary>
	Task<OperationResult> ChooseTeamAsync(ZContext context, IReadOnlyList<int> teamIndexes);

	/// <summary>选择房间。</summary>
	Task<OperationResult> SelectRoomAsync(ZContext context, int roomIndex, Mat? screen);

	/// <summary>点击出战。</summary>
	Task<OperationResult> DeployAsync(ZContext context);

	/// <summary>等待并选择多间模式预备编队。</summary>
	Task<OperationResult> WaitAndChooseMultiRoomTeamAsync(ZContext context, int teamIndex, Mat? screen);

	/// <summary>运行战斗。</summary>
	Task<OperationResult> BattleAsync(ZContext context, int teamIndex);

	/// <summary>多间模式战斗后退出。</summary>
	Task<OperationResult> ExitMultiRoomAfterBattleAsync(ZContext context, Mat? screen);

	/// <summary>多间模式返回式舆防卫战主界面。</summary>
	Task<OperationResult> BackToMainScreenAsync(ZContext context, Mat? screen);

	/// <summary>多间模式战斗失败后返回主界面。</summary>
	Task<OperationResult> RecoverFromMultiRoomFailureAsync(ZContext context, Mat? screen);

	/// <summary>普通节点战斗后跳转到下一节点。</summary>
	Task<OperationResult> AdvanceAfterBattleAsync(ZContext context, int currentNodeIndex, ShiyuDefenseConfig config, Mat? screen);

	/// <summary>等待所有节点完成后的主界面。</summary>
	Task<OperationResult> FinishAllNodesAsync(ZContext context, Mat? screen);

	/// <summary>领取奖励。</summary>
	Task<OperationResult> ClaimRewardAsync(ZContext context, Mat? screen);

	/// <summary>关闭奖励。</summary>
	Task<OperationResult> CloseRewardAsync(ZContext context, Mat? screen);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToWorldAsync(ZContext context);
}
