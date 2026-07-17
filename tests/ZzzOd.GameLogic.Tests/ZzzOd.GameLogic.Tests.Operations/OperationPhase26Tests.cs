using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class OperationPhase26Tests : IDisposable
{
	private sealed record DragRecord(OneDragon.Core.Abstractions.Geometry.Point End, OneDragon.Core.Abstractions.Geometry.Point? Start);

	private sealed class StageController(int standardWidth = 800, int standardHeight = 650) : ControllerBase(null, 0, standardWidth, standardHeight), IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(800, 650), MatType.CV_8UC3, Scalar.Black);

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<DragRecord> Drags { get; } = new List<DragRecord>();

		public List<OneDragon.Core.Abstractions.Geometry.Point> MouseMoves { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

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
			Drags.Add(new DragRecord(end, start));
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
			MouseMoves.Add(position);
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

	private sealed class StageOcrMatcher(StageController controller, Func<int, IReadOnlyList<string>> stageWords, Dictionary<int, Queue<string>> singleLineByStage) : IOcrMatcher
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
			int screenshotStage = controller.ScreenshotStage;
			Queue<string> value;
			return (singleLineByStage.TryGetValue(screenshotStage, out value) && value.Count > 0) ? value.Dequeue() : string.Concat(stageWords(screenshotStage));
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
			bool fullScreen = image.Width == 800 && image.Height == 650;
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => fullScreen ? new OcrMatchResult(0.99, 4 + index * 100, 4, 30, 12, word) : new OcrMatchResult(0.99, 4, 6, 12, 8, word)).ToArray();
		}
	}

	private readonly string _rootDirectory;

	public OperationPhase26Tests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-operation-26-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task Deploy_ClicksDeployAndConfirmsLowLevelDialog()
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
				1 => new string[] { "出战" }, 
				3 => new string[] { "确定并出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		Deploy operation = new Deploy(context, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("无需确认", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 20), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 80), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task Deploy_DiskFullFailsWithoutConfirming()
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
				1 => new string[] { "出战" }, 
				3 => new string[] { "驱动盘数量已达到可拥有上限" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		Deploy operation = new Deploy(context, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("驱动盘数量已达到可拥有上限", result.Status);
		Assert.Single(controller.Clicks);
	}

	[Fact]
	public async Task ChoosePredefinedTeam_SelectsMatchingTeamAndClicksPrepare()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTeamConfig();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "预备编队" }, 
				2 => new string[] { "预备编队" }, 
				3 => new string[2] { "猫又队", "妮可队" }, 
				4 => new string[] { "预备出战" }, 
				5 => new string[] { "预备出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		ChoosePredefinedTeam operation = new ChoosePredefinedTeam(context, new int[] { 1 }, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("预备出战", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 140), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(319, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 170), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains((IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.MouseMoves, (Predicate<OneDragon.Core.Abstractions.Geometry.Point>)((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(1866, 1069)));
	}

	[Fact]
	public async Task ChoosePredefinedTeam_WaitsAfterPrepareClickBeforeMovingMouse()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTeamConfig();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "预备编队" }, 
				2 => new string[] { "预备编队" }, 
				3 => new string[2] { "猫又队", "妮可队" }, 
				4 => new string[] { "预备出战" }, 
				5 => new string[] { "预备出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		ChoosePredefinedTeam operation = new ChoosePredefinedTeam(context, new int[] { 1 }, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(75L), TimeSpan.Zero);
		Stopwatch stopwatch = Stopwatch.StartNew();
		OperationResult result = await operation.ExecuteAsync();
		stopwatch.Stop();
		Assert.True(result.IsSuccess);
		Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(60L), $"实际等待 {stopwatch.Elapsed.TotalMilliseconds:0.###}ms");
		Assert.Contains((IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.MouseMoves, (Predicate<OneDragon.Core.Abstractions.Geometry.Point>)((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(1866, 1069)));
	}

	[Fact]
	public async Task ChoosePredefinedTeam_DragsWhenTargetTeamIsOnLaterPage()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTeamConfig();
		using StageController controller = new StageController(1920, 1080);
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result = stage switch
			{
				1 => new string[] { "预备编队" }, 
				2 => new string[] { "预备编队" }, 
				3 => new string[] { "猫又队" }, 
				4 => new string[] { "第六队" }, 
				5 => new string[] { "预备出战" }, 
				6 => new string[] { "预备出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result;
		});
		ChoosePredefinedTeam operation = new ChoosePredefinedTeam(context, new int[] { 5 }, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		DragRecord drag = Assert.Single(controller.Drags);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), drag.Start);
		Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 40), drag.End);
	}

	[Fact]
	public async Task ChoosePredefinedTeam_UsesPythonDifflibForInsertedOcrCharacters()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTeamConfig();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "预备编队" }, 
				2 => new string[] { "预备编队" }, 
				3 => new string[] { "错妮可队错错" }, 
				4 => new string[] { "预备出战" }, 
				5 => new string[] { "预备出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		ChoosePredefinedTeam operation = new ChoosePredefinedTeam(context, new int[] { 1 }, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(219, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task ChoosePredefinedTeam_PreservesPythonNegativeIndexSelection()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteTeamConfig();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				1 => new string[] { "预备编队" }, 
				2 => new string[] { "预备编队" }, 
				3 => new string[] { "编队20" }, 
				4 => new string[] { "预备出战" }, 
				5 => new string[] { "预备出战" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		ChoosePredefinedTeam operation = new ChoosePredefinedTeam(context, new int[] { -1 }, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(219, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task EatNoodle_OrdersNoodleAndReturnsToWorld()
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
				5 => new string[] { "招牌拉面" }, 
				6 => new string[] { "点单" }, 
				7 => new string[] { "点单确认" }, 
				8 => new string[] { "点单后跳过" }, 
				9 => new string[] { "效果确认" }, 
				10 => new string[] { "效果确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		List<string> actions = new List<string>();
		EatNoodle operation = new EatNoodle(context, "招牌拉面", (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "传送完成")), (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "大世界")), (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "已返回")), delegate
		{
			actions.Add("移动交互");
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("已返回", result.Status);
		Assert.Contains("移动交互", (IEnumerable<string>)actions);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 230), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 260), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 290), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.NotEmpty(controller.Drags);
	}

	[Fact]
	public void ChargePlanConfig_LoadsRestoreModeFromApplicationGroupYaml()
	{
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "config", "00", "one_dragon"));
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "config";
		buffer[2] = "00";
		buffer[3] = "one_dragon";
		buffer[4] = "charge_plan.yml";
		File.WriteAllText(Path.Combine(buffer), "restore_charge: 使用以太电池");
		using ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		ChargePlanConfig chargePlanConfig = ChargePlanConfig.Load(zContext.Environment, 0, "one_dragon");
		Assert.Equal(RestoreChargeMode.EtherOnly.DisplayName, chargePlanConfig.RestoreCharge);
		Assert.True(chargePlanConfig.IsRestoreChargeEnabled);
	}

	[Fact]
	public async Task RestoreCharge_MenuProbeReselectsEtherWhenBackupChargeIsNotEnough()
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
				1 => new string[] { "电量" }, 
				2 => new string[2] { "储蓄电量", "以太电池" }, 
				3 => new string[] { "确认" }, 
				4 => new string[] { "快捷使用" }, 
				5 => new string[2] { "快捷使用", "关闭" }, 
				7 => new string[] { "恢复电量" }, 
				8 => new string[2] { "储蓄电量", "以太电池" }, 
				9 => new string[] { "确认" }, 
				10 => new string[] { "快捷使用" }, 
				11 => new string[2] { "快捷使用", "关闭" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		}, new Dictionary<int, Queue<string>>
		{
			[4] = new Queue<string>(new string[] { "10" }),
			[10] = new Queue<string>(new string[] { "2" })
		});
		RestoreCharge operation = new RestoreCharge(config: new ChargePlanConfig
		{
			RestoreCharge = RestoreChargeMode.Both.DisplayName
		}, context: context, requiredCharge: 80, isMenu: true, retryDelay: TimeSpan.Zero, preClickDelay: TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("继续前往副本", result.Status);
		Assert.Equal(2, Assert.IsType<int>(result.Data));
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 350), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains((IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks, (Predicate<OneDragon.Core.Abstractions.Geometry.Point>)((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(20, 310)));
	}

	[Fact]
	public async Task RestoreCharge_InChallengeConfirmsRestoreAndRewardLayers()
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
				1 => new string[] { "下一步" }, 
				2 => new string[] { "储蓄电量" }, 
				3 => new string[] { "确认" }, 
				5 => new string[] { "确认" }, 
				6 => new string[2] { "快捷使用", "确认" }, 
				7 => new string[2] { "获得", "确认" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		}, new Dictionary<int, Queue<string>> { [4] = new Queue<string>(new string[2] { "100", "60" }) });
		ChargePlanConfig config = new ChargePlanConfig
		{
			RestoreCharge = RestoreChargeMode.BackupOnly.DisplayName
		};
		ChargePlanConfig config2 = config;
		TimeSpan? retryDelay = TimeSpan.Zero;
		TimeSpan? preClickDelay = TimeSpan.Zero;
		RestoreCharge operation = new RestoreCharge(context, null, isMenu: false, config2, retryDelay, preClickDelay);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("恢复电量成功", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 170), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 350), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}

	private ZContext CreateContext(StageController controller, Func<int, IReadOnlyList<string>> stageWords, Dictionary<int, Queue<string>>? singleLineByStage = null)
	{
		ZContext zContext = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		zContext.AttachController(controller);
		zContext.OcrService.Matcher = new StageOcrMatcher(controller, stageWords, singleLineByStage ?? new Dictionary<int, Queue<string>>());
		zContext.ScreenContext.Reload();
		return zContext;
	}

	private void WriteTeamConfig()
	{
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "config", "00"));
		File.WriteAllText(Path.Combine(_rootDirectory, "config", "00", "team.yml"), "team_list:\n  - name: 猫又队\n    auto_battle: 全配队通用\n    agent_id_list: [1011, 1021, 1031]\n  - name: 妮可队\n    auto_battle: 全配队通用\n    agent_id_list: [1041, 1051, 1061]\n  - name: 第三队\n  - name: 第四队\n  - name: 第五队\n  - name: 第六队");
	}

	private void WriteScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		File.WriteAllText(Path.Combine(buffer), "- screen_id: deploy\n  screen_name: 通用-出战\n  area_list:\n    - area_name: 按钮-出战\n      pc_rect: [10, 10, 30, 30]\n      text: 出战\n      lcs_percent: 0.8\n    - area_name: 标题-驱动盘数量已达到可拥有上限\n      pc_rect: [10, 40, 30, 60]\n      text: 驱动盘数量已达到可拥有上限\n      lcs_percent: 0.8\n    - area_name: 按钮-队员数量少-确认\n      pc_rect: [10, 70, 30, 90]\n      text: 队员数量少确认\n      lcs_percent: 0.8\n    - area_name: 按钮-等级低-确定并出战\n      pc_rect: [10, 70, 30, 90]\n      text: 确定并出战\n      lcs_percent: 0.8\n- screen_id: simulation\n  screen_name: 实战模拟室\n  area_list:\n    - area_name: 预备编队\n      pc_rect: [10, 130, 30, 150]\n      text: 预备编队\n      lcs_percent: 0.8\n    - area_name: 预备出战\n      pc_rect: [10, 160, 30, 180]\n      text: 预备出战\n      lcs_percent: 0.8\n    - area_name: 下一步\n      pc_rect: [10, 160, 30, 180]\n      text: 下一步\n      lcs_percent: 0.8\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      pc_rect: [10, 190, 30, 210]\n      text: 返回\n      lcs_percent: 0.8\n    - area_name: 关闭\n      pc_rect: [10, 370, 30, 390]\n      text: 关闭\n      lcs_percent: 0.8\n    - area_name: 文本-电量\n      pc_rect: [10, 460, 30, 480]\n      text: 电量\n      lcs_percent: 0.8\n- screen_id: noodle\n  screen_name: 拉面店\n  area_list:\n    - area_name: 拉面列表\n      pc_rect: [10, 220, 500, 250]\n    - area_name: 点单\n      pc_rect: [10, 220, 30, 240]\n      text: 点单\n      lcs_percent: 0.8\n    - area_name: 点单确认\n      pc_rect: [10, 250, 30, 270]\n      text: 点单确认\n      lcs_percent: 0.8\n    - area_name: 效果确认\n      pc_rect: [10, 280, 30, 300]\n      text: 效果确认\n      lcs_percent: 0.8\n- screen_id: coffee\n  screen_name: 咖啡店\n  area_list:\n    - area_name: 点单后跳过\n      pc_rect: [10, 310, 30, 330]\n      text: 点单后跳过\n      lcs_percent: 0.8\n- screen_id: restore\n  screen_name: 恢复电量\n  area_list:\n    - area_name: 标题-恢复电量\n      pc_rect: [10, 340, 30, 360]\n      text: 恢复电量\n      lcs_percent: 0.8\n    - area_name: 类型\n      pc_rect: [10, 400, 500, 440]\n    - area_name: 确认\n      pc_rect: [10, 340, 30, 360]\n      text: 确认\n      lcs_percent: 0.8\n    - area_name: 标题-快捷使用\n      pc_rect: [10, 430, 30, 450]\n      text: 快捷使用\n      lcs_percent: 0.8\n    - area_name: 当前数量\n      pc_rect: [10, 490, 90, 520]\n    - area_name: 兑换数量-数字输入框\n      pc_rect: [10, 530, 90, 560]\n    - area_name: 标题-获得\n      pc_rect: [10, 570, 30, 590]\n      text: 获得\n      lcs_percent: 0.8\n- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 战斗结果-再来一次\n      pc_rect: [10, 600, 30, 620]\n      text: 再来一次\n      lcs_percent: 0.8");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
