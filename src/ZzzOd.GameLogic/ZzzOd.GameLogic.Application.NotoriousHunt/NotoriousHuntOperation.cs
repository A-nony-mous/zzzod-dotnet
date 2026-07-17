using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎应用主流程。
/// </summary>
public sealed class NotoriousHuntOperation : ZOperation
{
	/// <summary>未配置恶名狩猎计划。</summary>
	public const string StatusNoPlan = "未配置恶名狩猎计划";

	/// <summary>本轮计划已完成。</summary>
	public const string StatusRoundFinished = "本轮计划已完成";

	private readonly NotoriousHuntConfig _config;

	private readonly NotoriousHuntRunRecord _runRecord;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _huntAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToWorldAsync;

	private ChargePlanItem? _nextPlan;

	private ChargePlanItem? _lastTriedPlan;

	private ZzzOd.GameLogic.Operations.Compendium.NotoriousHunt? _huntOperation;

	/// <summary>
	/// 初始化恶名狩猎应用主流程。
	/// </summary>
	public NotoriousHuntOperation(ZContext context, NotoriousHuntConfig config, NotoriousHuntRunRecord runRecord, Func<ZContext, ChargePlanItem, Task<OperationResult>>? transportAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? huntAsync = null, Func<ZContext, Task<OperationResult>>? backToWorldAsync = null)
		: base(context, "恶名狩猎")
	{
		_config = config;
		_runRecord = runRecord;
		_transportAsync = transportAsync ?? new Func<ZContext, ChargePlanItem, Task<OperationResult>>(DefaultTransportAsync);
		_huntAsync = huntAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)delegate(ZContext ctx, ChargePlanItem plan)
		{
			_huntOperation = new ZzzOd.GameLogic.Operations.Compendium.NotoriousHunt(ctx, plan, null, null, useChargePower: false, _config, _runRecord);
			return _huntOperation.ExecuteAsync();
		});
		_backToWorldAsync = backToWorldAsync ?? ((Func<ZContext, Task<OperationResult>>)((ZContext ctx) => new BackToNormalWorld(ctx).ExecuteAsync()));
	}

	/// <summary>
	/// 开始恶名狩猎。
	/// </summary>
	[OperationNode("开始恶名狩猎", IsStartNode = true)]
	public OperationRoundResult StartHunt()
	{
		_lastTriedPlan = null;
		foreach (ChargePlanItem plan in _config.PlanList)
		{
			plan.Skipped = false;
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 仅在子恶名狩猎 Operation 的自动战斗节点恢复自动战斗。
	/// </summary>
	public void ResumeAutoBattle()
	{
		_huntOperation?.ResumeAutoBattle();
	}

	/// <summary>
	/// 查找下一条计划。
	/// </summary>
	[NodeFrom("开始恶名狩猎")]
	[NodeFrom("跳过或结束计划")]
	[NodeFrom("判断剩余次数")]
	[OperationNode("查找下一条计划")]
	public OperationRoundResult FindNextPlan()
	{
		if (_config.PlanList.Count == 0)
		{
			return RoundSuccess("未配置恶名狩猎计划");
		}
		if (_config.AllPlanFinished())
		{
			if (!_config.Loop)
			{
				return RoundSuccess("本轮计划已完成");
			}
			_lastTriedPlan = null;
			_config.ResetPlans();
		}
		_nextPlan = _config.GetNextPlan(_lastTriedPlan);
		return (_nextPlan == null) ? RoundSuccess("本轮计划已完成") : RoundSuccess();
	}

	/// <summary>
	/// 传送到计划副本。
	/// </summary>
	[NodeFrom("查找下一条计划")]
	[OperationNode("传送")]
	public async Task<OperationRoundResult> Transport()
	{
		if (_nextPlan == null)
		{
			return RoundFail("未选择计划");
		}
		return RoundByOperationResult(await _transportAsync(base.ZContext, _nextPlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 特训目标已达成或头像找不到时跳过。
	/// </summary>
	[NodeFrom("传送", Success = false, Status = "找不到 代理人方案培养")]
	[NodeFrom("恶名狩猎", Status = "特训目标已达成")]
	[OperationNode("跳过或结束计划")]
	public OperationRoundResult SkipPlanOrFinish()
	{
		if (_nextPlan != null)
		{
			_nextPlan.Skipped = true;
			_lastTriedPlan = _nextPlan;
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 执行恶名狩猎。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("恶名狩猎")]
	public async Task<OperationRoundResult> Hunt()
	{
		if (_nextPlan == null)
		{
			return RoundFail("未选择计划");
		}
		return RoundByOperationResult(await _huntAsync(base.ZContext, _nextPlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 判断剩余奖励次数。
	/// </summary>
	[NodeFrom("恶名狩猎", Success = true)]
	[NodeFrom("恶名狩猎", Success = false)]
	[OperationNode("判断剩余次数")]
	public OperationRoundResult CheckLeftTimes()
	{
		if (_runRecord.LeftTimes == 0)
		{
			return RoundSuccess("周期挑战无剩余次数");
		}
		if (_nextPlan != null)
		{
			if (base.PreviousNode.IsSuccess)
			{
				_lastTriedPlan = null;
			}
			else
			{
				_nextPlan.Skipped = true;
				_lastTriedPlan = _nextPlan;
			}
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 点击奖励入口。
	/// </summary>
	[NodeFrom("判断剩余次数", Status = "周期挑战无剩余次数")]
	[NodeFrom("恶名狩猎", Status = "周期挑战无剩余次数")]
	[NodeFrom("查找下一条计划", Status = "未配置恶名狩猎计划")]
	[NodeFrom("查找下一条计划", Status = "本轮计划已完成")]
	[OperationNode("点击奖励入口")]
	public OperationRoundResult ClickRewardEntry()
	{
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromSeconds(1L);
		return RoundByClickArea("恶名狩猎", "奖励入口", clickLeftTop: false, null, successDelay, retryDelay);
	}

	/// <summary>
	/// 全部领取。
	/// </summary>
	[NodeFrom("点击奖励入口")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone)]
	[OperationNode("全部领取", NodeMaxRetryTimes = 2)]
	public OperationRoundResult ClaimAll()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(1L);
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		return RoundByFindAndClickArea(lastScreenshot, "恶名狩猎", "全部领取", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("点击奖励入口", Success = false)]
	[NodeFrom("全部领取")]
	[NodeFrom("全部领取", Success = false)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private static Task<OperationResult> DefaultTransportAsync(ZContext context, ChargePlanItem plan)
	{
		return new TransportByCompendium(context, plan.TabName, plan.CategoryName, plan.MissionTypeName).ExecuteAsync();
	}
}
