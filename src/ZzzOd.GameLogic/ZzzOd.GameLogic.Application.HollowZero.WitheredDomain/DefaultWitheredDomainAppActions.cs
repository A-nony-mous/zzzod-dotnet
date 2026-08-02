using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 默认枯萎之都入口动作。
/// </summary>
public sealed class DefaultWitheredDomainAppActions : IWitheredDomainAppActions
{
	/// <inheritdoc />
	public Task<OperationResult> CheckFirstScreenAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new WitheredDomainCheckFirstScreenOperation(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> TransportToEntryAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new TransportByCompendium(context, "作战", "周期征讨", "迷失之地").ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> WaitEntryLoadingAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new WitheredDomainChooseEntryOperation(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> ChooseMissionTypeAsync(ZContext context, WitheredDomainRunRecord runRecord, string missionTypeName, CancellationToken cancellationToken)
	{
		if (runRecord.IsFinishedByWeek() || runRecord.IsFinishedByDay())
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "已完成基本次数"));
		}
		return new WitheredDomainChooseMissionTypeOperation(context, missionTypeName).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> ChooseMissionAsync(ZContext context, string missionName, CancellationToken cancellationToken)
	{
		return new WitheredDomainChooseMissionOperation(context, missionName).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickNextAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new WitheredDomainClickNextOperation(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> DeployAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new Deploy(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> WaitBackLoadingAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new WitheredDomainFindAreaOperation(context, "完成后等待加载", "零号空洞-入口", "街区", 20).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> FinishAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new BackToNormalWorld(context).ExecuteAsync(cancellationToken);
	}
}
