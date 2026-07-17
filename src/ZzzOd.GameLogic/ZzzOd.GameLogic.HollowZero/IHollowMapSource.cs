using System.Threading;
using System.Threading.Tasks;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public interface IHollowMapSource
{
	Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken);
}
