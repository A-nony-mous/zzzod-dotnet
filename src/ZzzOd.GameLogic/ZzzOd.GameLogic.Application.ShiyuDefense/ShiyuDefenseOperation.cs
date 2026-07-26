using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用主流程。
/// </summary>
public sealed class ShiyuDefenseOperation : ZOperation
{
	/// <summary>所有节点都完成挑战。</summary>
	public const string StatusAllFinished = "所有节点都完成挑战";

	/// <summary>下一节点。</summary>
	public const string StatusNextNode = "下一节点";

	/// <summary>房间挑战完成。</summary>
	public const string StatusRoomComplete = "房间挑战完成";

	/// <summary>所有房间完成。</summary>
	public const string StatusAllRoomsComplete = "所有房间完成";

	private readonly ShiyuDefenseConfig _config;

	private readonly ShiyuDefenseRunRecord _runRecord;

	private readonly IShiyuDefenseOperationServices _services;

	private int _currentNodeIndex;

	private int _phaseIndex;

	private int _currentRoomIndex;

	private List<DefensePhaseTeamInfo> _phaseTeamList = new List<DefensePhaseTeamInfo>();

	private List<DefensePhaseTeamInfo> _roomTeams = new List<DefensePhaseTeamInfo>();

	/// <summary>
	/// 当前挑战节点。
	/// </summary>
	public int CurrentNodeIndex => _currentNodeIndex;

	/// <summary>
	/// 当前普通节点阶段。
	/// </summary>
	public int PhaseIndex => _phaseIndex;

	/// <summary>
	/// 当前多间模式房间。
	/// </summary>
	public int CurrentRoomIndex => _currentRoomIndex;

	/// <summary>
	/// 普通节点阶段队伍。
	/// </summary>
	public IReadOnlyList<DefensePhaseTeamInfo> PhaseTeamList => _phaseTeamList;

	/// <summary>
	/// 多间模式房间队伍。
	/// </summary>
	public IReadOnlyList<DefensePhaseTeamInfo> RoomTeams => _roomTeams;

	/// <summary>
	/// 初始化式舆防卫战应用主流程。
	/// </summary>
	public ShiyuDefenseOperation(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, IShiyuDefenseOperationServices? services = null)
		: base(context, "式舆防卫战")
	{
		_config = config;
		_runRecord = runRecord;
		_services = services ?? new DefaultShiyuDefenseOperationServices();
	}

