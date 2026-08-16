using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using OneDragon.Core.Template;
using OneDragon.Core.Yolo;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class LostVoidFixedAssetTests
{
	private const string LostVoidModelName = "yolov26n-736-lost-void-det-20260630";
	private const string IntegrationEnvironmentVariable = "ZZZOD_RUN_YOLO_INTEGRATION";

	[Trait("Category", "Integration")]
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

	[Trait("Category", "Integration")]
	[YoloIntegrationFact]
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
			AssertUsesCurrentLostVoidModel(lostVoidDetector, text);
			Assert.True(lostVoidDetector.InitModel());
			AssertUsesCurrentLostVoidModel(lostVoidDetector, text);
			using Mat image = LoadFixture("lost_void-after.png");
			YoloDetectFrameResult yoloDetectFrameResult = lostVoidDetector.CoreDetector.Run(image, 0.6f, 0.5f, 0.0);
			Assert.Collection(yoloDetectFrameResult.Results, delegate(YoloDetectObjectResult distance)
			{
				Assert.Equal("0001-距离", distance.DetectClass.ClassName);
				Assert.Equal((1158, 307, 1201, 349), (distance.X1, distance.Y1, distance.X2, distance.Y2));
				Assert.Equal(0.8948050737380981d, distance.Score, 6);
			}, delegate(YoloDetectObjectResult shopRight)
			{
				Assert.Equal("0015-迷雾", shopRight.DetectClass.ClassName);
				Assert.Equal((1045, 288, 1089, 329), (shopRight.X1, shopRight.Y1, shopRight.X2, shopRight.Y2));
				Assert.Equal(0.6754642724990845d, shopRight.Score, 6);
			});
		}
		finally
		{
			Directory.Delete(text2, recursive: true);
		}
	}

	[Trait("Category", "Integration")]
	[YoloIntegrationFact]
	public void RealGameFixture_LostVoidYolo26RawContractMatchesPython()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string modelDirectory = Path.Combine(workspaceRoot, "assets", "models", "lost_void_det", LostVoidModelName);
		string modelPath = Path.Combine(modelDirectory, "model.onnx");
		using InferenceSession session = new InferenceSession(modelPath);
		Assert.Equal(new int[4] { 1, 3, 736, 736 }, session.InputMetadata["images"].Dimensions);
		Assert.Equal(new int[3] { 1, 20, 11109 }, session.OutputMetadata["output0"].Dimensions);
		Assert.Equal(16, File.ReadLines(Path.Combine(modelDirectory, "model_label.txt")).Count(line => !string.IsNullOrWhiteSpace(line)));
		Assert.True(session.ModelMetadata.CustomMetadataMap.TryGetValue("version", out string? version));
		Assert.Equal("8.3.157", version);
		Assert.True(session.ModelMetadata.CustomMetadataMap.TryGetValue("args", out string? exportArgs));
		Assert.Contains("'nms': False", exportArgs, StringComparison.Ordinal);

		IReadOnlyList<YoloDetectObjectResult> dotnetResults = RunDotNetDetector(workspaceRoot);
		IReadOnlyList<PythonYoloResult> pythonResults = RunPythonDetector(workspaceRoot, modelDirectory);

		Assert.Equal(pythonResults.Count, dotnetResults.Count);
		for (int index = 0; index < pythonResults.Count; index++)
		{
			PythonYoloResult python = pythonResults[index];
			YoloDetectObjectResult dotnet = dotnetResults[index];
			Assert.Equal(python.ClassName, dotnet.DetectClass.ClassName);
			Assert.Equal((python.X1, python.Y1, python.X2, python.Y2), (dotnet.X1, dotnet.Y1, dotnet.X2, dotnet.Y2));
			Assert.Equal(python.Score, dotnet.Score, 6);
		}
	}

	/// <summary>
	/// 生产默认 Reload 直接读取根独立 screen YAML：确认按钮矩形与代理人阵亡 area，
	/// 且 battle/agent_dead 模板可被生产索引。
	/// </summary>
	[Trait("Category", "Integration")]
	[Fact]
	public void ProductionScreenContext_LoadsIndependentConfirmAndAgentDeadAreas()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string runRoot = CreateRunRoot(workspaceRoot);
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, workspaceRoot));
			zContext.ScreenContext.Reload();

			AssertConfirmAndDeadAreas(zContext);

			TemplateInfo template = Assert.IsType<TemplateInfo>(zContext.TemplateLoader.GetTemplate("battle", "agent_dead"));
			using (template)
			{
				Assert.False(template.Raw?.Empty() ?? true, "battle/agent_dead raw.png 必须可被模板加载器读取");
				Assert.False(template.Mask?.Empty() ?? true, "battle/agent_dead mask.png 必须可被模板加载器读取");
			}
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	/// <summary>
	/// 缺失或故意陈旧的 _od_merged.yml 均不影响独立 screen 生产加载结果。
	/// </summary>
	[Trait("Category", "Integration")]
	[Fact]
	public void ProductionScreenContext_IndependentFilesIgnoreMissingOrStaleMerged()
	{
		OpenCvTestRuntime.RequireAvailable();
		string workspaceRoot = FindWorkspaceRoot();
		string runRoot = CreateRunRoot(workspaceRoot);
		string resourceRoot = Path.Combine(Path.GetTempPath(), "zzzod-lost-void-merged-ignored", Guid.NewGuid().ToString("N"));
		try
		{
			string screenInfoDirectory = CopyScreenInfoWithoutMerged(workspaceRoot, resourceRoot);
			Assert.False(File.Exists(Path.Combine(screenInfoDirectory, "_od_merged.yml")));

			using (ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, resourceRoot)))
			{
				zContext.ScreenContext.Reload();
				AssertConfirmAndDeadAreas(zContext);
			}

			WriteStaleMerged(screenInfoDirectory);
			Assert.True(File.Exists(Path.Combine(screenInfoDirectory, "_od_merged.yml")));
			using (ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, resourceRoot)))
			{
				zContext.ScreenContext.Reload();
				AssertConfirmAndDeadAreas(zContext);
			}
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
			Directory.Delete(resourceRoot, recursive: true);
		}
	}

	private static Mat LoadFixture(string fileName)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "TestData", "LostVoid", fileName);
		Mat mat = Cv2.ImRead(text);
		Assert.False(mat.Empty(), "无法读取固定资产 " + text);
		return mat;
	}

	private static IReadOnlyList<YoloDetectObjectResult> RunDotNetDetector(string workspaceRoot)
	{
		string runRoot = CreateRunRoot(workspaceRoot);
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(runRoot, workspaceRoot));
			zContext.LostVoid.InitLostVoidDetectorModel();
			LostVoidDetector detector = Assert.IsType<LostVoidDetector>(zContext.LostVoid.Detector);
			Assert.IsType<YoloDetector>(detector.CoreDetector);
			AssertUsesCurrentLostVoidModel(detector, workspaceRoot);
			Assert.True(detector.InitModel());
			AssertUsesCurrentLostVoidModel(detector, workspaceRoot);
			using Mat image = LoadFixture("lost_void-after.png");
			return detector.CoreDetector.Run(image, 0.6f, 0.5f, 0.0).Results.ToArray();
		}
		finally
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}

	private static void AssertUsesCurrentLostVoidModel(LostVoidDetector detector, string workspaceRoot)
	{
		string modelDirectory = Path.Combine(workspaceRoot, "assets", "models", "lost_void_det", LostVoidModelName);
		Assert.Equal(LostVoidModelName, detector.ModelName);
		Assert.Equal(LostVoidModelName, detector.CoreDetector.Config.ModelName);
		Assert.Equal(modelDirectory, detector.ModelDirectoryPath);
		Assert.Equal(Path.Combine(modelDirectory, "model.onnx"), detector.CoreDetector.Config.ModelPath);
	}

	private static IReadOnlyList<PythonYoloResult> RunPythonDetector(string workspaceRoot, string modelDirectory)
	{
		string pythonRoot = Path.Combine(workspaceRoot, "ZenlessZoneZero-OneDragon");
		string scriptPath = Path.Combine(AppContext.BaseDirectory, "TestData", "LostVoid", "yolo_python_parity.py");
		string imagePath = Path.Combine(AppContext.BaseDirectory, "TestData", "LostVoid", "lost_void-after.png");
		string outputPath = Path.Combine(Path.GetTempPath(), "zzzod-yolo-python-parity", Guid.NewGuid().ToString("N") + ".json");
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
		ProcessStartInfo startInfo = new("uv")
		{
			WorkingDirectory = pythonRoot,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.Environment["PYTHONPATH"] = Path.Combine(pythonRoot, "src");
		startInfo.ArgumentList.Add("run");
		startInfo.ArgumentList.Add("python");
		startInfo.ArgumentList.Add(scriptPath);
		startInfo.ArgumentList.Add(modelDirectory);
		startInfo.ArgumentList.Add(imagePath);
		startInfo.ArgumentList.Add(outputPath);

		try
		{
			using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 uv Python 对照进程。");
			var standardOutputTask = process.StandardOutput.ReadToEndAsync();
			var standardErrorTask = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(60000))
			{
				process.Kill(entireProcessTree: true);
				process.WaitForExit();
				_ = standardOutputTask.GetAwaiter().GetResult();
				_ = standardErrorTask.GetAwaiter().GetResult();
				throw new TimeoutException("Python YOLO 对照超过 60 秒。");
			}

			string standardOutput = standardOutputTask.GetAwaiter().GetResult();
			string standardError = standardErrorTask.GetAwaiter().GetResult();

			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException($"Python YOLO 对照失败，退出码 {process.ExitCode}。{standardOutput}{standardError}");
			}

			return JsonSerializer.Deserialize<List<PythonYoloResult>>(
				File.ReadAllText(outputPath),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
				?? throw new InvalidDataException("Python YOLO 对照没有输出结果。");
		}
		finally
		{
			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}
		}
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

	private static void AssertConfirmAndDeadAreas(ZContext zContext)
	{
		OneDragon.Core.Screen.ScreenArea confirmArea = Assert.IsType<OneDragon.Core.Screen.ScreenArea>(zContext.ScreenContext.GetArea("迷失之地-通用选择", "按钮-确定"));
		Assert.Equal((855, 765, 1165, 975), (confirmArea.X1, confirmArea.Y1, confirmArea.X2, confirmArea.Y2));
		Assert.Equal(0.7d, confirmArea.LcsPercent);

		OneDragon.Core.Screen.ScreenArea deadArea = Assert.IsType<OneDragon.Core.Screen.ScreenArea>(zContext.ScreenContext.GetArea("战斗画面", "代理人阵亡"));
		Assert.Equal((830, 40, 930, 87), (deadArea.X1, deadArea.Y1, deadArea.X2, deadArea.Y2));
		Assert.Equal("battle", deadArea.TemplateSubDir);
		Assert.Equal("agent_dead", deadArea.TemplateId);
		Assert.True(deadArea.IsTemplateArea);
	}

	private static string CopyScreenInfoWithoutMerged(string workspaceRoot, string resourceRoot)
	{
		string target = Path.Combine(resourceRoot, "assets", "game_data", "screen_info");
		CopyDirectory(Path.Combine(workspaceRoot, "assets", "game_data", "screen_info"), target);
		string mergedPath = Path.Combine(target, "_od_merged.yml");
		if (File.Exists(mergedPath))
		{
			File.Delete(mergedPath);
		}

		return target;
	}

	private static void WriteStaleMerged(string screenInfoDirectory)
	{
		File.WriteAllText(
			Path.Combine(screenInfoDirectory, "_od_merged.yml"),
			"""
			- screen_id: lost_void_choose_common
			  screen_name: 迷失之地-通用选择
			  app_id: lost_void
			  pc_alt: false
			  area_list:
			  - area_name: 按钮-确定
			    id_mark: true
			    pc_rect: [855, 795, 1165, 975]
			    text: 确定
			    lcs_percent: 0.7
			""");
	}

	private sealed record PythonYoloResult(string ClassName, double Score, int X1, int Y1, int X2, int Y2);

	[AttributeUsage(AttributeTargets.Method)]
	private sealed class YoloIntegrationFactAttribute : FactAttribute
	{
		public YoloIntegrationFactAttribute()
		{
			if (!string.Equals(
				Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable),
				"1",
				StringComparison.Ordinal))
			{
				Skip = $"Requires {IntegrationEnvironmentVariable}=1, the production LostVoid ONNX model, fixed screenshot assets and Python uv environment.";
			}
		}
	}
}
