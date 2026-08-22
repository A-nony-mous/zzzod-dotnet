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
using ZzzOd.GameLogic.Operations.Hdd;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class HddPhase214Tests : IDisposable
{
	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<(OneDragon.Core.Abstractions.Geometry.Point Start, OneDragon.Core.Abstractions.Geometry.Point End)> Drags { get; } = new List<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point)>();

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
			Drags.Add((start.GetValueOrDefault(), end));
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
			bool fullScreen = image.Width == 320 && image.Height == 240;
			return CreateResults(fullScreen);
		}

		private IReadOnlyList<OcrMatchResult> CreateResults(bool fullScreen)
		{
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => fullScreen ? new OcrMatchResult(0.99, 4 + index * 80, 4, 60, 16, word) : new OcrMatchResult(0.99, 4, 6, 12, 8, word)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public HddPhase214Tests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-hdd-214-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task EnterHddMission_SelectsChapterMissionTypeMissionAndDeploys()
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
				1 => new string[] { "第二章间章" }, 
				2 => new string[] { "第二章间章" }, 
				3 => new string[] { "战斗委托" }, 
				4 => new string[] { "下一步" }, 
				5 => new string[] { "作战真拿命验收" }, 
				6 => new string[] { "下一步" }, 
				7 => new string[] { "出战" }, 
				8 => new string[] { "确定并出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		EnterHddMission operation = new EnterHddMission(context, "第二章间章", "战斗委托", "作战真拿命验收", -1, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(10, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 140), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 170), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task EnterHddMission_DragsMissionListWhenMissionMissing()
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
				1 => new string[] { "第二章间章" }, 
				2 => new string[] { "第二章间章" }, 
				3 => new string[] { "战斗委托" }, 
				4 => new string[] { "下一步" }, 
				5 => new string[] { "旧副本" }, 
				6 => new string[] { "作战真拿命验收" }, 
				7 => new string[] { "下一步" }, 
				8 => new string[] { "出战" }, 
				9 => new string[] { "出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		EnterHddMission operation = new EnterHddMission(context, "第二章间章", "战斗委托", "作战真拿命验收", -1, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Contains((IEnumerable<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point)>)controller.Drags, (Predicate<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point)>)(((OneDragon.Core.Abstractions.Geometry.Point Start, OneDragon.Core.Abstractions.Geometry.Point End) drag) => drag.Start == new OneDragon.Core.Abstractions.Geometry.Point(100, 95) && drag.End == new OneDragon.Core.Abstractions.Geometry.Point(100, -105)));
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
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: hdd\n  screen_name: HDD\n  area_list:\n    - area_name: 章节列表\n      pc_rect: [0, 0, 200, 40]\n    - area_name: 章节显示\n      pc_rect: [0, 0, 200, 40]\n      text: 第二章间章\n      lcs_percent: 0.8\n    - area_name: 委托区域\n      pc_rect: [0, 40, 200, 80]\n    - area_name: 副本区域\n      pc_rect: [0, 80, 200, 110]\n    - area_name: 下一步\n      pc_rect: [10, 130, 30, 150]\n      text: 下一步\n      lcs_percent: 0.8\n    - area_name: 出战\n      pc_rect: [10, 160, 30, 180]\n      text: 出战\n      lcs_percent: 0.8\n    - area_name: 确定并出战\n      pc_rect: [10, 190, 30, 210]\n      text: 确定并出战\n      lcs_percent: 0.8");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
