using System.Threading;
using System.Threading.Tasks;

namespace ZzzOd.GameLogic.HollowZero;

public interface IHollowEventDispatcher
{
	Task<HollowEventHandleResult> DispatchAsync(string eventName, CancellationToken cancellationToken);
}
