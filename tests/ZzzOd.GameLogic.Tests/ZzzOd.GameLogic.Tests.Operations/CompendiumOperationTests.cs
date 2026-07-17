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
using OneDragon.Core.Screen;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations.Compendium;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class CompendiumOperationTests : IDisposable
{
	private sealed record OcrWord(string Text, int X, int Y, int Width = 20, int Height = 10);

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
			Drags.Add((start ?? base.CenterPoint, end));
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

	private sealed class StageOcrMatcher(StageController controller, Func<int, int, IReadOnlyList<OcrWord>> words) : IOcrMatcher
	{
		private int _callCount;

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
			return string.Concat(from result in Ocr(image, threshold.GetValueOrDefault())
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
			_callCount++;
			return (from word in words(controller.ScreenshotStage, _callCount)
				select new OcrMatchResult(0.99, word.X, word.Y, word.Width, word.Height, word.Text)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public CompendiumOperationTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-compendium-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task OpenCompendium_ExecuteAsync_OpensMenuAndClicksCompendium()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage, int _)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<OcrWord> result2 = stage switch
			{
				1 => new OcrWord[2]
				{
					new OcrWord("信息", 10, 5),
					new OcrWord("菜单", 10, 5)
				}, 
				2 => new OcrWord[] { new OcrWord("返回", 10, 5) }, 
				3 => new OcrWord[] { new OcrWord("快捷手册", 10, 5) }, 
				_ => Array.Empty<OcrWord>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		OpenCompendium operation = new OpenCompendium(context, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("快捷手册", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(60, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task CompendiumChooseTab_ExecuteAsync_ClicksMatchedTab()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _, int _) => new OcrWord[] { new OcrWord("训练", 10, 5) });
		CompendiumChooseTab operation = new CompendiumChooseTab(context, "训练", TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(120, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task CompendiumChooseCategory_ExecuteAsync_ClicksMatchedCategory()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _, int _) => new OcrWord[] { new OcrWord("区域巡防", 10, 20) });
		CompendiumChooseCategory operation = new CompendiumChooseCategory(context, "区域巡防", TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(70, 125), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task CompendiumChooseMissionType_ExecuteAsync_ClicksGoButtonBelowTargetAndConfirms()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteCompendiumData();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage, int call)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<OcrWord> result;
			if (stage != 1)
			{
				if (stage != 2)
				{
					goto IL_00af;
				}
				result = new OcrWord[] { new OcrWord("确认", 10, 10) };
			}
			else if (call != 1)
			{
				if (call != 2)
				{
					goto IL_00af;
				}
				result = new OcrWord[2]
				{
					new OcrWord("前往", 10, 20),
					new OcrWord("前往", 10, 80)
				};
			}
			else
			{
				result = new OcrWord[2]
				{
					new OcrWord("VR训练", 10, 10),
					new OcrWord("高塔与巨炮", 10, 50)
				};
			}
			goto IL_00b7;
			IL_00af:
			result = Array.Empty<OcrWord>();
			goto IL_00b7;
			IL_00b7:
			if (1 == 0)
			{
			}
			return result;
		});
		CompendiumMissionType missionType = context.CompendiumService.GetMissionTypeData("训练", "区域巡防", "高塔与巨炮") ?? throw new InvalidOperationException("test data missing");
		CompendiumChooseMissionType operation = new CompendiumChooseMissionType(context, missionType, null, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 85), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 15), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public void CompendiumChooseMissionType_AgentAvatarResolverIgnoresNotoriousHuntDarkTopStrip()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using Mat mat = new Mat(new Size(500, 500), MatType.CV_8UC3, Scalar.Black);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(100, 200, 260, 45), new Scalar(5.0, 5.0, 5.0), -1);
		Cv2.Rectangle(mat, new OpenCvSharp.Rect(180, 270, 90, 90), new Scalar(70.0, 180.0, 230.0), -1);
		OneDragon.Core.Screen.ScreenArea area = new OneDragon.Core.Screen.ScreenArea
		{
			AreaName = "目标列表-训练-恶名狩猎",
			PcRect = new OneDragon.Core.Abstractions.Geometry.Rect(100, 200, 360, 420)
		};
		OneDragon.Core.Abstractions.Geometry.Point? actual = CompendiumChooseMissionType.ResolveAgentPlanTargetByImage(mat, area);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(225, 235), actual);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContext(StageController controller, Func<int, int, IReadOnlyList<OcrWord>> words)
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.AttachController(controller);
		zContext.OcrService.Matcher = new StageOcrMatcher(controller, words);
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
		File.WriteAllText(Path.Combine(buffer), "- screen_id: normal_world\n  screen_name: 大世界-普通\n  area_list:\n    - area_name: 信息\n      id_mark: true\n      pc_rect: [0, 0, 30, 20]\n      text: 信息\n      lcs_percent: 0.8\n    - area_name: 打开菜单\n      pc_rect: [40, 0, 80, 20]\n      text: 菜单\n      lcs_percent: 0.8\n      goto_list: [菜单]\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      id_mark: true\n      pc_rect: [0, 20, 30, 40]\n      text: 返回\n      lcs_percent: 0.8\n    - area_name: 底部列表\n      pc_rect: [0, 0, 160, 40]\n- screen_id: compendium\n  screen_name: 快捷手册\n  area_list:\n    - area_name: TAB列表\n      pc_rect: [100, 0, 300, 40]\n    - area_name: 分类列表\n      pc_rect: [50, 100, 260, 180]\n    - area_name: 副本列表\n      pc_rect: [0, 0, 260, 140]\n    - area_name: 前往列表\n      pc_rect: [0, 0, 260, 220]\n    - area_name: 传送确认\n      pc_rect: [0, 0, 120, 40]\n      text: 确认\n      lcs_percent: 0.8\n    - area_name: 目标列表-训练\n      pc_rect: [0, 0, 260, 220]\n    - area_name: 目标列表-训练-恶名狩猎\n      pc_rect: [0, 0, 260, 220]");
	}

	private void WriteCompendiumData()
	{
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data"));
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "compendium_data.yml"), "- tab_name: 训练\n  category_list:\n    - category_name: 区域巡防\n      mission_type_list:\n        - mission_type_name: VR训练\n          alias_list:\n            - VR\n        - mission_type_name: 高塔与巨炮\n          alias_list:\n            - 巨炮\n        - mission_type_name: 代理人方案培养");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
