using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using CoreOperation = OneDragon.Core.Operations.Operation;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// 锄大地道路匹配的地图边缘行为。
/// </summary>
/// <remarks>
/// 对齐 Python：搜索窗/候选区域钳制后小于小地图模板时，Python 侧 <c>cv2.matchTemplate</c>
/// （world_patrol_service.py:596-609）或 <c>cv2.bitwise_and</c>（:566-577）会抛错，
/// 经框架轮循环转成状态 <c>异常</c> 的重试；.NET 原先分别是"返回 null 当作本帧无坐标"和"静默跳过候选"。
/// </remarks>
public sealed class WorldPatrolMapEdgeTests
{
	/// <summary>
	/// 搜索窗越出大地图、钳制后小于小地图模板时应抛异常，而不是返回 null 静默降级。
	/// </summary>
	[Fact]
	public void CalculateCurrentPositionByRoad_ThrowsWhenSearchWindowFallsOffTheMap()
	{
		using Mat largeRoadMask = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
		using Mat miniRoadMask = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
		using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap("test_area", "road_mask.png", Array.Empty<WorldPatrolLargeMapIcon>(), largeRoadMask.Clone());
		WorldPatrolMiniMapSnapshot miniMap = CreateMiniMap(miniRoadMask);

		// 钳制后只剩 20x20，小于 50x50 的模板。
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
			WorldPatrolService.CalculateCurrentPositionByRoad(largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Rect(-30, -30, 20, 20)));

