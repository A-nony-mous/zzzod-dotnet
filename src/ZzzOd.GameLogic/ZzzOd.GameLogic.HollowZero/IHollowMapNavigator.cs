using OpenCvSharp;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public interface IHollowMapNavigator
{
	HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen);
}
