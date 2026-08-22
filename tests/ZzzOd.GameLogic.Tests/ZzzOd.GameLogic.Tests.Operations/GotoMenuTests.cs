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

public sealed class GotoMenuTests : IDisposable
{
	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(120, 100), MatType.CV_8UC3, Scalar.Black);

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			if (position.HasValue)
			{
				Clicks.Add(position.Value);
			}
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

		public void Dispose()
		{
			_screenshot.Dispose();
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			ScreenshotStage++;
			return _screenshot.Clone();
		}
	}

	private sealed class StageOcrMatcher(StageController controller, Func<int, IReadOnlyList<string>> stageWords) : IOcrMatcher
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
			return string.Concat(stageWords(controller.ScreenshotStage));
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
			bool fullScreen = image.Width == 120 && image.Height == 100;
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => fullScreen ? new OcrMatchResult(0.99, 4 + index * 30, 4, 20, 10, word) : new OcrMatchResult(0.99, 4, 4, 20, 10, word)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public GotoMenuTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-goto-menu-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task ExecuteAsync_SucceedsWhenAlreadyInMenu()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[] { "返回" }));
			if (1 == 0)
			{
			}
			return result2;
		});
		GotoMenu operation = new GotoMenu(context, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("菜单", result.Status);
		Assert.Empty(controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ClicksRouteFromNormalWorldToMenu()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[2] { "信息", "菜单" }, 
				2 => new string[] { "返回" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		GotoMenu operation = new GotoMenu(context, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("菜单", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 49), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsToWorldBeforeOpeningMenuWhenScreenCannotRoute()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		int backCalls = 0;
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				2 => new string[2] { "信息", "菜单" }, 
				3 => new string[] { "返回" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		context.ScreenContext.UpdateCurrentScreenName("大世界-普通");
		GotoMenu operation = new GotoMenu(context, delegate
		{
			backCalls++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("菜单", result.Status);
		Assert.Equal(1, backCalls);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 49), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContext(StageController controller, Func<int, IReadOnlyList<string>> stageWords)
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.AttachController(controller);
		zContext.OcrService.Matcher = new StageOcrMatcher(controller, stageWords);
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
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: normal_world\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 信息\n      id_mark: true\n      pc_rect: [10, 10, 30, 30]\n      text: 信息\n      lcs_percent: 0.8\n    - area_name: 打开菜单\n      pc_rect: [10, 40, 30, 60]\n      text: 菜单\n      lcs_percent: 0.8\n      goto_list: [菜单]\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      id_mark: true\n      pc_rect: [10, 70, 30, 90]\n      text: 返回\n      lcs_percent: 0.8");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