		Assert.Contains("裁剪区超出大地图边界", exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("重启当前路线", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// 搜索窗在范围内时行为不变：正常返回匹配坐标。
	/// </summary>
	[Fact]
	public void CalculateCurrentPositionByRoad_KeepsWorkingInsideTheMap()
	{
		using Mat miniRoadMask = CreatePattern(50, 50);
		using Mat largeRoadMask = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
		miniRoadMask.CopyTo(new Mat(largeRoadMask, new OpenCvSharp.Rect(60, 70, miniRoadMask.Cols, miniRoadMask.Rows)));
		using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap("test_area", "road_mask.png", Array.Empty<WorldPatrolLargeMapIcon>(), largeRoadMask.Clone());
		WorldPatrolMiniMapSnapshot miniMap = CreateMiniMap(miniRoadMask);

		WorldPatrolPoint? actual = WorldPatrolService.CalculateCurrentPositionByRoad(largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 200, 200));

		Assert.Equal(new WorldPatrolPoint(85, 95), actual);
	}

	/// <summary>
	/// 图标定位消歧时，候选区域越出道路掩码边界应抛异常，而不是静默跳过该候选继续挑选。
	/// </summary>
	[Fact]
	public void SelectBestByRoadMask_ThrowsWhenCandidateFallsOutOfBounds()
	{
		string rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zzzod-map-edge-tests", Guid.NewGuid().ToString("N"));
		System.IO.Directory.CreateDirectory(rootDirectory);
		try
		{
			WriteMapIconTemplate(rootDirectory, "icon_a");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using Mat miniRgb = new Mat(50, 50, MatType.CV_8UC3, Scalar.Black);
			using Mat miniRoadMask = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
			using Mat iconTemplate = CreateIconTemplate();
			iconTemplate.CopyTo(new Mat(miniRgb, new OpenCvSharp.Rect(14, 19, iconTemplate.Cols, iconTemplate.Rows)));
			using Mat largeRoadMask = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);

			// 两个同模板图标 → 两个等置信度候选，走 SelectBestByRoadMask 消歧。
			// 第二个图标贴近原点，其候选左上角为负，裁剪后小于 50x50 模板。
			using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap(
				"test_area",
				"road_mask.png",
				new WorldPatrolLargeMapIcon[]
				{
					WorldPatrolLargeMapIcon.Create("图标A", "icon_a", new WorldPatrolPoint(100, 120)),
					WorldPatrolLargeMapIcon.Create("图标B", "icon_a", new WorldPatrolPoint(5, 5)),
				},
				largeRoadMask.Clone());
			WorldPatrolMiniMapSnapshot miniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0, 50, miniRoadMask.Clone(), miniRgb.Clone());

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
				context.WorldPatrolService.CalculateCurrentPosition(context, largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 200, 200)));

			Assert.Contains("裁剪区超出大地图边界", exception.Message, StringComparison.Ordinal);
		}
		finally
		{
			System.IO.Directory.Delete(rootDirectory, recursive: true);
		}
	}

	/// <summary>
	/// 轮内异常经框架转成状态 `异常` 的重试，额度耗尽后节点失败；
	/// 失败状态不含"重启当前路线"，因此不会触发 WorldPatrolAppOperation 的路线级重试。
	/// </summary>
	[Fact]
	public async Task MapEdgeException_EndsWithExceptionStatusThatDoesNotTriggerRouteRestart()
	{
		string rootDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zzzod-map-edge-tests", Guid.NewGuid().ToString("N"));
		System.IO.Directory.CreateDirectory(rootDirectory);
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			ThrowingRoadMatchOperation operation = new ThrowingRoadMatchOperation(context);

			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(10));

			Assert.False(result.IsSuccess);
			Assert.Equal(CoreOperation.RoundExceptionStatus, result.Status);
			// WorldPatrolAppOperation.IsRestartRouteStatus 只匹配"重启当前路线"子串，`异常` 不会命中。
			Assert.DoesNotContain("重启当前路线", result.Status, StringComparison.Ordinal);
			// 节点重试额度耗尽后才失败，说明异常没有直接炸掉整个 operation。
			Assert.True(operation.RoundCount > 1, $"实际只跑了 {operation.RoundCount} 轮，异常应先消耗重试额度");
		}
		finally
		{
			System.IO.Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static WorldPatrolMiniMapSnapshot CreateMiniMap(Mat roadMask) =>
		new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0, roadMask.Cols, roadMask.Clone(), new Mat(roadMask.Rows, roadMask.Cols, MatType.CV_8UC3, Scalar.Black));

	private static void WriteMapIconTemplate(string rootDirectory, string templateId)
	{
		string directory = System.IO.Path.Combine(rootDirectory, "assets", "template", "map", templateId);
		System.IO.Directory.CreateDirectory(directory);
		using Mat template = CreateIconTemplate();
		Cv2.ImWrite(System.IO.Path.Combine(directory, "raw.png"), template);
	}

	private static Mat CreateIconTemplate()
	{
		Mat mat = new Mat(10, 10, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(1, 1, 4, 4), new Scalar(255.0, 255.0, 255.0), -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(7, 7), 2, new Scalar(20.0, 120.0, 240.0), -1);
		return mat;
	}

	private static Mat CreatePattern(int width, int height)
	{
		Mat mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
		for (int row = 0; row < height; row++)
		{
			for (int col = 0; col < width; col++)
			{
				mat.Set(row, col, (byte)((col * 17 + row * 23 + 31) % 251));
			}
		}
		return mat;
	}

	/// <summary>
	/// 节点每轮都触发地图边缘异常，用来验收异常经框架转重试后的最终失败形态。
	/// </summary>
	private sealed class ThrowingRoadMatchOperation(ZContext context) : ZOperation(context, "锄大地边缘异常")
	{
		public int RoundCount { get; private set; }

		[OperationNode("运行路线", IsStartNode = true, ScreenshotBeforeRound = false, NodeMaxRetryTimes = 2)]
		private OperationRoundResult RunRoute()
		{
			RoundCount++;
			using Mat largeRoadMask = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
			using Mat miniRoadMask = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
			using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap("test_area", "road_mask.png", Array.Empty<WorldPatrolLargeMapIcon>(), largeRoadMask.Clone());
			WorldPatrolMiniMapSnapshot miniMap = CreateMiniMap(miniRoadMask);
			WorldPatrolService.CalculateCurrentPositionByRoad(largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Rect(-30, -30, 20, 20));
			return RoundSuccess("不应到达");
		}
	}
}
