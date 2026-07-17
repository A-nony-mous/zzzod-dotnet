using System;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LostVoidFixedAssetTests
{
	[Fact]
	public void RealGameFixtures_UseProductionScreenContextAndTemplatesForWorldRecognition()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = FindWorkspaceRoot();
		string text2 = CreateRunRoot(text);
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text2, text));
			zContext.ScreenContext.Reload();
			using Mat screen = LoadFixture("lost_void-initial.png");
			using Mat screen2 = LoadFixture("lost_void-before.png");
			using Mat screen3 = LoadFixture("lost_void-after.png");
			string matchScreenName = ScreenUtils.GetMatchScreenName(zContext, screen, new string[3] { "大世界-普通", "迷失之地-入口", "迷失之地-大世界" });
			string matchScreenName2 = ScreenUtils.GetMatchScreenName(zContext, screen2, new string[3] { "大世界-普通", "迷失之地-入口", "迷失之地-大世界" });
			Assert.Equal("大世界-普通", matchScreenName);
			Assert.Equal("大世界-普通", matchScreenName2);
			Assert.True(LostVoidMoveByDetectionService.Instance.IsInNormalWorld(zContext, screen3));
			Assert.Equal(FindAreaResultEnum.True, ScreenUtils.FindArea(zContext, screen3, "迷失之地-大世界", "迷失之地-TAB"));
		}
		finally
		{
			Directory.Delete(text2, recursive: true);
		}
	}

	[Fact]
	public void RealGameFixture_LostVoidAfterRunsProductionYoloModel()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = FindWorkspaceRoot();
		string text2 = CreateRunRoot(text);
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text2, text));
			zContext.LostVoid.InitLostVoidDetectorModel();
			LostVoidDetector lostVoidDetector = Assert.IsType<LostVoidDetector>(zContext.LostVoid.Detector);
			Assert.True(lostVoidDetector.InitModel());
			using Mat image = LoadFixture("lost_void-after.png");
			YoloDetectFrameResult yoloDetectFrameResult = lostVoidDetector.CoreDetector.Run(image, 0.6f, 0.5f, 0.0);
			Assert.Collection(yoloDetectFrameResult.Results, delegate(YoloDetectObjectResult distance)
			{
				Assert.Equal("0001-距离", distance.DetectClass.ClassName);
				Assert.Equal((1159, 307, 1201, 349), (distance.X1, distance.Y1, distance.X2, distance.Y2));
			}, delegate(YoloDetectObjectResult shopRight)
			{
				Assert.Equal("0010-邦布商店", shopRight.DetectClass.ClassName);
				Assert.Equal((1368, 284, 1414, 328), (shopRight.X1, shopRight.Y1, shopRight.X2, shopRight.Y2));
			}, delegate(YoloDetectObjectResult shopLeft)
			{
				Assert.Equal("0010-邦布商店", shopLeft.DetectClass.ClassName);
				Assert.Equal((1044, 287, 1089, 330), (shopLeft.X1, shopLeft.Y1, shopLeft.X2, shopLeft.Y2));
			});
		}
		finally
		{
			Directory.Delete(text2, recursive: true);
		}
	}

	private static Mat LoadFixture(string fileName)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "TestData", "LostVoid", fileName);
		Mat mat = Cv2.ImRead(text);
		Assert.False(mat.Empty(), "无法读取固定资产 " + text);
		return mat;
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 工作区根目录。");
	}

	private static string CreateRunRoot(string workspaceRoot)
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-lost-void-fixed-assets", Guid.NewGuid().ToString("N"));
		CopyDirectory(Path.Combine(workspaceRoot, "config"), Path.Combine(text, "config"));
		return text;
	}

	private static void CopyDirectory(string sourceDirectory, string targetDirectory)
	{
		foreach (string item in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(sourceDirectory, item);
			string text = Path.Combine(targetDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}
}
