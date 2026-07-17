using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.Tests.HollowZero;

public class HollowPathfindingTests
{
	[Fact]
	public void TestPathfinding_BasicGraph()
	{
		HollowZeroEntry entry = new HollowZeroEntry("1001");
		HollowZeroEntry entry2 = new HollowZeroEntry("1002");
		HollowZeroEntry entry3 = new HollowZeroEntry("1003");
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 10, 10), entry);
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(20, 0, 30, 10), entry2);
		HollowZeroMapNode hollowZeroMapNode3 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(40, 0, 50, 10), entry3);
		List<HollowZeroMapNode> nodes = new List<HollowZeroMapNode> { hollowZeroMapNode, hollowZeroMapNode2, hollowZeroMapNode3 };
		Dictionary<int, List<int>> edges = new Dictionary<int, List<int>>
		{
			{
				0,
				new List<int> { 1 }
			},
			{
				1,
				new List<int> { 0, 2 }
			},
			{
				2,
				new List<int> { 1 }
			}
		};
		HollowZeroMap currentMap = new HollowZeroMap(nodes, 0, edges);
		HollowPathfinding.SearchMap(currentMap, null, null);
		Assert.Equal(0, hollowZeroMapNode.PathStepCnt);
		Assert.Equal(0, hollowZeroMapNode.PathNodeCnt);
		Assert.Equal(1, hollowZeroMapNode2.PathStepCnt);
		Assert.Equal(1, hollowZeroMapNode2.PathNodeCnt);
		Assert.Equal(2, hollowZeroMapNode3.PathStepCnt);
		Assert.Equal(2, hollowZeroMapNode3.PathNodeCnt);
	}

	[Fact]
	public void ColorCodedScreenshotMapSource_ParsesMapAndSelectsNextRouteNode()
	{
		using Mat mat = new Mat(120, 220, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(10, 40, 30, 30), new Scalar(0.0, 0.0, 255.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(110, 40, 30, 30), new Scalar(0.0, 255.0, 0.0), -1);
		HollowZeroMap hollowZeroMap = ColorCodedHollowMapSource.Parse(mat);
		HollowZeroMapNode hollowZeroMapNode = new HollowRouteSelector().SelectNextNode(hollowZeroMap);
		Assert.NotNull(hollowZeroMap);
		Assert.Equal(2, hollowZeroMap.Nodes.Count);
		Assert.Equal(0, hollowZeroMap.CurrentIdx);
		Assert.NotNull(hollowZeroMapNode);
		Assert.Equal("目标", hollowZeroMapNode.Entry.EntryName);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(125, 55), hollowZeroMapNode.Pos.Center);
	}

	[Fact]
	public void ColorCodedScreenshotMapSource_ReturnsNullWhenNoNodes()
	{
		using Mat screen = new Mat(80, 80, MatType.CV_8UC3, Scalar.Black);
		HollowZeroMap hollowZeroMap = ColorCodedHollowMapSource.Parse(screen);
		Assert.Null(hollowZeroMap);
	}

	[Fact]
	public void ColorCodedScreenshotMapSource_IgnoresTinyNoiseContours()
	{
		using Mat mat = new Mat(80, 80, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(4, 4, 3, 3), new Scalar(0.0, 0.0, 255.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(20, 20, 4, 4), new Scalar(0.0, 255.0, 0.0), -1);
		HollowZeroMap hollowZeroMap = ColorCodedHollowMapSource.Parse(mat);
		Assert.Null(hollowZeroMap);
	}

	[Fact]
	public void ColorCodedScreenshotMapSource_KeepsMapInvalidWhenCurrentNodeMissing()
	{
		using Mat mat = new Mat(120, 220, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(40, 40, 30, 30), new Scalar(0.0, 255.0, 0.0), -1);
		HollowZeroMap hollowZeroMap = ColorCodedHollowMapSource.Parse(mat);
		Assert.NotNull(hollowZeroMap);
		Assert.False(hollowZeroMap.IsValidMap);
		Assert.Null(hollowZeroMap.CurrentIdx);
		Assert.Single(hollowZeroMap.Nodes);
	}

	[Fact]
	public void ColorCodedScreenshotMapSource_BuildsEdgesOnlyWithinRowOrColumnThreshold()
	{
		using Mat mat = new Mat(260, 360, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(10, 40, 30, 30), new Scalar(0.0, 0.0, 255.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(110, 40, 30, 30), new Scalar(0.0, 255.0, 0.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(10, 150, 30, 30), new Scalar(0.0, 255.0, 0.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(260, 210, 30, 30), new Scalar(0.0, 255.0, 0.0), -1);
		HollowZeroMap map = ColorCodedHollowMapSource.Parse(mat);
		Assert.NotNull(map);
		int value = map.CurrentIdx.Value;
		Assert.Equal(2, map.Edges[value].Count);
		Assert.Contains((IEnumerable<int>)map.Edges[value], (Predicate<int>)((int idx) => map.Nodes[idx].Pos.Center == new OneDragon.Core.Abstractions.Geometry.Point(125, 55)));
		Assert.Contains((IEnumerable<int>)map.Edges[value], (Predicate<int>)((int idx) => map.Nodes[idx].Pos.Center == new OneDragon.Core.Abstractions.Geometry.Point(25, 165)));
		Assert.DoesNotContain((IEnumerable<int>)map.Edges[value], (Predicate<int>)((int idx) => map.Nodes[idx].Pos.Center == new OneDragon.Core.Abstractions.Geometry.Point(275, 225)));
	}

	[Fact]
	public void HollowRouteSelector_SkipsNodesOverVisitLimit()
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 10, 10), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(20, 0, 30, 10), new HollowZeroEntry("0001-已达上限", isBenefit: true, 1, isBase: false, canGo: true, isTp: false, moveAfterwards: false, 1))
		{
			VisitedTimes = 1
		};
		HollowZeroMapNode hollowZeroMapNode3 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(40, 0, 50, 10), new HollowZeroEntry("0002-可前往"));
		int num = 3;
		List<HollowZeroMapNode> list = new List<HollowZeroMapNode>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<HollowZeroMapNode> span = CollectionsMarshal.AsSpan(list);
		span[0] = hollowZeroMapNode;
		span[1] = hollowZeroMapNode2;
		span[2] = hollowZeroMapNode3;
		int? currentIdx = 0;
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		num = 2;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<int> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = 1;
		span2[1] = 2;
		dictionary[0] = list2;
		num = 1;
		List<int> list3 = new List<int>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = 0;
		dictionary[1] = list3;
		num = 1;
		List<int> list4 = new List<int>(num);
		CollectionsMarshal.SetCount(list4, num);
		CollectionsMarshal.AsSpan(list4)[0] = 0;
		dictionary[2] = list4;
		HollowZeroMap map = new HollowZeroMap(list, currentIdx, dictionary);
		HollowZeroMapNode actual = new HollowRouteSelector().SelectNextNode(map);
		Assert.Same(hollowZeroMapNode3, actual);
	}

	[Fact]
	public void HollowPathfinding_IgnoresCanGoFalseNodes()
	{
		HollowZeroMapNode hollowZeroMapNode = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 10, 10), new HollowZeroEntry("0000-当前"));
		HollowZeroMapNode hollowZeroMapNode2 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(20, 0, 30, 10), new HollowZeroEntry("0001-阻塞", isBenefit: true, 1, isBase: false, canGo: false));
		HollowZeroMapNode hollowZeroMapNode3 = new HollowZeroMapNode(new OneDragon.Core.Abstractions.Geometry.Rect(40, 0, 50, 10), new HollowZeroEntry("0002-目标"));
		int num = 3;
		List<HollowZeroMapNode> list = new List<HollowZeroMapNode>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<HollowZeroMapNode> span = CollectionsMarshal.AsSpan(list);
		span[0] = hollowZeroMapNode;
		span[1] = hollowZeroMapNode2;
		span[2] = hollowZeroMapNode3;
		int? currentIdx = 0;
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		num = 1;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 1;
		dictionary[0] = list2;
		num = 2;
		List<int> list3 = new List<int>(num);
		CollectionsMarshal.SetCount(list3, num);
		Span<int> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = 0;
		span2[1] = 2;
		dictionary[1] = list3;
		num = 1;
		List<int> list4 = new List<int>(num);
		CollectionsMarshal.SetCount(list4, num);
		CollectionsMarshal.AsSpan(list4)[0] = 1;
		dictionary[2] = list4;
		HollowZeroMap hollowZeroMap = new HollowZeroMap(list, currentIdx, dictionary);
		HollowPathfinding.SearchMap(hollowZeroMap, null, null);
		Assert.Equal(-1, hollowZeroMapNode2.PathStepCnt);
		Assert.Equal(-1, hollowZeroMapNode3.PathStepCnt);
		Assert.Null(new HollowRouteSelector().SelectNextNode(hollowZeroMap));
	}
}
