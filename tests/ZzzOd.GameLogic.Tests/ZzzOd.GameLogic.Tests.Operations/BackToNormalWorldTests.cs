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
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class BackToNormalWorldTests : IDisposable
{
	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot;

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public StageController(Mat? screenshot = null)
		{
			_screenshot = screenshot?.Clone() ?? new Mat(new Size(400, 400), MatType.CV_8UC3, Scalar.Black);
		}

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

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			return CreateResults();
		}

		private IReadOnlyList<OcrMatchResult> CreateResults()
		{
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => new OcrMatchResult(0.99, 4 + index * 10, 4, 20, 10, word)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public BackToNormalWorldTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-back-normal-world-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
	}

	[Fact]
	public void DefaultOperationTimeoutMatchesPythonUnlimited()
	{
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		BackToNormalWorld obj = new BackToNormalWorld(context);
		PropertyInfo property = typeof(Operation).GetProperty("TimeoutSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.Equal(-1.0, Assert.IsType<double>(property.GetValue(obj)));
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsSuccessWhenAlreadyNormalWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage != 1)
			{
				IReadOnlyList<string> readOnlyList = Array.Empty<string>();
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Empty(controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ClicksDialogCancelBeforeReturningNormalWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage != 1)
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[] { "取消" };
				result2 = readOnlyList;
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 19), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ClicksCommonBackWhenCurrentScreenIsStaleAndGotoCannotRecognize()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage != 1)
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[] { "返回" };
				result2 = readOnlyList;
			}
			return result2;
		});
		context.ScreenContext.UpdateCurrentScreenName("大世界-普通");
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(15, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_AllowBattleReturnsBattleStatus()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[] { "普通攻击" });
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: true, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-战斗", result.Status);
		Assert.Empty(controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ClicksBattleMenuExitConfirmAndFinishBeforeReturningWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "普通攻击" }, 
				2 => new string[] { "退出战斗" }, 
				3 => new string[] { "确认退出" }, 
				4 => new string[] { "完成" }, 
				_ => new string[] { "普通世界标识" }, 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal(new List<OneDragon.Core.Abstractions.Geometry.Point>(4)
		{
			new OneDragon.Core.Abstractions.Geometry.Point(20, 20),
			new OneDragon.Core.Abstractions.Geometry.Point(15, 10),
			new OneDragon.Core.Abstractions.Geometry.Point(15, 10),
			new OneDragon.Core.Abstractions.Geometry.Point(15, 10)
		}, controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ExitsHollowEventBeforeReturningWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = ((stage <= 2) ? new string[2] { "背包", "放弃" } : (stage switch
			{
				3 => new string[] { "放弃" }, 
				4 => new string[] { "确认放弃" }, 
				5 => new string[] { "通关完成" }, 
				_ => new string[] { "普通世界标识" }, 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 339), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ExitsCompendiumWhenMultipleTabsAreRecognized()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		WriteCompendiumData();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage != 1)
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[2] { "训练", "作战" };
				result2 = readOnlyList;
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 249), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsFoundMapWhenMiniMapPlayerMaskIsVisible()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using Mat screen = CreateWorldScreenWithMiniMapPlayerMask();
		using StageController controller = new StageController(screen);
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("发现地图", result.Status);
		Assert.Empty(controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_ClicksSecondAgentDialogOption()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			IReadOnlyList<string> result2;
			if (stage != 1)
			{
				IReadOnlyList<string> readOnlyList = new string[] { "普通世界标识" };
				result2 = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[3] { "妮可", "寒暄", "离开" };
				result2 = readOnlyList;
			}
			return result2;
		});
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(25, 329), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_EnsureNormalWorldUsesTransportNodeGraphFromInvestigationWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = ((stage >= 4) ? new string[] { "普通世界标识" } : (stage switch
			{
				1 => new string[] { "勘域世界标识" }, 
				2 => new string[] { "地图" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		bool transportCalled = false;
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: true, allowBattle: false, delegate
		{
			transportCalled = true;
			return Task.FromResult(new OperationResult(IsSuccess: true, "传送完成"));
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.True(transportCalled);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(24, 19), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ExecuteAsync_FailsWhenScreenCannotBeRecognizedBeforeTimeout()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteScreenYaml();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		BackToNormalWorld operation = new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: false, null, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(20L));
		OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(2L));
		Assert.False(result.IsSuccess);
		Assert.Equal("执行超时", result.Status);
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
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: normal_world_basic\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 普通标识\n      id_mark: true\n      pc_rect: [1, 1, 40, 20]\n      text: 普通世界标识\n      lcs_percent: 0.9\n- screen_id: normal_world_investigation\n  screen_name: 大世界-勘域\n  area_list:\n    - area_name: 勘域标识\n      id_mark: true\n      pc_rect: [1, 1, 40, 20]\n      text: 勘域世界标识\n      lcs_percent: 0.9\n- screen_id: normal_world\n  screen_name: 大世界\n  area_list:\n    - area_name: 地图\n      pc_rect: [10, 10, 30, 30]\n      text: 地图\n      lcs_percent: 0.9\n    - area_name: 小地图\n      pc_rect: [0, 0, 240, 240]\n    - area_name: 对话框取消\n      pc_rect: [10, 10, 30, 30]\n      text: 取消\n      lcs_percent: 0.9\n    - area_name: 对话框确认\n      pc_rect: [40, 10, 60, 30]\n      text: 确认\n      lcs_percent: 0.9\n    - area_name: 好感度标题\n      pc_rect: [1, 300, 80, 320]\n    - area_name: 好感度选项\n      pc_rect: [1, 320, 80, 360]\n- screen_id: common_screen\n  screen_name: 画面-通用\n  area_list:\n    - area_name: 左上角-街区\n      pc_rect: [1, 1, 40, 20]\n      text: 街区\n      lcs_percent: 0.9\n    - area_name: 返回\n      pc_rect: [1, 1, 40, 20]\n      text: 返回\n      lcs_percent: 0.9\n    - area_name: 关闭\n      pc_rect: [1, 1, 40, 20]\n      text: 关闭\n      lcs_percent: 0.9\n    - area_name: 完成\n      pc_rect: [1, 1, 40, 20]\n      text: 完成\n      lcs_percent: 0.9\n- screen_id: battle_menu\n  screen_name: 战斗-菜单\n  area_list:\n    - area_name: 按钮-退出战斗\n      pc_rect: [1, 1, 40, 20]\n      text: 退出战斗\n      lcs_percent: 0.9\n    - area_name: 按钮-退出战斗-确认\n      pc_rect: [1, 1, 40, 20]\n      text: 确认退出\n      lcs_percent: 0.9\n    - area_name: 按钮-脱离卡死\n      pc_rect: [1, 1, 40, 20]\n      text: 脱离卡死\n      lcs_percent: 0.9\n    - area_name: 按钮-脱离卡死-确认\n      pc_rect: [1, 1, 40, 20]\n      text: 确认脱离\n      lcs_percent: 0.9\n- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 按键-普通攻击\n      pc_rect: [1, 1, 40, 20]\n      text: 普通攻击\n      lcs_percent: 0.9\n    - area_name: 菜单\n      pc_rect: [10, 10, 30, 30]\n- screen_id: map_3d\n  screen_name: 3D地图\n  area_list:\n    - area_name: 标题-标识点筛选\n      pc_rect: [1, 1, 80, 20]\n      text: 标识点筛选\n      lcs_percent: 0.9\n    - area_name: 按钮-关闭筛选\n      pc_rect: [40, 40, 70, 70]\n- screen_id: hollow_event\n  screen_name: 零号空洞-事件\n  area_list:\n    - area_name: 背包\n      pc_rect: [1, 250, 80, 270]\n      text: 背包\n      lcs_percent: 0.9\n    - area_name: 放弃\n      pc_rect: [10, 270, 38, 288]\n      text: 放弃\n      lcs_percent: 0.9\n    - area_name: 放弃-确认\n      pc_rect: [10, 290, 38, 308]\n      text: 确认放弃\n      lcs_percent: 0.9\n    - area_name: 通关-完成\n      pc_rect: [10, 310, 38, 328]\n      text: 通关完成\n      lcs_percent: 0.9\n    - area_name: 菜单\n      pc_rect: [10, 330, 38, 348]\n- screen_id: compendium\n  screen_name: 快捷手册\n  area_list:\n    - area_name: TAB列表\n      pc_rect: [1, 220, 80, 240]\n    - area_name: 按钮-退出\n      pc_rect: [10, 240, 38, 258]");
	}

	private void WriteCompendiumData()
	{
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data"));
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "compendium_data.yml"), "- tab_name: 训练\n- tab_name: 作战");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}

	private static Mat CreateWorldScreenWithMiniMapPlayerMask()
	{
		Mat mat = new Mat(240, 240, MatType.CV_8UC3, new Scalar(255.0, 255.0, 255.0));
		Cv2.Circle(mat, new OpenCvSharp.Point(70, 70), 10, new Scalar(0.0, 160.0, 255.0), -1);
		return mat;
	}
}
