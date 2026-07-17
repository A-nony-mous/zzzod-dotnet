using System.Threading;
using System.Threading.Tasks;

namespace ZzzOd.GameLogic.HollowZero;

public interface IHollowEventSource
{
	Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken);
}
