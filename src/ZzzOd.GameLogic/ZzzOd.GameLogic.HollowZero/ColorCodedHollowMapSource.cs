using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OpenCvSharp;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

public sealed class ColorCodedHollowMapSource : IHollowMapSource
{
	private readonly Func<Mat?> _screenProvider;

	public ColorCodedHollowMapSource(Func<Mat?> screenProvider)
	{
		_screenProvider = screenProvider ?? throw new ArgumentNullException("screenProvider");
	}

	public Task<HollowZeroMap?> DetectMapAsync(HollowEventDetection? detection, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Mat mat = detection?.Screen;
		if (mat != null)
		{
			return Task.FromResult(Parse(mat));
		}
		using Mat mat2 = _screenProvider();
		return Task.FromResult((mat2 == null) ? null : Parse(mat2));
	}

	public static HollowZeroMap? Parse(Mat screen, int maxAdjacentDistance = 140)
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		List<(HollowZeroMapNode, bool)> list = new List<(HollowZeroMapNode, bool)>();
		list.AddRange(FindNodes(screen, new Scalar(0.0, 0.0, 200.0), new Scalar(80.0, 80.0, 255.0), "0000-当前", isCurrent: true));
		list.AddRange(FindNodes(screen, new Scalar(0.0, 200.0, 0.0), new Scalar(80.0, 255.0, 80.0), "0001-目标", isCurrent: false));
		if (list.Count == 0)
		{
			return null;
		}
		int? currentIdx = null;
		List<HollowZeroMapNode> list2 = new List<HollowZeroMapNode>();
		for (int i = 0; i < list.Count; i++)
		{
			list2.Add(list[i].Item1);
			if (list[i].Item2)
			{
				int valueOrDefault = currentIdx.GetValueOrDefault();
				if (!currentIdx.HasValue)
				{
					valueOrDefault = i;
					currentIdx = valueOrDefault;
				}
			}
		}
		return new HollowZeroMap(list2, currentIdx, BuildEdges(list2, maxAdjacentDistance));
	}

	private static IEnumerable<(HollowZeroMapNode Node, bool IsCurrent)> FindNodes(Mat screen, Scalar lower, Scalar upper, string entryName, bool isCurrent)
	{
		using Mat mask = new Mat();
		Cv2.InRange(screen, lower, upper, mask);
		Cv2.FindContours(mask, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		OpenCvSharp.Point[][] array = contours;
		foreach (OpenCvSharp.Point[] contour in array)
		{
			OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);
			if (rect.Width >= 5 && rect.Height >= 5)
			{
				OneDragon.Core.Abstractions.Geometry.Rect geometryRect = new OneDragon.Core.Abstractions.Geometry.Rect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
				yield return (Node: new HollowZeroMapNode(geometryRect, new HollowZeroEntry(entryName), null, 1f), IsCurrent: isCurrent);
			}
		}
	}

	private static Dictionary<int, List<int>> BuildEdges(IReadOnlyList<HollowZeroMapNode> nodes, int maxAdjacentDistance)
	{
		Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
		for (int i = 0; i < nodes.Count; i++)
		{
			dictionary[i] = new List<int>();
		}
		for (int j = 0; j < nodes.Count; j++)
		{
			for (int k = j + 1; k < nodes.Count; k++)
			{
				int num = Math.Abs(nodes[j].Pos.Center.X - nodes[k].Pos.Center.X);
				int num2 = Math.Abs(nodes[j].Pos.Center.Y - nodes[k].Pos.Center.Y);
				bool flag = num2 <= 20 && num <= maxAdjacentDistance;
				bool flag2 = num <= 20 && num2 <= maxAdjacentDistance;
				if (flag || flag2)
				{
					dictionary[j].Add(k);
					dictionary[k].Add(j);
				}
			}
		}
		return dictionary.ToDictionary((KeyValuePair<int, List<int>> pair) => pair.Key, (KeyValuePair<int, List<int>> pair) => pair.Value);
	}
}
