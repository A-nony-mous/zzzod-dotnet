using System;
using OneDragon.Core.Abstractions.Geometry;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class ControllerHollowMapNavigator : IHollowMapNavigator
{
	private readonly ZContext _context;

	private readonly IHollowRouteSelector _routeSelector;

	public ControllerHollowMapNavigator(ZContext context, IHollowRouteSelector? routeSelector = null)
	{
		_context = context ?? throw new ArgumentNullException("context");
		_routeSelector = routeSelector ?? new HollowRouteSelector();
	}

	public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
	{
		HollowZeroMapNode hollowZeroMapNode = _routeSelector.SelectNextNode(map);
		if (hollowZeroMapNode == null)
		{
			return null;
		}
		OneDragon.Core.Abstractions.Geometry.Point center = hollowZeroMapNode.Pos.Center;
		bool clicked = _context.Controller?.Click(center) ?? false;
		return new HollowMapMoveResult(hollowZeroMapNode, center, clicked);
	}
}
