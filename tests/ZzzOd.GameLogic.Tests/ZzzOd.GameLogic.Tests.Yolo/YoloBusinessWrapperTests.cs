using System;
using System.IO;
using OneDragon.Core.Runtime;
using OneDragon.Core.Yolo;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Yolo;

namespace ZzzOd.GameLogic.Tests.Yolo;

public sealed class YoloBusinessWrapperTests
{
	[Fact]
	public void FlashClassifier_BuildsCoreClassifierConfigFromZzzModelConfig()
	{
		string text = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config"));
			File.WriteAllText(Path.Combine(text, "config", "model.yml"), "flash_classifier: yolov8n-640-flash-20250921\nflash_classifier_gpu: true");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			FlashClassifier flashClassifier = zContext.FlashClassifier;
			YoloClassifier coreClassifier = flashClassifier.CoreClassifier;
			Assert.Equal("flash_classifier", flashClassifier.ModelCategory);
			Assert.Equal("yolov8n-640-flash-20250921", flashClassifier.ModelName);
			Assert.Equal("yolov8n-640-flash-20250906", flashClassifier.BackupModelName);
			Assert.Equal("https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model", flashClassifier.ModelDownloadUrl);
			Assert.Equal(2.0, flashClassifier.KeepResultSeconds);
			Assert.True(flashClassifier.UseGpu);
			Assert.False(coreClassifier.Config.RequireLabelsFile);
			Assert.Equal(flashClassifier.ModelName, coreClassifier.Config.ModelName);
			Assert.Equal(flashClassifier.BackupModelName, coreClassifier.Config.BackupModelName);
			Assert.Equal(flashClassifier.ModelDirectoryPath, coreClassifier.Config.ModelDirectory);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void HollowEventDetector_BuildsCoreDetectorConfigFromZzzModelConfig()
	{
		string text = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config"));
			File.WriteAllText(Path.Combine(text, "config", "model.yml"), "hollow_zero_event: yolov8s-736-hollow-zero-event-0126\nhollow_zero_event_gpu: true");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			HollowEventDetector hollowEventDetector = zContext.HollowEventDetector;
			YoloDetector coreDetector = hollowEventDetector.CoreDetector;
			Assert.Equal("hollow_zero_event", hollowEventDetector.ModelCategory);
			Assert.Equal("yolov8s-736-hollow-zero-event-0126", hollowEventDetector.ModelName);
			Assert.Equal("yolov8s-736-hollow-zero-event-1130", hollowEventDetector.BackupModelName);
			Assert.Equal("https://github.com/OneDragon-Anything/OneDragon-YOLO/releases/download/zzz_model", hollowEventDetector.ModelDownloadUrl);
			Assert.Equal(2.0, hollowEventDetector.KeepResultSeconds);
			Assert.True(hollowEventDetector.UseGpu);
			Assert.True(coreDetector.Config.RequireLabelsFile);
			Assert.Equal(hollowEventDetector.ModelName, coreDetector.Config.ModelName);
			Assert.Equal(hollowEventDetector.BackupModelName, coreDetector.Config.BackupModelName);
			Assert.Equal(hollowEventDetector.ModelDirectoryPath, coreDetector.Config.ModelDirectory);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ZzzOcrService_UsesResolvedProfileAndShutdownFlag()
	{
		string text = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config"));
			File.WriteAllText(Path.Combine(text, "config", "model.yml"), "ocr_profile: v5-server");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			ZzzOcrService zzzOcrService = zContext.ZzzOcrService;
			Assert.Equal("v5-server", zzzOcrService.ProfileId);
			Assert.False(zzzOcrService.IsShutdown);
			zzzOcrService.Shutdown();
			Assert.True(zzzOcrService.IsShutdown);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ZzzOcrService_ResolvesLegacyOcrProfile()
	{
		string text = CreateTempRoot();
		try
		{
			Directory.CreateDirectory(Path.Combine(text, "config"));
			File.WriteAllText(Path.Combine(text, "config", "model.yml"), "ocr: ppocrv6");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			Assert.Equal("v6-small", zContext.ZzzOcrService.ProfileId);
			Assert.True(zContext.ZzzOcrService.Resolution.UsedLegacySelection);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
