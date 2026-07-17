using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

/// <summary>
/// 按枯萎之都挑战配置选择地图节点并执行前台区域点击。
/// </summary>
public sealed class WitheredDomainMapNavigator : IHollowMapNavigator
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化导航器。
	/// </summary>
	public WitheredDomainMapNavigator(ZContext context)
	{
		_context = context ?? throw new ArgumentNullException("context");
	}

	/// <inheritdoc />
	public HollowMapMoveResult? MoveNext(HollowZeroMap map, Mat? screen)
	{
		ArgumentNullException.ThrowIfNull(map, "map");
		HollowZeroMapNode hollowZeroMapNode = _context.WitheredDomain.GetNextToMove(map)?.NextNodeToMove;
		if (hollowZeroMapNode == null)
		{
			return null;
		}
		OneDragon.Core.Abstractions.Geometry.Point mapNodeClickPosition = GetMapNodeClickPosition(hollowZeroMapNode, screen);
		bool flag = _context.Controller?.Click(mapNodeClickPosition) ?? false;
		if (flag)
		{
			_context.WitheredDomain.UpdateContextAfterMove(map, hollowZeroMapNode);
		}
		return new HollowMapMoveResult(hollowZeroMapNode, mapNodeClickPosition, flag);
	}

	private OneDragon.Core.Abstractions.Geometry.Point GetMapNodeClickPosition(HollowZeroMapNode nextNode, Mat? screen)
	{
		if (screen == null)
		{
			return nextNode.Pos.Center;
		}
		OneDragon.Core.Screen.ScreenArea area = _context.ScreenContext.GetArea("零号空洞-事件", "格子入口选项");
		if (area == null)
		{
			return nextNode.Pos.Center;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = _context.OcrService.GetOcrResultList(screen, null, area.Rect);
		if (ocrResultList.Count == 0)
		{
			return nextNode.Pos.Center;
		}
		string[] entryOptions = new string[4]
		{
			HollowZeroSpecialEvent.ResoniumStore5.EventName,
			HollowZeroSpecialEvent.CriticalStageEntry.EventName,
			HollowZeroSpecialEvent.CriticalStageEntry2.EventName,
			HollowZeroSpecialEvent.DoorBattleEntry.EventName
		};
		int top = ocrResultList.Min((OcrMatchResult result) => result.Y);
		return SelectMapNodeClickPosition(nextNode, ocrResultList.Where((OcrMatchResult result) => result.Y - top < 20).FirstOrDefault((OcrMatchResult result) => StringUtils.FindBestMatchByDifflib(result.Text, entryOptions).HasValue)?.Center);
	}

	internal static OneDragon.Core.Abstractions.Geometry.Point SelectMapNodeClickPosition(HollowZeroMapNode nextNode, OneDragon.Core.Abstractions.Geometry.Point? entryOptionCenter)
	{
		ArgumentNullException.ThrowIfNull(nextNode, "nextNode");
		if (!entryOptionCenter.HasValue)
		{
			return nextNode.Pos.Center;
		}
		OneDragon.Core.Abstractions.Geometry.Point[] source = new OneDragon.Core.Abstractions.Geometry.Point[4]
		{
			new OneDragon.Core.Abstractions.Geometry.Point(nextNode.Pos.X1 + 15, nextNode.Pos.Y1 + 15),
			new OneDragon.Core.Abstractions.Geometry.Point(nextNode.Pos.X1 + 15, nextNode.Pos.Y2 - 15),
			new OneDragon.Core.Abstractions.Geometry.Point(nextNode.Pos.X2 - 15, nextNode.Pos.Y1 + 15),
			new OneDragon.Core.Abstractions.Geometry.Point(nextNode.Pos.X2 - 15, nextNode.Pos.Y2 - 15)
		};
		return source.MaxBy(delegate(OneDragon.Core.Abstractions.Geometry.Point point)
		{
			double num = point.X - entryOptionCenter.Value.X;
			double num2 = point.Y - entryOptionCenter.Value.Y;
			return num * num + num2 * num2;
		});
	}
}
