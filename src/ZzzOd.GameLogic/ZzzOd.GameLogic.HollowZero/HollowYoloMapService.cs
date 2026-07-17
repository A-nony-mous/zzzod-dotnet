using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.GameData;
using ZzzOd.GameLogic.HollowZero.HollowMap;

namespace ZzzOd.GameLogic.HollowZero;

/// <summary>
/// 将空洞事件 YOLO 的当前帧结果转换为 BaselineParity 同语义的电视地图。
/// </summary>
public static class HollowYoloMapService
{
	private sealed class HollowEntryYaml
	{
		public string EntryName { get; set; } = string.Empty;

		public bool IsBenefit { get; set; } = true;

		public int NeedStep { get; set; } = 1;

		public bool IsBase { get; set; }

		public bool CanGo { get; set; } = true;

		public bool IsTp { get; set; }

		public bool MoveAfterwards { get; set; }

		public int CanVisitedTimes { get; set; } = 2;
	}

	/// <summary>
	/// 使用传入截图及其采集时间识别当前地图。不会重新截图。
	/// </summary>
	public static HollowZeroMap? CalculateCurrentMap(ZContext context, Mat screen, DateTimeOffset screenshotTimeUtc)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		ArgumentNullException.ThrowIfNull(screen, "screen");
		double value = (double)screenshotTimeUtc.ToUnixTimeMilliseconds() / 1000.0;
		YoloDetectFrameResult frame = context.HollowEventDetector.CoreDetector.Run(screen, 0.6f, 0.5f, value);
		return ConstructMap(context, frame);
	}

	/// <summary>
	/// 从一帧 YOLO 结果构建地图，供固定资产回归复用。
	/// </summary>
	public static HollowZeroMap? ConstructMap(ZContext context, YoloDetectFrameResult frame)
	{
		ArgumentNullException.ThrowIfNull(context, "context");
		ArgumentNullException.ThrowIfNull(frame, "frame");
		IReadOnlyDictionary<string, HollowZeroEntry> readOnlyDictionary = LoadEntries(context);
		if (!readOnlyDictionary.TryGetValue("未知", out var value))
		{
			throw new InvalidOperationException("空洞地图数据缺少 未知 入口。");
		}
		List<HollowZeroMapNode> list = new List<HollowZeroMapNode>();
		foreach (YoloDetectObjectResult result in frame.Results)
		{
			string className = result.DetectClass.ClassName;
			string key = ((className.Length > 5) ? className.Substring(5) : className);
			HollowZeroEntry value2;
			HollowZeroEntry hollowZeroEntry = (readOnlyDictionary.TryGetValue(key, out value2) ? Clone(value2) : Clone(value));
			int width = result.Width;
			int height = result.Height;
			if (width > 0 && height > 0)
			{
				OneDragon.Core.Abstractions.Geometry.Rect pos = (hollowZeroEntry.IsBase ? new OneDragon.Core.Abstractions.Geometry.Rect(result.X1, result.Y2 - width, result.X2, result.Y2) : new OneDragon.Core.Abstractions.Geometry.Rect(result.X1, result.Y1, result.X2, result.Y2 + height / 3));
				HollowZeroMapNode hollowZeroMapNode = list.FirstOrDefault((HollowZeroMapNode existing) => IsSameNodePos(existing.Pos, pos));
				if (hollowZeroMapNode == null)
				{
					list.Add(new HollowZeroMapNode(pos, hollowZeroEntry, (float)frame.RunTime, (float)result.Score));
				}
				else if (hollowZeroMapNode.Entry.IsBase && !hollowZeroEntry.IsBase)
				{
					hollowZeroMapNode.Entry = hollowZeroEntry;
					hollowZeroMapNode.Pos = new OneDragon.Core.Abstractions.Geometry.Rect(hollowZeroMapNode.Pos.X1, pos.Y1, hollowZeroMapNode.Pos.X2, hollowZeroMapNode.Pos.Y2);
				}
				else if (!hollowZeroMapNode.Entry.IsBase && hollowZeroEntry.IsBase)
				{
					hollowZeroMapNode.Pos = new OneDragon.Core.Abstractions.Geometry.Rect(pos.X1, hollowZeroMapNode.Pos.Y1, pos.X2, pos.Y2);
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		foreach (HollowZeroMapNode item in list.Where((HollowZeroMapNode node) => node.Entry.IsBase))
		{
			item.Entry = Clone(value);
		}
		return ConstructMapFromNodes(context, list, (float)frame.RunTime);
	}

	private static HollowZeroMap ConstructMapFromNodes(ZContext context, List<HollowZeroMapNode> nodes, float checkTime)
	{
		int? currentIdx = null;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].Entry.EntryName != "当前")
			{
				continue;
			}
			if (currentIdx.HasValue)
			{
				int valueOrDefault = currentIdx.GetValueOrDefault();
				if (true)
				{
					if (nodes[valueOrDefault].CheckTime < nodes[i].CheckTime)
					{
						nodes[valueOrDefault].Entry = LoadEntries(context)["未知"];
						nodes[valueOrDefault].Confidence = 0f;
						currentIdx = i;
					}
					continue;
				}
			}
			currentIdx = i;
		}
		Dictionary<int, List<int>> edges = new Dictionary<int, List<int>>();
		int screenStandardWidth = context.ProjectConfig.ScreenStandardWidth;
		int screenStandardHeight = context.ProjectConfig.ScreenStandardHeight;
		for (int j = 0; j < nodes.Count; j++)
		{
			if (!IsInScreen(nodes[j], screenStandardWidth, screenStandardHeight))
			{
				continue;
			}
			for (int k = 0; k < nodes.Count; k++)
			{
				if (!IsInScreen(nodes[k], screenStandardWidth, screenStandardHeight) || !nodes[j].Entry.CanGo || !nodes[k].Entry.CanGo)
				{
					continue;
				}
				if (AtLeft(nodes[j], nodes[k]))
				{
					if (nodes[k].Entry.EntryName != "轨道-左" && !IsRailToLeft(nodes[j].Entry.EntryName))
					{
						AddDirectedEdge(edges, j, k);
					}
				}
				else if (AtRight(nodes[j], nodes[k]))
				{
					if (nodes[k].Entry.EntryName != "轨道-右" && !IsRailToRight(nodes[j].Entry.EntryName))
					{
						AddDirectedEdge(edges, j, k);
					}
				}
				else if (Above(nodes[j], nodes[k]))
				{
					if (nodes[k].Entry.EntryName != "轨道-上" && !IsRailToUp(nodes[j].Entry.EntryName))
					{
						AddDirectedEdge(edges, j, k);
					}
				}
				else if (Under(nodes[j], nodes[k]) && nodes[k].Entry.EntryName != "轨道-下" && !IsRailToDown(nodes[j].Entry.EntryName))
				{
					AddDirectedEdge(edges, j, k);
				}
			}
		}
		return new HollowZeroMap(nodes, currentIdx, edges, checkTime);
	}

	private static IReadOnlyDictionary<string, HollowZeroEntry> LoadEntries(ZContext context)
	{
		string text = Path.Combine(GameConst.GetGameDataPath(context.Environment), "hollow_zero", "entry_list.yml");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("未找到空洞地图入口数据。", text);
		}
		IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
		List<HollowEntryYaml> list = deserializer.Deserialize<List<HollowEntryYaml>>(File.ReadAllText(text));
		if (list == null)
		{
			throw new InvalidDataException("空洞地图入口数据为空 " + text);
		}
		return (from item in list
			where !string.IsNullOrWhiteSpace(item.EntryName)
			select new HollowZeroEntry(item.EntryName, item.IsBenefit, item.NeedStep, item.IsBase, item.CanGo, item.IsTp, item.MoveAfterwards, item.CanVisitedTimes)).ToDictionary<HollowZeroEntry, string>((HollowZeroEntry item) => item.EntryName, StringComparer.Ordinal);
	}

	private static HollowZeroEntry Clone(HollowZeroEntry entry)
	{
		return new HollowZeroEntry(entry.EntryId + "-" + entry.EntryName, entry.IsBenefit, entry.NeedStep, entry.IsBase, entry.CanGo, entry.IsTp, entry.MoveAfterwards, entry.CanVisitedTimes);
	}

	private static bool IsSameNodePos(OneDragon.Core.Abstractions.Geometry.Rect left, OneDragon.Core.Abstractions.Geometry.Rect right)
	{
		int num = Math.Min(Math.Min(left.Height, left.Width), Math.Min(right.Height, right.Width)) / 2;
		long num2 = left.Center.X - right.Center.X;
		long num3 = left.Center.Y - right.Center.Y;
		return Math.Sqrt(num2 * num2 + num3 * num3) < (double)num;
	}

	private static bool IsInScreen(HollowZeroMapNode node, int width, int height)
	{
		return node.Pos.X1 >= 0 && node.Pos.Y1 >= 0 && node.Pos.X2 < width && node.Pos.Y2 < height;
	}

	private static bool AtLeft(HollowZeroMapNode left, HollowZeroMapNode right)
	{
		return Math.Abs(left.Pos.X2 - right.Pos.X1) <= Math.Min(left.Pos.Width, right.Pos.Width) / 4 && IsSameRow(left, right);
	}

	private static bool AtRight(HollowZeroMapNode left, HollowZeroMapNode right)
	{
		return Math.Abs(left.Pos.X1 - right.Pos.X2) <= Math.Min(left.Pos.Width, right.Pos.Width) / 4 && IsSameRow(left, right);
	}

	private static bool Above(HollowZeroMapNode above, HollowZeroMapNode under)
	{
		return Math.Abs(above.Pos.Y2 - under.Pos.Y1) <= Math.Min(above.Pos.Height, under.Pos.Height) / 4 && IsSameColumn(above, under);
	}

	private static bool Under(HollowZeroMapNode under, HollowZeroMapNode above)
	{
		return Math.Abs(under.Pos.Y1 - above.Pos.Y2) <= Math.Min(under.Pos.Height, above.Pos.Height) / 4 && IsSameColumn(under, above);
	}

	private static bool IsSameRow(HollowZeroMapNode left, HollowZeroMapNode right)
	{
		int num = Math.Min(left.Pos.Height, right.Pos.Height) / 3;
		return Math.Abs(left.Pos.Y1 - right.Pos.Y1) <= num || Math.Abs(left.Pos.Y2 - right.Pos.Y2) <= num;
	}

	private static bool IsSameColumn(HollowZeroMapNode above, HollowZeroMapNode under)
	{
		int num = Math.Min(above.Pos.Width, under.Pos.Width) / 3;
		return Math.Abs(above.Pos.X1 - under.Pos.X1) <= num || Math.Abs(above.Pos.X2 - under.Pos.X2) <= num;
	}

	private static bool IsRailToLeft(string entryName)
	{
		switch (entryName)
		{
		case "轨道-上":
		case "轨道-下":
		case "轨道-左":
			return true;
		default:
			return false;
		}
	}

	private static bool IsRailToRight(string entryName)
	{
		switch (entryName)
		{
		case "轨道-上":
		case "轨道-下":
		case "轨道-右":
			return true;
		default:
			return false;
		}
	}

	private static bool IsRailToUp(string entryName)
	{
		switch (entryName)
		{
		case "轨道-左":
		case "轨道-右":
		case "轨道-上":
			return true;
		default:
			return false;
		}
	}

	private static bool IsRailToDown(string entryName)
	{
		switch (entryName)
		{
		case "轨道-左":
		case "轨道-右":
		case "轨道-下":
			return true;
		default:
			return false;
		}
	}

	private static void AddDirectedEdge(Dictionary<int, List<int>> edges, int from, int to)
	{
		if (!edges.TryGetValue(from, out List<int> value))
		{
			value = (edges[from] = new List<int>());
		}
		value.Add(to);
	}
}
