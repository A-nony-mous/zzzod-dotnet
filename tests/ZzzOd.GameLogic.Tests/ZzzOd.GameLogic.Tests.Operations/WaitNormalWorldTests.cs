using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class WaitNormalWorldTests : IDisposable
{
	private sealed class ScreenshotController(Mat screenshot) : ControllerBase
	{
		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return screenshot.Clone();
		}
	}

	private sealed class FakeOcrMatcher(IReadOnlyList<OcrMatchResult> results) : IOcrMatcher
	{
		public void UpdateUseGpu(bool useGpu)
		{
		}

		public bool IsUseGpu()
		{
			return false;
		}

		public bool InitModel(string? proxyUrl = null, string? ghProxyUrl = null, bool skipIfExisted = true, Action<double, string>? progressCallback = null)
		{
			return true;
		}

		public string RunOcrSingleLine(Mat image, double? threshold = null, bool strictOneLine = true)
		{
			return string.Concat(from result in results
				orderby result.Y, result.X
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in Ocr(image, threshold.GetValueOrDefault(), mergeLineDistance))
			{
				if (!dictionary.TryGetValue(item.Text, out var value))
				{
					value = new MatchResultList(onlyBest: false);
					dictionary[item.Text] = value;
				}
				value.Append(item, autoMerge: false);
			}
			return dictionary;
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return results.Select((OcrMatchResult result) => new OcrMatchResult(result.Confidence, result.X, result.Y, result.Width, result.Height, result.Text)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public WaitNormalWorldTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-wait-normal-world-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsMatchedNormalWorldScreen()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using ZContext context = CreateContext(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 4, 20, 10, "普通世界标识") });
		using Mat screenshot = CreateScreen();
		context.AttachController(new ScreenshotController(screenshot.Clone()));
		WaitNormalWorld operation = new WaitNormalWorld(context, checkOnce: true);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal("大世界-普通", context.ScreenContext.CurrentScreenName);
	}

	[Fact]
	public async Task ExecuteAsync_FallsBackToBinaryInfoArea()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using ZContext context = CreateContext(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 4, 20, 10, "信息") });
		using Mat screenshot = CreateScreen();
		context.AttachController(new ScreenshotController(screenshot.Clone()));
		WaitNormalWorld operation = new WaitNormalWorld(context, checkOnce: true);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("信息", result.Status);
	}

	[Fact]
	public async Task ExecuteAsync_FallsBackToWeekArea()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using ZContext context = CreateContext(new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 4, 20, 10, "星期") });
		using Mat screenshot = CreateScreen();
		context.AttachController(new ScreenshotController(screenshot.Clone()));
		WaitNormalWorld operation = new WaitNormalWorld(context, checkOnce: true);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("星期", result.Status);
	}

	[Fact]
	public async Task ExecuteAsync_FallsBackToNormalWorldOcrText()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using ZContext context = CreateContext(new OcrMatchResult[] { new OcrMatchResult(0.99, 70, 145, 120, 20, "前往任务目标") });
		using Mat screenshot = CreateScreen();
		context.AttachController(new ScreenshotController(screenshot.Clone()));
		WaitNormalWorld operation = new WaitNormalWorld(context, checkOnce: true);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界", result.Status);
	}

	[Fact]
	public async Task ExecuteAsync_CheckOnceFailsWhenNormalWorldNotReached()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using ZContext context = CreateContext(Array.Empty<OcrMatchResult>());
		using Mat screenshot = CreateScreen();
		context.AttachController(new ScreenshotController(screenshot.Clone()));
		WaitNormalWorld operation = new WaitNormalWorld(context, checkOnce: true);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("未到达大世界", result.Status);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContext(IReadOnlyList<OcrMatchResult> ocrResults)
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.OcrService.Matcher = new FakeOcrMatcher(ocrResults);
		zContext.ScreenContext.Reload();
		return zContext;
	}

	private void WriteScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		File.WriteAllText(Path.Combine(buffer), "- screen_id: normal_world_basic\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 普通标识\n      id_mark: true\n      pc_rect: [1, 1, 40, 20]\n      text: 普通世界标识\n      lcs_percent: 0.9\n- screen_id: normal_world_investigation\n  screen_name: 大世界-勘域\n  area_list:\n    - area_name: 勘域标识\n      id_mark: true\n      pc_rect: [1, 1, 40, 20]\n      text: 勘域世界标识\n      lcs_percent: 0.9\n- screen_id: normal_world\n  screen_name: 大世界\n  area_list:\n    - area_name: 信息\n      pc_rect: [1, 1, 40, 20]\n      text: 信息\n      lcs_percent: 0.9\n    - area_name: 星期\n      pc_rect: [1, 1, 40, 20]\n      text: 星期\n      lcs_percent: 0.9");
	}

	private static Mat CreateScreen()
	{
		return new Mat(new Size(80, 60), MatType.CV_8UC3, Scalar.Black);
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
