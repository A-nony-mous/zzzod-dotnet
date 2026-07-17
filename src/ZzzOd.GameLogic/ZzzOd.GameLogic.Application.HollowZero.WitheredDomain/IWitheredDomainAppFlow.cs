using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都应用流程。
/// </summary>
public interface IWitheredDomainAppFlow
{
	Task<OperationResult> RunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, CancellationToken cancellationToken);
}
