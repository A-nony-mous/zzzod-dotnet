using OneDragon.Core.Abstractions.Geometry;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public sealed record HollowMapMoveResult(HollowZeroMapNode NextNode, Point ClickPosition, bool Clicked);
