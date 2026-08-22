using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class TransportTests : IDisposable
{
	private sealed record DragRecord(OneDragon.Core.Abstractions.Geometry.Point End, OneDragon.Core.Abstractions.Geometry.Point? Start);

	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = CreateScreen();

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<string> GamepadActions { get; } = new List<string>();

		public List<DragRecord> Drags { get; } = new List<DragRecord>();

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			if (position.HasValue)
			{
				Clicks.Add(position.Value);
			}
			if (gamepadAction != null)
			{
				GamepadActions.Add(gamepadAction);
			}
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
			Drags.Add(new DragRecord(end, start));
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
			return string.Concat(from result in CreateResults()
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

		private IReadOnlyList<OcrMatchResult> CreateResults()
		{
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => new OcrMatchResult(0.99, 4 + index * 30, 4, 20, 10, word)).ToArray();
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			bool fullScreen = image.Width == 80 && image.Height == 60;
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => fullScreen ? new OcrMatchResult(0.99, 4 + index * 30, 4, 20, 10, word) : new OcrMatchResult(0.99, 4, 4, 20, 10, word)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public TransportTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-transport-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data"));
	}

	[Fact]
	public void IsMapScreen_RequiresAreaNamesTransportPointAndBackButton()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[5] { "六分街", "录像店", "澄辉坪", "芭莱大厦前", "返回" });
		using Mat screen = CreateScreen();
		Transport transport = new Transport(context, "六分街", "录像店", waitAtLast: false, TimeSpan.Zero, TimeSpan.Zero);
		bool condition = transport.IsMapScreen(screen);
		Assert.True(condition);
	}

	[Fact]
	public void IsMapScreen_DoesNotCountDuplicateOcrBoxesAsDifferentAreaNames()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[5] { "六分街", "六分街", "六分街", "录像店", "返回" });
		using Mat screen = CreateScreen();
		Transport transport = new Transport(context, "六分街", "录像店", waitAtLast: false, TimeSpan.Zero, TimeSpan.Zero);
		MapScreenRecognitionSummary mapScreenRecognitionSummary = transport.GetMapScreenRecognitionSummary(screen);
		Assert.False(mapScreenRecognitionSummary.IsMapScreen);
		Assert.Equal(1, mapScreenRecognitionSummary.AreaNameMatchCount);
		Assert.Equal(new string[3] { "六分街", "录像店", "返回" }, mapScreenRecognitionSummary.OcrTexts);
	}

	[Fact]
	public void DeclaresPythonNodeGraphWithoutSyntheticWaitMapNode()
	{
		Dictionary<string, MethodInfo> dictionary = (from method in typeof(Transport).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
			select (Method: method, Node: method.GetCustomAttribute<OperationNodeAttribute>()) into item
			where item.Node != null
			select item).ToDictionary<(MethodInfo, OperationNodeAttribute), string, MethodInfo>(((MethodInfo Method, OperationNodeAttribute Node) item) => item.Node.Name, ((MethodInfo Method, OperationNodeAttribute Node) item) => item.Method, StringComparer.Ordinal);
		string[] source = new string[5] { "画面识别", "返回大世界", "打开地图", "执行传送", "等待大世界加载" };
		Assert.Equal(source.OrderBy<string, string>((string name) => name, StringComparer.Ordinal), dictionary.Keys.OrderBy<string, string>((string name) => name, StringComparer.Ordinal));
		Assert.DoesNotContain("等待地图", (IEnumerable<string>)dictionary.Keys);
		Assert.Contains(dictionary["执行传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "打开地图");
		Assert.Contains(dictionary["执行传送"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "画面识别");
	}

	[Fact]
	public void DefaultOperationTimeoutMatchesPythonUnlimitedExecution()
	{
		WriteGameData();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		Transport obj = new Transport(context, "六分街", "录像店");
		PropertyInfo property = typeof(Operation).GetProperty("TimeoutSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.Equal(-1.0, property.GetValue(obj));
	}

	[Fact]
	public async Task MapTransport_ClicksAreaTransportPointAndConfirm()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "六分街" }, 
				2 => new string[2] { "咖啡店", "录像店" }, 
				3 => new string[] { "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		MapTransport operation = new MapTransport(context, "六分街", "录像店", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("确认", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(14, 9), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(44, 9), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 19), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task MapTransport_DragsAreaListWhenTargetAreaIsNotVisible()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result = stage switch
			{
				1 => new string[] { "澄辉坪" }, 
				2 => new string[] { "六分街" }, 
				3 => new string[] { "录像店" }, 
				4 => new string[] { "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		MapTransport operation = new MapTransport(context, "六分街", "录像店", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		DragRecord drag = Assert.Single(controller.Drags);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), drag.Start);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1460, 540), drag.End);
	}

	[Fact]
	public async Task MapTransport_DragsAreaListTowardRightWhenTargetAreaIsAfterVisibleArea()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result = stage switch
			{
				1 => new string[] { "六分街" }, 
				2 => new string[] { "芭莱大厦前" }, 
				3 => new string[] { "喵吉长官" }, 
				4 => new string[] { "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		MapTransport operation = new MapTransport(context, "芭莱大厦前", "喵吉长官", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		DragRecord drag = Assert.Single(controller.Drags);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), drag.Start);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(460, 540), drag.End);
	}

	[Fact]
	public async Task MapTransport_DragsTransportPointListLeftWhenVisiblePointIsBeforeTarget()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result = stage switch
			{
				1 => new string[] { "六分街" }, 
				2 => new string[] { "录像店" }, 
				3 => new string[] { "咖啡店" }, 
				4 => new string[] { "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		MapTransport operation = new MapTransport(context, "六分街", "咖啡店", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		DragRecord drag = Assert.Single(controller.Drags);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(25, 15), drag.Start);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(-775, 15), drag.End);
	}

	[Fact]
	public async Task MapTransport_DragsTransportPointListRightWhenVisiblePointIsAfterTarget()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result = stage switch
			{
				1 => new string[] { "六分街" }, 
				2 => new string[] { "咖啡店" }, 
				3 => new string[] { "录像店" }, 
				4 => new string[] { "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		MapTransport operation = new MapTransport(context, "六分街", "录像店", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		DragRecord drag = Assert.Single(controller.Drags);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(25, 15), drag.Start);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(775, 15), drag.End);
	}

	[Fact]
	public async Task MapTransport_MissingAreaFailsWithoutThrowing()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteMapScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[] { "六分街" });
		MapTransport operation = new MapTransport(context, "不存在区域", "录像店", TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("地图区域未配置 不存在区域", result.Status);
	}

	[Fact]
	public async Task ExecuteAsync_TimesOutInOpenMapWhenMapClickDoesNotOpenMap()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteGameData();
		WriteTransportScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage > 2)
			{
				IReadOnlyList<string> readOnlyList = new string[] { "地图" };
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			return result2;
		});
		Transport operation = new Transport(context, "六分街", "咖啡店", waitAtLast: false, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(500L), TimeSpan.Zero, (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通")));
		OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.False(result.IsSuccess);
		Assert.Equal("执行超时", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 19), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Empty(controller.GamepadActions);
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
		zContext.MapService.Reload();
		return zContext;
	}

	private void WriteGameData()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "map_area.yml"), "- area_name: 六分街\n  tp_list:\n    - 录像店\n    - 咖啡店\n- area_name: 澄辉坪\n  tp_list:\n    - 汀曼咖啡\n- area_name: 芭莱大厦前\n  tp_list:\n    - 喵吉长官");
	}

	private void WriteMapScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: map\n  screen_name: 地图\n  area_list:\n    - area_name: 左上角返回\n      pc_rect: [1, 1, 40, 20]\n      text: 返回\n      lcs_percent: 0.9\n    - area_name: 传送点名称\n      pc_rect: [20, 20, 70, 50]\n    - area_name: 确认\n      pc_rect: [10, 10, 30, 30]\n      text: 确认\n      lcs_percent: 0.9");
	}

	private void WriteTransportScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: normal_world_basic\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 普通标识\n      id_mark: true\n      pc_rect: [1, 1, 40, 20]\n      text: 普通世界标识\n      lcs_percent: 0.9\n- screen_id: normal_world\n  screen_name: 大世界\n  area_list:\n    - area_name: 地图\n      pc_rect: [10, 10, 30, 30]\n      text: 地图\n      lcs_percent: 0.9\n- screen_id: map\n  screen_name: 地图\n  area_list:\n    - area_name: 左上角返回\n      pc_rect: [1, 1, 40, 20]\n      text: 返回\n      lcs_percent: 0.9\n    - area_name: 传送点名称\n      pc_rect: [20, 20, 70, 50]\n    - area_name: 确认\n      pc_rect: [10, 10, 30, 30]\n      text: 确认\n      lcs_percent: 0.9");
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
