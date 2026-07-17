using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都 runner。
/// </summary>
public interface IWitheredDomainRunner
{
	Task<OperationResult> RunAsync(ZContext context, WitheredDomainConfig config, WitheredDomainRunRecord runRecord, CancellationToken cancellationToken);
}
