using System;
using System.IO;
using System.Runtime.CompilerServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class WorldPatrolMiniMapTests
{
	[Fact]
	public void MiniMapWrapper_BuildsCircleRoadAndPlayerMasks()
	{
		using Mat mat = new Mat(240, 240, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(95, 105, 28, 18), new Scalar(20.0, 20.0, 20.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(145, 105, 28, 18), new Scalar(40.0, 0.0, 0.0), -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(70, 70), 10, new Scalar(255.0, 160.0, 0.0), -1);
		using WorldPatrolMiniMapWrapper worldPatrolMiniMapWrapper = new WorldPatrolMiniMapWrapper(mat);
		Assert.Equal(255, worldPatrolMiniMapWrapper.CircleMask.At<byte>(120, 120));
		Assert.Equal(0, worldPatrolMiniMapWrapper.CircleMask.At<byte>(189, 207));
		Assert.True(worldPatrolMiniMapWrapper.PlayMaskFound);
		Assert.True(Cv2.CountNonZero(worldPatrolMiniMapWrapper.PlayerMask) > 50);
		Assert.Equal(255, worldPatrolMiniMapWrapper.RoadMask.At<byte>(112, 108));
		Assert.Equal(0, worldPatrolMiniMapWrapper.RoadMask.At<byte>(112, 158));
	}

	[Fact]
	public void MiniMapWrapper_NonSquareCropKeepsPythonMaskDimensions()
	{
		using Mat mat = new Mat(120, 160, MatType.CV_8UC3, Scalar.Black);
		using WorldPatrolMiniMapWrapper worldPatrolMiniMapWrapper = new WorldPatrolMiniMapWrapper(mat);
		Assert.Equal(mat.Size(), worldPatrolMiniMapWrapper.CircleMask.Size());
		Assert.Equal(mat.Size(), worldPatrolMiniMapWrapper.RoadMask.Size());
	}

	[Fact]
	public void MiniMapWrapper_RecognizesPlayerAfterBgrScreenshotConversion()
	{
		using Mat mat = new Mat(240, 240, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		Cv2.Circle(mat, new OpenCvSharp.Point(70, 70), 10, new Scalar(0.0, 160.0, 255.0), -1);
		using Mat rgb = WorldPatrolMiniMapWrapper.ConvertBgrToRgb(mat);
		using WorldPatrolMiniMapWrapper worldPatrolMiniMapWrapper = new WorldPatrolMiniMapWrapper(rgb);
		Assert.True(worldPatrolMiniMapWrapper.PlayMaskFound);
		Assert.True(Cv2.CountNonZero(worldPatrolMiniMapWrapper.PlayerMask) > 50);
	}

	[Fact]
	public void AngleCalculator_ReturnsDirectionFromViewMaskPixels()
	{
		using Mat mat = new Mat(120, 120, MatType.CV_8UC1, Scalar.Black);
		Cv2.Ellipse(mat, new OpenCvSharp.Point(60, 60), new Size(55, 55), 0.0, 127.0, 232.0, Scalar.White, -1);
		double? value = WorldPatrolMiniMapAngleCalculator.Calculate(mat);
		Assert.NotNull(value);
		Assert.InRange(value.Value, 175.0, 185.0);
	}

	[Fact]
	public void AngleCalculator_ReturnsNullWhenMaskHasNoProminentSector()
	{
		using Mat viewMask = new Mat(120, 120, MatType.CV_8UC1, Scalar.Black);
		Assert.Null(WorldPatrolMiniMapAngleCalculator.Calculate(viewMask));
	}

	[Fact]
	public void CalPos_MatchesTemplateAndAddsCropOffset()
	{
		using Mat mat = CreatePattern(12, 10);
		using Mat mat2 = new Mat(80, 80, MatType.CV_8UC1, Scalar.Black);
		mat.CopyTo(new Mat(mat2, new OpenCvSharp.Rect(30, 24, mat.Cols, mat.Rows)));
		MatchResult matchResult = WorldPatrolCalPosUtils.CalPos(mat2, mat, new WorldPatrolPoint(36, 29));
		WorldPatrolPoint? actual = WorldPatrolCalPosUtils.CalCurrentPosition(mat2, mat, new WorldPatrolPoint(36, 29));
		Assert.NotNull(matchResult);
		Assert.Equal(30, matchResult.X);
		Assert.Equal(24, matchResult.Y);
		Assert.Equal(new WorldPatrolPoint(36, 29), actual);
	}

	[Fact]
	public void Service_CalculatesCurrentPositionByIconBeforeRoadMaskFallback()
	{
		string text = CreateTempRoot();
		try
		{
			WriteMapIconTemplate(text, "icon_a");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			using Mat mat = new Mat(50, 50, MatType.CV_8UC3, Scalar.Black);
			using Mat mat2 = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
			using Mat mat3 = CreateIconTemplate();
			mat3.CopyTo(new Mat(mat, new OpenCvSharp.Rect(14, 19, mat3.Cols, mat3.Rows)));
			using Mat mat4 = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
			WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap("test_area", "road_mask.png", new WorldPatrolLargeMapIcon[] { WorldPatrolLargeMapIcon.Create("图标A", "icon_a", new WorldPatrolPoint(100, 120)) }, mat4.Clone());
			WorldPatrolMiniMapSnapshot miniMap = new WorldPatrolMiniMapSnapshot(PlayMaskFound: true, 0.0, 50, mat2.Clone(), mat.Clone());
			WorldPatrolPoint? actual = zContext.WorldPatrolService.CalculateCurrentPosition(zContext, largeMap, miniMap, new OneDragon.Core.Abstractions.Geometry.Rect(0, 0, 200, 200));
			Assert.Equal(new WorldPatrolPoint(106, 121), actual);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static Mat CreatePattern(int width, int height)
	{
		Mat mat = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				mat.Set(i, j, (byte)((j * 17 + i * 23 + 31) % 251));
			}
		}
		return mat;
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteMapIconTemplate(string rootDirectory, string templateId)
	{
		string[] buffer = new string[5];
		buffer[0] = rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "template";
		buffer[3] = "map";
		buffer[4] = templateId;
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		using Mat img = CreateIconTemplate();
		Cv2.ImWrite(Path.Combine(text, "raw.png"), img);
	}

	private static Mat CreateIconTemplate()
	{
		Mat mat = new Mat(10, 10, MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(1, 1, 4, 4), new Scalar(255.0, 255.0, 255.0), -1);
		Cv2.Circle(mat, new OpenCvSharp.Point(7, 7), 2, new Scalar(20.0, 120.0, 240.0), -1);
		return mat;
	}
}
