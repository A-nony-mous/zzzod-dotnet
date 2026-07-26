using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class ImageAnalysisServiceTests
{
	[Fact]
	public void PipelineFilesRoundTripWithPythonYamlShape()
	{
		string text = CreateRoot();
		using ZzzRuntimeManager runtime = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
		ZzzImageAnalysisService zzzImageAnalysisService = new ZzzImageAnalysisService(new ZzzRunRoot(text), runtime);
		ImageAnalysisPipeline pipeline = new ImageAnalysisPipeline(new ImageAnalysisStep[2]
		{
			new ImageAnalysisStep("灰度化", new Dictionary<string, object>()),
			new ImageAnalysisStep("二值化", new Dictionary<string, object>
			{
				["method"] = "BINARY",
				["threshold_value"] = 127
			})
		});
		zzzImageAnalysisService.SavePipeline("roundtrip", pipeline);
		ImageAnalysisPipeline imageAnalysisPipeline = zzzImageAnalysisService.LoadPipeline("roundtrip");
		Assert.Equal(new string[2] { "灰度化", "二值化" }, imageAnalysisPipeline.Steps.Select((ImageAnalysisStep step) => step.Name));
		string actualString = File.ReadAllText(Path.Combine(text, "assets", "image_analysis_pipelines", "roundtrip.yml"));
		Assert.Contains("- step: 灰度化", actualString, StringComparison.Ordinal);
		Assert.Contains("threshold_value: 127", actualString, StringComparison.Ordinal);
	}

	[Fact]
	public void RealOpenCvPipelineProducesProcessedImageAndTimings()
	{
		string text = FindWorkspaceRoot();
		using ZzzRuntimeManager runtime = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
		ZzzImageAnalysisService zzzImageAnalysisService = new ZzzImageAnalysisService(new ZzzRunRoot(text), runtime);
		using Mat mat = new Mat(8, 8, MatType.CV_8UC3, new Scalar(20.0, 80.0, 200.0));
		byte[] imageBytes = mat.ImEncode();
		ImageAnalysisExecutionResult imageAnalysisExecutionResult = zzzImageAnalysisService.Execute(new ImageAnalysisPipeline(new ImageAnalysisStep[] { new ImageAnalysisStep("灰度化", new Dictionary<string, object>()) }), imageBytes);
		Assert.NotEmpty(imageAnalysisExecutionResult.DisplayImage);
		Assert.Single(imageAnalysisExecutionResult.StepTimings);
		Assert.Equal("灰度化", imageAnalysisExecutionResult.StepTimings[0].StepName);
		Assert.Contains("图像已转换为灰度", (IEnumerable<string>)imageAnalysisExecutionResult.AnalysisResults);
		Assert.True(imageAnalysisExecutionResult.TotalMilliseconds >= 0.0);
	}

	[Fact]
	public void AxamlUsesRequiredFluentControlsAndNoDemoValues()
	{
		string text = FindWorkspaceRoot();
		string[] buffer = new string[8];
		buffer[0] = text;
		buffer[1] = "zzzod-dotnet";
		buffer[2] = "src";
		buffer[3] = "ZzzOd.Gui";
		buffer[4] = "Views";
		buffer[5] = "FrontierPages";
		buffer[6] = "DevTools";
		buffer[7] = "FrontierImageAnalysisPage.axaml";
		string actualString = File.ReadAllText(Path.Combine(buffer));
		Assert.Contains("<fa:FACommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("<ListBox", actualString, StringComparison.Ordinal);
		Assert.Contains("<DataTemplate", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FANumberBox", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAContentDialog", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("默认流水线", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("battle/avatar", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("来源", actualString, StringComparison.Ordinal);
	}

	private static string CreateRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), $"zzz-image-analysis-{Guid.NewGuid():N}");
		Directory.CreateDirectory(text);
		return text;
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "assets")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到测试工作区根目录。");
	}
}
