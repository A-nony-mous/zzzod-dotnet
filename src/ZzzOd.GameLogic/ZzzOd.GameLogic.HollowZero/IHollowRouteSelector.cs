using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public interface IHollowRouteSelector
{
	HollowZeroMapNode? SelectNextNode(HollowZeroMap map);
}
