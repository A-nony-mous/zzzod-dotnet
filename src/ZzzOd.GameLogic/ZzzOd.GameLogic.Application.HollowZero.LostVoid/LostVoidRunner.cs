using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public sealed class LostVoidRunner : ILostVoidRunner, ILostVoidRunnerLifecycle
{
	private readonly ILostVoidLevelExecutor _levelExecutor;

	/// <summary>
	/// <see cref="RunAsync"/> 使用的层数硬顶。仅服务于该未接入生产路径的方法，见其说明。
	/// </summary>
	public int MaxLevelRuns { get; init; } = 20;

	public LostVoidRunner(ILostVoidLevelExecutor? levelExecutor = null)
	{
		_levelExecutor = levelExecutor ?? new OperationLostVoidLevelExecutor();
	}

	/// <inheritdoc />
	public void Pause(ZContext context)
	{
		if (_levelExecutor is ILostVoidLevelExecutorLifecycle lostVoidLevelExecutorLifecycle)
		{
			lostVoidLevelExecutorLifecycle.Pause(context);
		}
		else
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}

	/// <inheritdoc />
	public void Resume(ZContext context)
	{
		if (_levelExecutor is ILostVoidLevelExecutorLifecycle lostVoidLevelExecutorLifecycle)
		{
			lostVoidLevelExecutorLifecycle.Resume(context);
		}
	}

	/// <inheritdoc />
	public void Stop(ZContext context)
	{
		if (_levelExecutor is ILostVoidLevelExecutorLifecycle lostVoidLevelExecutorLifecycle)
		{
			lostVoidLevelExecutorLifecycle.Stop(context);
		}
		else
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}

	/// <summary>
	/// 循环执行迷失之地关卡直至通关或失败，层数达到 <see cref="MaxLevelRuns"/> 时视为失败退出。
	/// 未接入生产路径：生产侧的层间跳转由 <c>LostVoidAppOperation</c> 的节点图驱动，
	/// 按节点状态逐层调用 <see cref="RunLevelAsync"/>，并不经过本方法的循环与层数硬顶；
	/// 这里硬编码的层数上限在真实游戏中也没有对应依据。仅因既有单元测试直接调用而保留，未删除。
	/// </summary>
	public async Task<OperationResult> RunAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, CancellationToken cancellationToken)
	{
		context.LostVoid.InitLostVoidDetectorModel();
		string regionType = "入口";
		for (int i = 0; i < MaxLevelRuns; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			OperationResult result = await RunLevelAsync(context, config, runRecord, regionType, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!result.IsSuccess || string.Equals(result.Status, "通关", StringComparison.Ordinal))
			{
				return result;
			}
			if (!string.Equals(result.Status, "进入下层", StringComparison.Ordinal))
			{
				return result;
			}
			regionType = (result.Data as string) ?? "入口";
			context.LostVoid.HadInteractedOpheliaOnCurrentLevel = false;
		}
		return new OperationResult(IsSuccess: false, "迷失之地层间移动超过最大层数");
	}

	/// <inheritdoc />
	public Task<OperationResult> RunLevelAsync(ZContext context, LostVoidConfig config, LostVoidRunRecord runRecord, string regionType, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return _levelExecutor.RunLevelAsync(context, runRecord, regionType, cancellationToken);
	}
}
