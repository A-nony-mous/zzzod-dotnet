using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都入口状态机动作。
/// </summary>
public interface IWitheredDomainAppActions
{
	/// <summary>识别初始画面。</summary>
	Task<OperationResult> CheckFirstScreenAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>通过快捷手册前往枯萎之都入口。</summary>
	Task<OperationResult> TransportToEntryAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>等待入口街区加载。</summary>
	Task<OperationResult> WaitEntryLoadingAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>选择副本类型。</summary>
	Task<OperationResult> ChooseMissionTypeAsync(ZContext context, WitheredDomainRunRecord runRecord, string missionTypeName, CancellationToken cancellationToken);

	/// <summary>选择副本。</summary>
	Task<OperationResult> ChooseMissionAsync(ZContext context, string missionName, CancellationToken cancellationToken);

	/// <summary>处理下一步、行动中确认、出战和继续确认。</summary>
	Task<OperationResult> ClickNextAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>执行出战。</summary>
	Task<OperationResult> DeployAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>完成基本次数后等待入口加载。</summary>
	Task<OperationResult> WaitBackLoadingAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>完成后返回大世界。</summary>
	Task<OperationResult> FinishAsync(ZContext context, CancellationToken cancellationToken);
}
