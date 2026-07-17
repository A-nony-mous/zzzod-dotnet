using System.Threading;
using System.Threading.Tasks;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class EmptyHollowMapSource : IHollowMapSource
{
	public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
	{
		return Task.FromResult<HollowZeroMap>(null);
	}
}
