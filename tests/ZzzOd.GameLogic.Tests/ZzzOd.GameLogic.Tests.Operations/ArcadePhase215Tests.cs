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
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations.Arcade;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class ArcadePhase215Tests : IDisposable
{
	private sealed record ControllerAction(string Name, TimeSpan? PressTime);

	private sealed class StageController : ControllerBase, IZzzControllerActions, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<ControllerAction> Actions { get; } = new List<ControllerAction>();

		public List<float> TurnDistances { get; } = new List<float>();

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

		public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			Actions.Add(new ControllerAction("MoveW", pressTime));
		}

		public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			Actions.Add(new ControllerAction("MoveS", pressTime));
		}

		public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			Actions.Add(new ControllerAction("MoveA", pressTime));
		}

		public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			Actions.Add(new ControllerAction("MoveD", pressTime));
		}

		public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			Actions.Add(new ControllerAction("Interact", pressTime));
		}

		public void TurnByDistance(float distance)
		{
			TurnDistances.Add(distance);
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

	public ArcadePhase215Tests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-arcade-215-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task ArcadeStartGame_TransportsMovesInteractsAndStartsSelectedGame()
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
				4 => new string[] { "返回" }, 
				5 => new string[] { "街机模式" }, 
				6 => new string[] { "蛇对蛇" }, 
				7 => new string[] { "选择" }, 
				8 => new string[] { "开始游戏" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		List<string> calls = new List<string>();
		ArcadeStartGame operation = new ArcadeStartGame(context, "蛇对蛇", delegate
		{
			calls.Add("transport");
			return Task.FromResult(new OperationResult(IsSuccess: true, "电玩店"));
		}, delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal<List<string>>(new List<string>(2) { "transport", "wait" }, calls);
		Assert.Contains((IEnumerable<ControllerAction>)controller.Actions, (Predicate<ControllerAction>)((ControllerAction action) => action.Name == "MoveW" && action.PressTime == TimeSpan.FromSeconds(1.5)));
		Assert.Contains((IEnumerable<ControllerAction>)controller.Actions, (Predicate<ControllerAction>)((ControllerAction action) => action.Name == "Interact" && action.PressTime == TimeSpan.FromMilliseconds(200L)));
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(10, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 110), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 140), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ArcadeSnakeSuicide_RestartsUntilTotalCountAndReturnsWorld()
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
				2 => new string[] { "加载完成" }, 
				3 => Array.Empty<string>(), 
				4 => new string[] { "点击空白处继续" }, 
				5 => new string[] { "加载完成" }, 
				6 => new string[] { "点击空白处继续" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		List<string> calls = new List<string>();
		ArcadeSnakeSuicide operation = new ArcadeSnakeSuicide(context, 2, delegate
		{
			calls.Add("start");
			return Task.FromResult(new OperationResult(IsSuccess: true, "开始"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal<List<string>>(new List<string>(2) { "start", "back" }, calls);
		Assert.Equal(2, operation.FinishCount);
		Assert.Contains((IEnumerable<ControllerAction>)controller.Actions, (Predicate<ControllerAction>)((ControllerAction action) => action.Name == "MoveW" && action.PressTime == TimeSpan.FromMilliseconds(200L)));
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(10, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
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
		File.WriteAllText(Path.Combine(buffer), "- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      pc_rect: [0, 0, 200, 40]\n      text: 返回\n      lcs_percent: 0.8\n- screen_id: arcade\n  screen_name: 电玩店\n  area_list:\n    - area_name: 模式列表\n      pc_rect: [0, 0, 200, 40]\n    - area_name: 游戏名称\n      pc_rect: [0, 0, 200, 40]\n    - area_name: 下一个游戏\n      pc_rect: [10, 70, 30, 90]\n    - area_name: 选择\n      pc_rect: [10, 100, 30, 120]\n      text: 选择\n      lcs_percent: 0.8\n    - area_name: 开始游戏\n      pc_rect: [10, 130, 30, 150]\n      text: 开始游戏\n      lcs_percent: 0.8\n    - area_name: 蛇对蛇-加载完成\n      pc_rect: [0, 0, 200, 40]\n      text: 加载完成\n      lcs_percent: 0.8\n    - area_name: 蛇对蛇-点击空白处继续\n      pc_rect: [0, 0, 200, 40]\n      text: 点击空白处继续\n      lcs_percent: 0.8");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
