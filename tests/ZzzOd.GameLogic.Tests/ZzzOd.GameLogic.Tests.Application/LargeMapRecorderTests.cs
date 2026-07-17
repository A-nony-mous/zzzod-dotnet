using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LargeMapRecorderTests
{
	private sealed class RecordingRemoteMapImageClient(byte[] bytes) : IRemoteMapImageClient
	{
		public int RequestCount { get; private set; }

		public Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken)
		{
			RequestCount++;
			return Task.FromResult(bytes);
		}
	}

	[Fact]
	public void MergeLargeMap_InitializesThreeByThreeMapAndOffsetsIcons()
	{
		using MiniMapSnapshot miniMap = CreateMiniMap(new OneDragon.Core.Abstractions.Geometry.Point(2, 3));
		using LargeMapSnapshot largeMapSnapshot = LargeMapRecorderUtils.MergeLargeMap(null, miniMap, null);
		Assert.Equal(12, largeMapSnapshot.RoadMask.Rows);
		Assert.Equal(12, largeMapSnapshot.RoadMask.Cols);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(6, 6), largeMapSnapshot.PositionAfterMerge);
		LargeMapIcon largeMapIcon = Assert.Single(largeMapSnapshot.IconList);
		Assert.Equal("map_icon_01", largeMapIcon.TemplateId);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(6, 7), largeMapIcon.LargeMapPosition);
	}

	[Fact]
	public void MergeMiniMap_CombinesRoadMaskAndDeduplicatesCloseIcons()
	{
		using Mat mat = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
		using Mat mat2 = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
		mat.Set(1, 1, 255);
		mat2.Set(2, 2, 255);
		using MiniMapSnapshot merge = new MiniMapSnapshot(mat.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(1, 1)) });
		using MiniMapSnapshot newMiniMap = new MiniMapSnapshot(mat2.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(3, 3)) });
		using MiniMapSnapshot miniMapSnapshot = LargeMapRecorderUtils.MergeMiniMap(merge, newMiniMap);
		Assert.Equal(255, miniMapSnapshot.RoadMask.At<byte>(1, 1));
		Assert.Equal(255, miniMapSnapshot.RoadMask.At<byte>(2, 2));
		Assert.Single(miniMapSnapshot.IconList);
	}

	[Fact]
	public void GetMiniMapInCircle_FillsSmallBlackDotsAndRemovesSmallWhiteNoiseLikePython()
	{
		using Mat mat = new Mat(40, 40, MatType.CV_8UC1, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(8, 8, 16, 16), Scalar.White, -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(12, 12, 3, 3), Scalar.Black, -1);
		mat.Set(20, 32, 255);
		using MiniMapSnapshot miniMap = new MiniMapSnapshot(mat.Clone(), Array.Empty<MiniMapIcon>());
		using MiniMapSnapshot miniMapSnapshot = LargeMapRecorderUtils.GetMiniMapInCircle(miniMap);
		Assert.Equal(255, miniMapSnapshot.RoadMask.At<byte>(13, 13));
		Assert.Equal(0, miniMapSnapshot.RoadMask.At<byte>(20, 32));
	}

	[Fact]
	public void GetMiniMapCircleMask_AppliesPythonFixedPlayMaskWhenCenterIsOutsideImage()
	{
		using Mat mat = LargeMapRecorderUtils.GetMiniMapCircleMask(207);
		Assert.Equal(0, mat.At<byte>(189, 180));
	}

	[Fact]
	public void CreateMiniMapSnapshot_ScansContinuousMapIconsWithTemplateMaskAndPythonThreshold()
	{
		string text = CreateTempRoot();
		try
		{
			using Mat mat = CreateIcon(new Scalar(20.0, 80.0, 150.0));
			using Mat mat2 = CreateIcon(new Scalar(120.0, 40.0, 200.0));
			using Mat mask = new Mat(3, 3, MatType.CV_8UC1, Scalar.White);
			WriteMapTemplate(text, "map_icon_01", mat, mask);
			WriteMapTemplate(text, "map_icon_03", mat2, mask);
			using Mat mat3 = new Mat(12, 12, MatType.CV_8UC3, Scalar.Black);
			mat.CopyTo(new Mat(mat3, new OpenCvSharp.Rect(4, 5, mat.Cols, mat.Rows)));
			mat2.CopyTo(new Mat(mat3, new OpenCvSharp.Rect(1, 1, mat2.Cols, mat2.Rows)));
			using Mat mat4 = new Mat(12, 12, MatType.CV_8UC1, Scalar.Black);
			using TemplateLoader templateLoader = new TemplateLoader(new OneDragonEnvironment(text));
			TemplateMatcher templateMatcher = new TemplateMatcher(templateLoader);
			using MiniMapSnapshot miniMapSnapshot = LargeMapRecorderUtils.CreateMiniMapSnapshot(templateMatcher, mat3, mat4);
			MiniMapIcon miniMapIcon = Assert.Single(miniMapSnapshot.IconList);
			Assert.Equal("map_icon_01", miniMapIcon.TemplateId);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(5, 6), miniMapIcon.Position);
			Assert.Same(mat4, miniMapSnapshot.RoadMask);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void MapDisplay_RendersTemplateRawOnlyWhereMaskAllows()
	{
		string text = CreateTempRoot();
		try
		{
			using Mat mat = new Mat(3, 3, MatType.CV_8UC3, new Scalar(80.0, 120.0, 200.0));
			using Mat mat2 = new Mat(3, 3, MatType.CV_8UC1, Scalar.Black);
			mat2.Set(1, 1, 255);
			WriteMapTemplate(text, "map_icon_01", mat, mat2);
			using Mat mat3 = new Mat(12, 12, MatType.CV_8UC1, Scalar.Black);
			using MiniMapSnapshot miniMap = new MiniMapSnapshot(mat3.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(5, 6)) });
			using TemplateLoader templateLoader = new TemplateLoader(new OneDragonEnvironment(text));
			using Mat mat4 = LargeMapRecorderUtils.GetMiniMapDisplay(templateLoader, miniMap);
			using LargeMapSnapshot largeMap = new LargeMapSnapshot("area", mat3.Clone(), new LargeMapIcon[] { new LargeMapIcon(string.Empty, "map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(5, 6)) }, new OneDragon.Core.Abstractions.Geometry.Point(5, 6));
			using Mat mat5 = LargeMapRecorderUtils.GetLargeMapDisplay(templateLoader, largeMap);
			Assert.Equal(mat.At<Vec3b>(1, 1), mat4.At<Vec3b>(6, 5));
			Assert.Equal(new Vec3b(0, 0, 0), mat4.At<Vec3b>(5, 4));
			Assert.Equal(mat.At<Vec3b>(1, 1), mat5.At<Vec3b>(6, 5));
			Assert.Null(LargeMapRecorderUtils.GetLargeMapDisplay(templateLoader, null));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CalculatePosition_UsesRoadMaskWhenIconLocationIsUnavailable()
	{
		using Mat mat = new Mat(24, 24, MatType.CV_8UC1, Scalar.Black);
		using Mat mat2 = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
		mat2.Set(0, 0, 255);
		mat2.Set(1, 2, 255);
		mat2.Set(3, 1, 255);
		mat2.CopyTo(new Mat(mat, new OpenCvSharp.Rect(10, 11, mat2.Cols, mat2.Rows)));
		using LargeMapSnapshot largeMap = new LargeMapSnapshot("area", mat.Clone(), Array.Empty<LargeMapIcon>(), new OneDragon.Core.Abstractions.Geometry.Point(12, 13));
		using MiniMapSnapshot miniMap = new MiniMapSnapshot(mat2.Clone(), Array.Empty<MiniMapIcon>());
		MatchResult matchResult = LargeMapRecorderUtils.CalculatePosition(largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Point(12, 13), useIcon: true);
		Assert.NotNull(matchResult);
		Assert.Equal(10, matchResult.X);
		Assert.Equal(11, matchResult.Y);
	}

	[Fact]
	public void MergeLargeMap_ReturnsCloneWhenPositionMissing()
	{
		using MiniMapSnapshot miniMap = CreateMiniMap(new OneDragon.Core.Abstractions.Geometry.Point(2, 3));
		using LargeMapSnapshot largeMapSnapshot = LargeMapRecorderUtils.MergeLargeMap(null, miniMap, null);
		using LargeMapSnapshot largeMapSnapshot2 = LargeMapRecorderUtils.MergeLargeMap(largeMapSnapshot, miniMap, null);
		Assert.NotSame(largeMapSnapshot.RoadMask, largeMapSnapshot2.RoadMask);
		Assert.Equal(largeMapSnapshot.RoadMask.Rows, largeMapSnapshot2.RoadMask.Rows);
		Assert.Equal(largeMapSnapshot.IconList.Count, largeMapSnapshot2.IconList.Count);
	}

	[Fact]
	public void CalculatePositionByIcon_ReturnsMostLikelyTopLeft()
	{
		using Mat mat = new Mat(20, 20, MatType.CV_8UC1, Scalar.Black);
		using Mat mat2 = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
		LargeMapSnapshot largeMapSnapshot = new LargeMapSnapshot("area", mat.Clone(), new LargeMapIcon[] { new LargeMapIcon(string.Empty, "map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(12, 13)) }, new OneDragon.Core.Abstractions.Geometry.Point(10, 10));
		using MiniMapSnapshot miniMap = new MiniMapSnapshot(mat2.Clone(), new MiniMapIcon[] { new MiniMapIcon("map_icon_01", new OneDragon.Core.Abstractions.Geometry.Point(2, 3)) });
		MatchResult matchResult = LargeMapRecorderUtils.CalculatePositionByIcon(largeMapSnapshot, miniMap, new OneDragon.Core.Abstractions.Geometry.Point(10, 10));
		Assert.NotNull(matchResult);
		Assert.Equal(10, matchResult.X);
		Assert.Equal(10, matchResult.Y);
		largeMapSnapshot.Dispose();
	}

	[Fact]
	public void MapIconExtractor_CropsByColorRangesAndSavesTemplateFiles()
	{
		string text = CreateTempRoot();
		try
		{
			using Mat img = new Mat(6, 6, MatType.CV_8UC3, Scalar.Black);
			Cv2.Rectangle(img, new OpenCvSharp.Rect(2, 1, 2, 3), new Scalar(20.0, 120.0, 180.0), -1);
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "assets";
			buffer[2] = "template";
			buffer[3] = "map";
			buffer[4] = "map_icon_test";
			string text2 = Path.Combine(buffer);
			Directory.CreateDirectory(text2);
			Cv2.ImWrite(Path.Combine(text2, "raw.png"), img);
			MapIconExtractor mapIconExtractor = new MapIconExtractor(new OneDragonEnvironment(text));
			using MapIconExtractionResult mapIconExtractionResult = mapIconExtractor.ExtractAndSave("map_icon_test", new (Scalar, Scalar)[] { (new Scalar(10.0, 100.0, 160.0), new Scalar(30.0, 140.0, 200.0)) });
			Assert.Equal(4, mapIconExtractionResult.Raw.Rows);
			Assert.Equal(3, mapIconExtractionResult.Raw.Cols);
			Assert.Equal(4, mapIconExtractionResult.Mask.Rows);
			Assert.Equal(3, mapIconExtractionResult.Mask.Cols);
			string[] buffer2 = new string[6];
			buffer2[0] = text;
			buffer2[1] = "assets";
			buffer2[2] = "template";
			buffer2[3] = "map";
			buffer2[4] = "map_icon_test";
			buffer2[5] = "raw.png";
			Assert.True(File.Exists(Path.Combine(buffer2)));
			string[] buffer3 = new string[6];
			buffer3[0] = text;
			buffer3[1] = "assets";
			buffer3[2] = "template";
			buffer3[3] = "map";
			buffer3[4] = "map_icon_test";
			buffer3[5] = "mask.png";
			Assert.True(File.Exists(Path.Combine(buffer3)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task LargeMapDownloader_CachesDownloadedImageUnderDebugDirectory()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using Mat source = new Mat(4, 4, MatType.CV_8UC3, new Scalar(210.0, 210.0, 210.0));
			Cv2.Rectangle(source, new OpenCvSharp.Rect(1, 1, 2, 2), Scalar.Black, -1);
			Cv2.ImEncode(".png", source, out byte[] bytes);
			RecordingRemoteMapImageClient client = new RecordingRemoteMapImageClient(bytes);
			LargeMapDownloader downloader = new LargeMapDownloader(new OneDragonEnvironment(rootDirectory), client);
			using Mat first = await downloader.GetAreaMapImageAsync("HKC_ZYZZQ_DLDC");
			using Mat second = await downloader.GetAreaMapImageAsync("HKC_ZYZZQ_DLDC");
			Assert.Equal(1, client.RequestCount);
			Assert.True(File.Exists(downloader.GetAreaMapImagePath("HKC_ZYZZQ_DLDC", 120)));
			Assert.True(first.Rows > source.Rows);
			Assert.Equal(first.Rows, second.Rows);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static MiniMapSnapshot CreateMiniMap(OneDragon.Core.Abstractions.Geometry.Point iconPoint)
	{
		Mat mat = new Mat(4, 4, MatType.CV_8UC1, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(1, 1, 2, 2), Scalar.White, -1);
		return new MiniMapSnapshot(mat, new MiniMapIcon[] { new MiniMapIcon("map_icon_01", iconPoint) });
	}

	private static Mat CreateIcon(Scalar color)
	{
		Mat mat = new Mat(3, 3, MatType.CV_8UC3, Scalar.Black);
		mat.Set(0, 0, new Vec3b((byte)color.Val0, (byte)color.Val1, (byte)color.Val2));
		mat.Set(1, 1, new Vec3b((byte)(color.Val0 / 2.0), (byte)(color.Val1 / 2.0), (byte)(color.Val2 / 2.0)));
		mat.Set(2, 2, new Vec3b((byte)(255.0 - color.Val0), (byte)(255.0 - color.Val1), (byte)(255.0 - color.Val2)));
		return mat;
	}

	private static void WriteMapTemplate(string rootDirectory, string templateId, Mat raw, Mat mask)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "map";
		buffer[4] = templateId;
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		Cv2.ImWrite(Path.Combine(text, "raw.png"), raw);
		Cv2.ImWrite(Path.Combine(text, "mask.png"), mask);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