	/// <summary>
	/// 传送。
	/// </summary>
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		return RoundByOperationResult(await _services.TransportAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待画面加载。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("等待画面加载", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> WaitLoading()
	{
		OperationResult result = await _services.WaitForMainScreenAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return string.Equals(result.Status, "前次行动最佳记录", StringComparison.Ordinal) ? RoundWait(result.Status, null, TimeSpan.FromSeconds(2L)) : RoundSuccess(result.Status);
	}

	/// <summary>
	/// 选择节点。
	/// </summary>
	[NodeFrom("等待画面加载")]
	[OperationNode("选择节点")]
	public async Task<OperationRoundResult> ChooseNodeIndex()
	{
		int? nextIndex = await _services.GetNextNodeIndexAsync(base.ZContext, _config, _runRecord, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!nextIndex.HasValue)
		{
			return RoundSuccess("所有节点都完成挑战");
		}
		_currentNodeIndex = nextIndex.Value;
		if (ShiyuDefenseConstants.MultiRoomNodes.Contains(_currentNodeIndex))
		{
			_currentRoomIndex = 0;
			_roomTeams = new List<DefensePhaseTeamInfo>();
		}
		OperationResult result = await _services.SelectNodeAsync(base.ZContext, _currentNodeIndex, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return ShiyuDefenseConstants.MultiRoomNodes.Contains(_currentNodeIndex) ? RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L)) : RoundWait(result.Status, result.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 识别弱点并计算配队。
	/// </summary>
	[NodeFrom("选择节点")]
	[NodeFrom("下一节点")]
	[OperationNode("识别弱点并计算配队", NodeMaxRetryTimes = 10)]
	public async Task<OperationRoundResult> CheckWeakness()
	{
		if (ShiyuDefenseConstants.MultiRoomNodes.Contains(_currentNodeIndex))
		{
			OperationResult ready = await _services.PrepareMultiRoomAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
			if (!ready.IsSuccess)
			{
				return RoundRetry(ready.Status, null, TimeSpan.FromSeconds(1L));
			}
			if (string.Equals(ready.Status, "点击确认", StringComparison.Ordinal))
			{
				return RoundWait(ready.Status, null, TimeSpan.FromSeconds(2L));
			}
			if (string.Equals(ready.Status, "已重置", StringComparison.Ordinal))
			{
				return RoundRetry(ready.Status, null, TimeSpan.FromSeconds(1L));
			}
			_roomTeams = (await _services.CalculateMultiRoomTeamsAsync(base.ZContext, _config, _currentNodeIndex, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false)).ToList();
			if (_roomTeams.Count < ShiyuDefenseConstants.RoomNames.Count)
			{
				return RoundRetry("配队计算失败 请检查配置", null, TimeSpan.FromSeconds(1L));
			}
			for (int roomIndex = 0; roomIndex < _roomTeams.Count; roomIndex++)
			{
				int teamIndex = _roomTeams[roomIndex].TeamIndex;
				if (teamIndex >= 0 && !IsValidTeamIndex(teamIndex))
				{
					return RoundRetry(ShiyuDefenseConstants.RoomNames[roomIndex] + "未找到编队", null, TimeSpan.FromSeconds(1L));
				}
			}
			return RoundSuccess("多间模式");
		}
		_phaseTeamList = (await _services.CalculateTeamsAsync(base.ZContext, _config, _currentNodeIndex, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false)).ToList();
		if (_phaseTeamList.Count < 2)
		{
			return RoundRetry("当前配置计算配队未足够多阶段 请检查配置", null, TimeSpan.FromSeconds(1L));
		}
		for (int phaseIndex = 0; phaseIndex < _phaseTeamList.Count; phaseIndex++)
		{
			if (!IsValidTeamIndex(_phaseTeamList[phaseIndex].TeamIndex))
			{
				return RoundRetry($"阶段 {phaseIndex + 1} 未找到编队", null, TimeSpan.FromSeconds(1L));
			}
		}
		OperationResult enterTeam = await _services.EnterTeamSelectionAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		return enterTeam.IsSuccess ? RoundSuccess(enterTeam.Status, enterTeam.Data, TimeSpan.FromSeconds(1L)) : RoundRetry(enterTeam.Status, enterTeam.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 选择配队。
	/// </summary>
	[NodeFrom("识别弱点并计算配队", Status = "角色头像")]
	[OperationNode("选择配队")]
	public async Task<OperationRoundResult> ChooseTeam()
	{
		return RoundByOperationResult(await _services.ChooseTeamAsync(base.ZContext, _phaseTeamList.Select((DefensePhaseTeamInfo team) => team.TeamIndex).ToArray()).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 多间选择房间。
	/// </summary>
	[NodeFrom("识别弱点并计算配队", Status = "多间模式")]
	[NodeFrom("多间-战斗结束", Status = "房间挑战完成")]
	[OperationNode("多间-选择房间", NodeMaxRetryTimes = 30)]
	public async Task<OperationRoundResult> MultiRoomSelect()
	{
		int roomIndex = _roomTeams.FindIndex((DefensePhaseTeamInfo team) => team.TeamIndex >= 0);
		if (roomIndex < 0)
		{
			_runRecord.AddNodeFinished(_currentNodeIndex);
			return RoundSuccess("所有房间完成");
		}
		_currentRoomIndex = roomIndex;
		OperationResult result = await _services.SelectRoomAsync(base.ZContext, roomIndex, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L)) : RoundRetry(result.Status, result.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 多间等待预备编队。
	/// </summary>
	[NodeFrom("多间-选择房间")]
	[OperationNode("多间-等待预备编队")]
	public async Task<OperationRoundResult> MultiRoomWaitPrepare()
	{
		OperationResult result = await _services.WaitAndChooseMultiRoomTeamAsync(base.ZContext, _roomTeams[_currentRoomIndex].TeamIndex, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return string.Equals(result.Status, "下一步", StringComparison.Ordinal) ? RoundRetry("未找到预备编队", null, TimeSpan.FromSeconds(1L)) : RoundSuccess("预备编队完成");
	}

	/// <summary>
	/// 多间出战。
	/// </summary>
	[NodeFrom("多间-等待预备编队", Status = "预备编队完成")]
	[OperationNode("多间-出战")]
	public async Task<OperationRoundResult> MultiRoomDeploy()
	{
		OperationResult result = await _services.DeployAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess)
		{
			_roomTeams[_currentRoomIndex].TeamIndex = -1;
		}
		return RoundByOperationResult(result);
	}

	/// <summary>
	/// 多间战斗。
	/// </summary>
	[NodeFrom("多间-出战")]
	[OperationNode("多间-战斗")]
	public async Task<OperationRoundResult> MultiRoomBattle()
	{
		return (await _services.BattleAsync(base.ZContext, _roomTeams[_currentRoomIndex].TeamIndex).ConfigureAwait(continueOnCapturedContext: false)).IsSuccess ? RoundSuccess() : RoundSuccess("战斗失败");
	}

	/// <summary>
	/// 多间战斗结束。
	/// </summary>
	[NodeFrom("多间-战斗")]
	[OperationNode("多间-战斗结束", NodeMaxRetryTimes = 30)]
	public async Task<OperationRoundResult> MultiRoomExit()
	{
		OperationResult result = await _services.ExitMultiRoomAfterBattleAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess("房间挑战完成", null, TimeSpan.FromSeconds(5L)) : RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 多间返回主界面。
	/// </summary>
	[NodeFrom("多间-战斗结束", Status = "所有房间完成")]
	[NodeFrom("多间-选择房间", Status = "所有房间完成")]
	[OperationNode("多间-返回主界面")]
	public async Task<OperationRoundResult> MultiRoomBack()
	{
		return RoundByOperationResult(await _services.BackToMainScreenAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false), null, retryOnFail: true);
	}

	/// <summary>
	/// 多间战斗失败。
	/// </summary>
	[NodeFrom("多间-战斗", Status = "战斗失败")]
	[OperationNode("多间-战斗失败", NodeMaxRetryTimes = 30)]
	public async Task<OperationRoundResult> MultiRoomFailed()
	{
		OperationResult result = await _services.RecoverFromMultiRoomFailureAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L)) : RoundRetry(result.Status, result.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 出战。
	/// </summary>
	[NodeFrom("选择配队")]
	[OperationNode("出战")]
	public async Task<OperationRoundResult> Deploy()
	{
		_phaseIndex = 0;
		return RoundByOperationResult(await _services.DeployAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 自动战斗。
	/// </summary>
	[NodeFrom("出战")]
	[OperationNode("自动战斗")]
	public async Task<OperationRoundResult> ShiyuBattle()
	{
		if (!(await _services.BattleAsync(base.ZContext, _phaseTeamList[_phaseIndex].TeamIndex).ConfigureAwait(continueOnCapturedContext: false)).IsSuccess)
		{
			return RoundSuccess();
		}
		_phaseIndex++;
		if (_phaseIndex < _phaseTeamList.Count)
		{
			return RoundWait();
		}
		_runRecord.AddNodeFinished(_currentNodeIndex);
		return RoundSuccess("下一节点");
	}

	/// <summary>
	/// 下一节点。
	/// </summary>
	[NodeFrom("自动战斗", Status = "下一节点")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone, Detail = true)]
	[OperationNode("下一节点")]
	public async Task<OperationRoundResult> ToNextNode()
	{
		OperationResult result = await _services.AdvanceAfterBattleAsync(base.ZContext, _currentNodeIndex, _config, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			// "节点-05"找不到按 3 秒重试；其余失败（例如最后一层的"战斗结束-退出"）按 1 秒重试
			bool isNodeFiveFailure = result.Status != null && result.Status.Contains("节点-05", StringComparison.Ordinal);
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(isNodeFiveFailure ? 3L : 1L));
		}
		if (result.Data is int nodeIndex)
		{
			_currentNodeIndex = nodeIndex;
			return RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L));
		}
		if (string.Equals(result.Status, "下一步", StringComparison.Ordinal))
		{
			_currentNodeIndex++;
			return RoundSuccess(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		if (string.Equals(result.Status, "战斗结束-下一防线", StringComparison.Ordinal))
		{
			return RoundWait(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(5L));
	}

	/// <summary>
	/// 所有节点完成。
	/// </summary>
	[NodeFrom("下一节点", Success = false)]
	[NodeFrom("下一节点", Status = "战斗结束-退出")]
	[OperationNode("所有节点完成", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> AllNodeFinished()
	{
		OperationResult result = await _services.FinishAllNodesAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess(result.Status) : RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 领取奖励。
	/// </summary>
	[NodeFrom("所有节点完成")]
	[NodeFrom("选择节点", Status = "所有节点都完成挑战")]
	[NodeFrom("多间-返回主界面")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("领取奖励")]
	public async Task<OperationRoundResult> ClaimReward()
	{
		OperationResult result = await _services.ClaimRewardAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		return string.Equals(result.Status, "奖励入口", StringComparison.Ordinal) ? RoundWait(result.Status, null, TimeSpan.FromMilliseconds(500L)) : RoundSuccess(result.Status, result.Data, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 关闭奖励。
	/// </summary>
	[NodeFrom("领取奖励")]
	[OperationNode("关闭奖励")]
	public async Task<OperationRoundResult> CloseReward()
	{
		OperationResult result = await _services.CloseRewardAsync(base.ZContext, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.IsSuccess)
		{
			return RoundRetry(result.Status, null, TimeSpan.FromSeconds(1L));
		}
		string status = result.Status;
		bool flag = ((status == "领取奖励-确认" || status == "领取奖励-关闭") ? true : false);
		return flag ? RoundWait(result.Status, result.Data, TimeSpan.FromMilliseconds(500L)) : RoundSuccess(result.Status, result.Data);
	}

	/// <summary>
	/// 结束后返回。
	/// </summary>
	[NodeFrom("自动战斗")]
	[NodeFrom("关闭奖励")]
	[NodeFrom("多间-战斗失败")]
	[OperationNode("结束后返回")]
	public async Task<OperationRoundResult> BackAfterAll()
	{
		return RoundByOperationResult(await _services.BackToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private bool IsValidTeamIndex(int teamIndex)
	{
		return teamIndex >= 0 && base.ZContext.TeamConfig.TeamList.Any((PredefinedTeamInfo team) => team.Idx == teamIndex);
	}
}
