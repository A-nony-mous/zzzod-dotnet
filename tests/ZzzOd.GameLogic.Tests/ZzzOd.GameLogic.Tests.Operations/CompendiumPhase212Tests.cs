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
using OneDragon.Core.Utils;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations.Compendium;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class CompendiumPhase212Tests : IDisposable
{
	private sealed record ControllerAction(string Name, TimeSpan? PressTime);

	private sealed class StageController : ControllerBase, IZzzControllerActions, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);

		public int ScreenshotStage { get; private set; }

		public List<OneDragon.Core.Abstractions.Geometry.Point> Clicks { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<OneDragon.Core.Abstractions.Geometry.Point> MouseMoves { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public List<(OneDragon.Core.Abstractions.Geometry.Point Start, OneDragon.Core.Abstractions.Geometry.Point End)> Drags { get; } = new List<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point)>();

		public List<ControllerAction> Actions { get; } = new List<ControllerAction>();

		public List<float> TurnDistances { get; } = new List<float>();

		public StageController(bool includeAgentPlanAvatar = false)
		{
			if (includeAgentPlanAvatar)
			{
				Cv2.Rectangle(_screenshot, new OpenCvSharp.Rect(0, 40, 40, 40), new Scalar(0.0, 0.0, 255.0), -1);
			}
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
			Drags.Add((start.GetValueOrDefault(), end));
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
			MouseMoves.Add(position);
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
			bool fullScreen = image.Width >= 320 && image.Height >= 240;
			return CreateResults(fullScreen);
		}

		private IReadOnlyList<OcrMatchResult> CreateResults(bool fullScreen)
		{
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => fullScreen ? new OcrMatchResult(0.99, 4 + index * 40, 4, 30, 12, word) : new OcrMatchResult(0.99, 4, 6, 12, 8, word)).ToArray();
		}
	}

	private sealed class StageMoveDetector(StageController controller, Func<int, NotoriousHuntDistanceHint?> hintByStage) : INotoriousHuntMoveDetector
	{
		public bool Initialized { get; private set; }

		public void Initialize()
		{
			Initialized = true;
		}

		public NotoriousHuntDistanceHint? DetectDistanceHint(Mat screen)
		{
			return hintByStage(controller.ScreenshotStage);
		}
	}

	private sealed class RecordingBattleFlow : IChallengeBattleFlow
	{
		private readonly Queue<OperationResult> _results;

		public List<string> AutoBattleNames { get; } = new List<string>();

		public RecordingBattleFlow(params OperationResult[] results)
		{
			_results = new Queue<OperationResult>((results.Length != 0) ? ((IEnumerable<OperationResult>)results) : ((IEnumerable<OperationResult>)new OperationResult[1]
			{
				new OperationResult(IsSuccess: true, "普通战斗-完成")
			}));
		}

		public OperationResult CheckBattleState(ZContext context, ChargePlanItem plan, string autoBattleName, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			AutoBattleNames.Add(autoBattleName);
			return (_results.Count > 1) ? _results.Dequeue() : _results.Peek();
		}
	}

	private readonly string _rootDirectory;

	public CompendiumPhase212Tests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-compendium-212-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data"));
		WriteScreenYaml();
		WriteCompendiumData();
	}

	[Fact]
	public async Task TransportByCompendium_NormalizesCustomTemplateAndGoesToTabScreen()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		List<string> calls = new List<string>();
		TransportByCompendium operation = new TransportByCompendium(context, "训练", "实战模拟室", "自定义模板", delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, null, null, delegate(ZContext _, string category)
		{
			calls.Add("category:" + category);
			return Task.FromResult(new OperationResult(IsSuccess: true, category));
		}, delegate(ZContext _, CompendiumMissionType missionType)
		{
			calls.Add("mission:" + missionType.MissionTypeName);
			return Task.FromResult(new OperationResult(IsSuccess: true, missionType.MissionTypeName));
		}, delegate(ZContext _, string tab)
		{
			calls.Add("goto:" + tab);
			return Task.FromResult(new OperationResult(IsSuccess: true, "快捷手册-" + tab));
		});
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal<List<string>>(new List<string>(4) { "back", "goto:训练", "category:实战模拟室", "mission:基础材料" }, calls);
		Assert.Equal("基础材料", operation.MissionTypeName);
	}

	[Fact]
	public async Task TransportByCompendium_AlreadyOnTargetTabSkipsBackToWorld()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int stage) => stage == 1 ? new string[] { "快捷手册训练" } : Array.Empty<string>());
		List<string> calls = new List<string>();
		TransportByCompendium operation = new TransportByCompendium(context, "训练", "实战模拟室", "基础材料", delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, null, null, delegate(ZContext _, string category)
		{
			calls.Add("category:" + category);
			return Task.FromResult(new OperationResult(IsSuccess: true, category));
		}, delegate(ZContext _, CompendiumMissionType missionType)
		{
			calls.Add("mission:" + missionType.MissionTypeName);
			return Task.FromResult(new OperationResult(IsSuccess: true, missionType.MissionTypeName));
		}, delegate(ZContext _, string tab)
		{
			calls.Add("goto:" + tab);
			return Task.FromResult(new OperationResult(IsSuccess: true, "快捷手册-" + tab));
		});
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal<List<string>>(new List<string>(3) { "goto:训练", "category:实战模拟室", "mission:基础材料" }, calls);
	}

	[Fact]
	public async Task TransportByCompendium_MissingMissionTypeSkipsMissionSelection()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		List<string> calls = new List<string>();
		TransportByCompendium operation = new TransportByCompendium(context, "训练", "实战模拟室", null, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界"));
		}, null, null, delegate(ZContext _, string category)
		{
			calls.Add("category:" + category);
			return Task.FromResult(new OperationResult(IsSuccess: true, category));
		}, delegate(ZContext _, CompendiumMissionType missionType)
		{
			calls.Add("mission:" + missionType.MissionTypeName);
			return Task.FromResult(new OperationResult(IsSuccess: true, missionType.MissionTypeName));
		}, delegate(ZContext _, string tab)
		{
			calls.Add("goto:" + tab);
			return Task.FromResult(new OperationResult(IsSuccess: true, "快捷手册-" + tab));
		});
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal<List<string>>(new List<string>(3) { "back", "goto:训练", "category:实战模拟室" }, calls);
		Assert.Equal("无需选择副本", result.Status);
	}

	[Fact]
	public async Task Coupon_ConfirmUseCouponIncrementsPlanRunTimesAndClosesPopup()
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
				1 => new string[] { "使用" }, 
				2 => new string[] { "确认" }, 
				3 => new string[] { "绳网信用" }, 
				4 => new string[] { "使用" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			PlanTimes = 1,
			RunTimes = 0
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		Coupon operation = new Coupon(context, plan, config, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal(1, plan.RunTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 20), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 50), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(1500, 200), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task Coupon_MissingConfirmContinuesWithChargeWhenPlanStillNeedsRuns()
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
			IReadOnlyList<string> result2 = ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[] { "使用" }));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			PlanTimes = 2,
			RunTimes = 0
		};
		Coupon operation = new Coupon(context, plan, new ChargePlanConfig(), TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("继续使用电量", result.Status);
		Assert.Equal(0, plan.RunTimes);
	}

	[Fact]
	public async Task NotoriousHuntMove_TurnsMovesInteractsChoosesBuffAndStopsAtBossBar()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		StageMoveDetector detector = new StageMoveDetector(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			NotoriousHuntDistanceHint result2 = stage switch
			{
				2 => new NotoriousHuntDistanceHint(new OneDragon.Core.Abstractions.Geometry.Point(740, 100), 0.0), 
				3 => new NotoriousHuntDistanceHint(new OneDragon.Core.Abstractions.Geometry.Point(960, 100), 7.2), 
				_ => null, 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = stage switch
			{
				3 => new string[] { "7.2m" }, 
				4 => new string[] { "交互" }, 
				6 => new string[3] { "选择", "选择", "选择" }, 
				7 => new string[] { "普通攻击" }, 
				8 => new string[] { "BOSS血条" }, 
				_ => Array.Empty<string>(), 
			};
			if (1 == 0)
			{
			}
			return result2;
		});
		NotoriousHuntMove operation = new NotoriousHuntMove(context, 2, detector, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Contains((IEnumerable<ControllerAction>)controller.Actions, (Predicate<ControllerAction>)((ControllerAction action) => action.Name == "MoveW" && action.PressTime.HasValue && Math.Abs((action.PressTime.Value - TimeSpan.FromSeconds(1L)).TotalMilliseconds) < 1.0));
		Assert.Contains((IEnumerable<ControllerAction>)controller.Actions, (Predicate<ControllerAction>)((ControllerAction action) => action.Name == "Interact"));
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(59, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.True(detector.Initialized);
	}

	[Fact]
	public void NotoriousHuntMove_DefaultDetectorReusesLostVoidDetectorWithoutOwningItsLifetime()
	{
		using StageController controller = new StageController();
		using ZContext zContext = CreateContext(controller, (int _) => Array.Empty<string>());
		zContext.LostVoid.InitLostVoidDetectorModel();
		LostVoidDetector lostVoidDetector = Assert.IsType<LostVoidDetector>(zContext.LostVoid.Detector);
		using DefaultNotoriousHuntMoveDetector defaultNotoriousHuntMoveDetector = new DefaultNotoriousHuntMoveDetector(zContext);
		Assert.Same(lostVoidDetector.CoreDetector, defaultNotoriousHuntMoveDetector.CoreDetector);
		defaultNotoriousHuntMoveDetector.Dispose();
		Assert.False(lostVoidDetector.IsShutdown);
	}

	[Fact]
	public async Task AreaPatrol_UsesSharedChallengeFlowAndBattleFacade()
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
			IReadOnlyList<string> result2 = ((stage >= 4) ? new string[2] { "普通攻击", "完成" } : (stage switch
			{
				1 => new string[] { "挑战等级" }, 
				2 => new string[] { "下一步" }, 
				3 => new string[] { "出战" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "区域巡防",
			MissionTypeName = "铁律与狂徒",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BattleFlow = new RecordingBattleFlow()
		};
		AreaPatrol operation = new AreaPatrol(context, plan, config, services, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Equal(1, plan.RunTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 80), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(1866, 1069), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.MouseMoves);
		Assert.Equal<List<string>>(actual: Assert.IsType<RecordingBattleFlow>(services.BattleFlow).AutoBattleNames, expected: new List<string>(1) { "全配队通用" });
	}

	[Fact]
	public async Task AreaPatrol_RetryRunsAgainBeforeFinishing()
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
			IReadOnlyList<string> result2 = ((stage >= 4) ? new string[3] { "普通攻击", "再来一次", "完成" } : (stage switch
			{
				1 => new string[] { "挑战等级" }, 
				2 => new string[] { "下一步" }, 
				3 => new string[] { "出战" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "区域巡防",
			MissionTypeName = "铁律与狂徒",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 2,
			RunTimes = 0,
			PredefinedTeamIndex = -1
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BattleFlow = new RecordingBattleFlow(new OperationResult(IsSuccess: true, "普通战斗-完成"), new OperationResult(IsSuccess: true, "普通战斗-完成"))
		};
		AreaPatrol operation = new AreaPatrol(context, plan, config, services, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Equal(2, plan.RunTimes);
		Assert.Equal<List<string>>(actual: Assert.IsType<RecordingBattleFlow>(services.BattleFlow).AutoBattleNames, expected: new List<string>(2) { "全配队通用", "全配队通用" });
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 130), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task CombatSimulation_SelectsMissionTypeMissionAndCardNumBeforeSharedChallengeFlow()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController(includeAgentPlanAvatar: true);
		using ZContext context = CreateContext(controller, delegate(int stage)
		{
			if (1 == 0)
			{
			}
			IReadOnlyList<string> result2 = ((stage >= 2) ? new string[7] { "代理人方案培养", "防护演练", "保存方案", "下一步", "出战", "普通攻击", "完成" } : ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[3] { "基础材料", "代理人方案培养", "驱动盘" })));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "实战模拟室",
			MissionTypeName = "代理人方案培养",
			MissionName = "防护演练",
			CardNum = "2",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BattleFlow = new RecordingBattleFlow()
		};
		CombatSimulation operation = new CombatSimulation(context, plan, config, services, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Equal(1, plan.RunTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(10, 10), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 140), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 190), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Equal(5, controller.Clicks.Count((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(20, 250)));
		Assert.Equal(2, controller.Clicks.Count((OneDragon.Core.Abstractions.Geometry.Point point) => point == new OneDragon.Core.Abstractions.Geometry.Point(20, 280)));
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 220), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task CombatSimulation_UsesPythonGameTextAndDifflibForTranslatedMissionSelection()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteEnglishGameText();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[8] { "Basic Materials", "Agent Training", "Drive Disc", "Investigation Special", "Next", "Deploy", "Normal Attack", "Finish" });
		context.GameAccountConfig.GameLanguage = "en";
		Assert.Equal("Basic Materials", context.GameTextResolver("基础材料"));
		Assert.Equal(0, StringUtils.FindBestMatchByDifflib("Basic Materials", new string[] { "Basic Materials" }, 0.5));
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "实战模拟室",
			MissionTypeName = "基础材料",
			MissionName = "调查专项",
			CardNum = "默认数量",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			PredefinedTeamIndex = -1
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BattleFlow = new RecordingBattleFlow()
		};
		CombatSimulation operation = new CombatSimulation(context, plan, config, services, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Equal(1, plan.RunTimes);
	}

	[Fact]
	public async Task ExpertChallenge_ClosesBurnoutModeBeforeSharedChallengeFlow()
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
			IReadOnlyList<string> result = ((stage >= 7) ? new string[2] { "普通攻击", "完成" } : (stage switch
			{
				1 => new string[] { "挑战等级" }, 
				2 => new string[] { "深度追猎ON" }, 
				3 => new string[] { "深度追猎确认" }, 
				5 => new string[] { "下一步" }, 
				6 => new string[] { "出战" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "专业挑战室",
			MissionTypeName = "牲鬼",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1
		};
		ExpertChallenge operation = new ExpertChallenge(services: new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BattleFlow = new RecordingBattleFlow()
		}, context: context, plan: plan, config: new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		}, retryDelay: TimeSpan.Zero, preClickDelay: TimeSpan.Zero);
		Assert.True((await operation.ExecuteAsync()).IsSuccess);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 340), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 310), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Fact]
	public async Task NotoriousHunt_UsesChargePowerDepthHuntMoveAndBattleFlow()
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
			IReadOnlyList<string> result2;
			if (stage < 8)
			{
				switch (stage)
				{
				case 1:
					result2 = new string[2] { "当期剩余奖励次数", "猎血清道夫" };
					break;
				case 2:
					result2 = new string[] { "猎血清道夫" };
					break;
				case 3:
				case 4:
					result2 = new string[2] { "深度追猎信息", "无报酬模式" };
					break;
				case 5:
				case 6:
					result2 = new string[2] { "深度追猎信息", "深度追猎ON" };
					break;
				case 7:
					result2 = new string[] { "下一步" };
					break;
				default:
					result2 = Array.Empty<string>();
					break;
				}
			}
			else
			{
				result2 = new string[5] { "出战", "普通攻击", "完成", "街区", "剩余奖励次数" };
			}
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "恶名狩猎",
			MissionTypeName = "猎血清道夫",
			Level = "默认等级",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1,
			NotoriousHuntBuffNum = 2
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BeforeBattleMoveAsync = (ZContext _, ChargePlanItem item) => Task.FromResult(new OperationResult(IsSuccess: true, $"战前移动-{item.NotoriousHuntBuffNum}")),
			BattleFlow = new RecordingBattleFlow()
		};
		NotoriousHunt operation = new NotoriousHunt(context, plan, new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		}, services, useChargePower: true, null, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Null(result.Status);
		Assert.Equal(1, plan.RunTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(10, 190), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 340), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Equal<List<string>>(actual: Assert.IsType<RecordingBattleFlow>(services.BattleFlow).AutoBattleNames, expected: new List<string>(1) { "全配队通用" });
	}

	[Fact]
	public async Task NotoriousHunt_UsesPythonGameTextForTranslatedMissionName()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		WriteEnglishGameText();
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[9] { "Current Reward", "Blood Cleaner", "Depth Hunt Information", "Depth Hunt On", "Next", "Deploy", "Normal Attack", "Finish", "Street" });
		context.GameAccountConfig.GameLanguage = "en";
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "恶名狩猎",
			MissionTypeName = "猎血清道夫",
			Level = "默认等级",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			PredefinedTeamIndex = -1
		};
		ChargePlanConfig config = new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BeforeBattleMoveAsync = (ZContext _, ChargePlanItem _) => Task.FromResult(new OperationResult(IsSuccess: true, "战前移动")),
			BattleFlow = new RecordingBattleFlow()
		};
		NotoriousHunt operation = new NotoriousHunt(context, plan, config, services, useChargePower: true, new NotoriousHuntConfig(), new NotoriousHuntRunRecord());
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Null(result.Status);
		Assert.Equal(1, plan.RunTimes);
	}

	[Fact]
	public async Task NotoriousHunt_RewindRestartsBattleAndThenFinishes()
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
			IReadOnlyList<string> result2 = ((stage >= 5) ? new string[6] { "出战", "普通攻击", "倒带", "完成", "街区", "剩余奖励次数" } : (stage switch
			{
				1 => new string[2] { "当期剩余奖励次数", "猎血清道夫" }, 
				2 => new string[] { "猎血清道夫" }, 
				3 => new string[] { "2" }, 
				4 => new string[] { "下一步" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "恶名狩猎",
			MissionTypeName = "猎血清道夫",
			Level = "默认等级",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1,
			NotoriousHuntBuffNum = 2
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BeforeBattleMoveAsync = (ZContext _, ChargePlanItem item) => Task.FromResult(new OperationResult(IsSuccess: true, $"战前移动-{item.NotoriousHuntBuffNum}")),
			BattleFlow = new RecordingBattleFlow(new OperationResult(IsSuccess: true, "普通战斗-撤退"), new OperationResult(IsSuccess: true, "普通战斗-完成"))
		};
		NotoriousHuntConfig huntConfig = new NotoriousHuntConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		NotoriousHuntRunRecord runRecord = new NotoriousHuntRunRecord(huntConfig)
		{
			LeftTimes = 2
		};
		NotoriousHunt operation = new NotoriousHunt(context, plan, new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		}, services, useChargePower: false, huntConfig, runRecord, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess, result.Status);
		Assert.Null(result.Status);
		Assert.Equal(1, plan.RunTimes);
		Assert.Equal(1, runRecord.LeftTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 190), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Equal<List<string>>(actual: Assert.IsType<RecordingBattleFlow>(services.BattleFlow).AutoBattleNames, expected: new List<string>(2) { "全配队通用", "全配队通用" });
	}

	[Fact]
	public async Task NotoriousHunt_RetreatClicksExitAndFails()
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
			IReadOnlyList<string> result2 = ((stage >= 5) ? new string[4] { "出战", "普通攻击", "撤退", "退出" } : (stage switch
			{
				1 => new string[2] { "当期剩余奖励次数", "猎血清道夫" }, 
				2 => new string[] { "猎血清道夫" }, 
				3 => new string[] { "1" }, 
				4 => new string[] { "下一步" }, 
				_ => Array.Empty<string>(), 
			}));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChargePlanItem plan = new ChargePlanItem
		{
			CategoryName = "恶名狩猎",
			MissionTypeName = "猎血清道夫",
			Level = "默认等级",
			AutoBattleConfig = "全配队通用",
			PlanTimes = 1,
			RunTimes = 0,
			PredefinedTeamIndex = -1,
			NotoriousHuntBuffNum = 2
		};
		ChallengeMissionServices services = new ChallengeMissionServices
		{
			InitializeAutoBattle = (ZContext _, ChargePlanItem _, string _) => new OperationResult(IsSuccess: true, "加载自动战斗指令"),
			DeployAsync = (ZContext _) => Task.FromResult(new OperationResult(IsSuccess: true, "出战")),
			BeforeBattleMoveAsync = (ZContext _, ChargePlanItem item) => Task.FromResult(new OperationResult(IsSuccess: true, $"战前移动-{item.NotoriousHuntBuffNum}")),
			BattleFlow = new RecordingBattleFlow(new OperationResult(IsSuccess: true, "普通战斗-撤退"))
		};
		NotoriousHuntConfig huntConfig = new NotoriousHuntConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		};
		NotoriousHuntRunRecord runRecord = new NotoriousHuntRunRecord(huntConfig)
		{
			LeftTimes = 1
		};
		NotoriousHunt operation = new NotoriousHunt(context, plan, new ChargePlanConfig
		{
			PlanList = new List<ChargePlanItem>(1) { plan }
		}, services, useChargePower: false, huntConfig, runRecord, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("战斗结果-退出", result.Status);
		Assert.Equal(0, plan.RunTimes);
		Assert.Equal(1, runRecord.LeftTimes);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 220), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 250), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
	}

	[Theory]
	[InlineData(new object[] { 759, -100 })]
	[InlineData(new object[] { 760, -50 })]
	[InlineData(new object[] { 860, -25 })]
	[InlineData(new object[] { 910, null })]
	[InlineData(new object[] { 1010, null })]
	[InlineData(new object[] { 1011, 25 })]
	[InlineData(new object[] { 1061, 50 })]
	[InlineData(new object[] { 1161, 100 })]
	public void NotoriousHuntMove_ResolveTurnDistanceUsesPythonBoundaries(int x, int? expected)
	{
		float? expected2 = (expected.HasValue ? new float?(expected.Value) : ((float?)null));
		Assert.Equal(expected2, NotoriousHuntMove.ResolveTurnDistance(x));
	}

	[Fact]
	public void AutoBattleDistance_UsesPythonLastParsedValueAndPositiveFloatCleaning()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext zContext = CreateContext(controller, (int _) => new string[2] { "1.0m", "噪声1x2.5m" });
		zContext.ProjectConfig.ScreenStandardWidth = 0;
		using Mat screen = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
		float? actual = zContext.AutoBattleContext.CheckBattleDistance(screen);
		Assert.Equal(12.5f, actual);
		Assert.Equal(12.5f, zContext.AutoBattleContext.LastCheckDistance);
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
		zContext.CompendiumService.Reload();
		return zContext;
	}

	private void WriteCompendiumData()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "assets", "game_data", "compendium_data.yml"), "- tab_name: 训练\n  category_list:\n    - category_name: 实战模拟室\n      mission_type_list:\n        - mission_type_name: 基础材料\n          mission_list:\n            - mission_name: 调查专项\n        - mission_type_name: 代理人方案培养\n          mission_list:\n            - mission_name: 防护演练\n        - mission_type_name: 驱动盘\n    - category_name: 区域巡防\n      mission_type_list:\n        - mission_type_name: 铁律与狂徒\n    - category_name: 恶名狩猎\n      mission_type_list:\n        - mission_type_name: 猎血清道夫");
	}

	private void WriteEnglishGameText()
	{
		string text = Path.Combine(_rootDirectory, "assets", "text", "game");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "en.po"), "msgid \"基础材料\"\nmsgstr \"Basic Materials\"\n\nmsgid \"代理人方案培养\"\nmsgstr \"Agent Training\"\n\nmsgid \"驱动盘\"\nmsgstr \"Drive Disc\"\n\nmsgid \"调查专项\"\nmsgstr \"Investigation Special\"\n\nmsgid \"下一步\"\nmsgstr \"Next\"\n\nmsgid \"出战\"\nmsgstr \"Deploy\"\n\nmsgid \"普通攻击\"\nmsgstr \"Normal Attack\"\n\nmsgid \"完成\"\nmsgstr \"Finish\"\n\nmsgid \"猎血清道夫\"\nmsgstr \"Blood Cleaner\"\n\nmsgid \"当期剩余奖励次数\"\nmsgstr \"Current Reward\"\n\nmsgid \"深度追猎信息\"\nmsgstr \"Depth Hunt Information\"\n\nmsgid \"深度追猎ON\"\nmsgstr \"Depth Hunt On\"\n\nmsgid \"街区\"\nmsgstr \"Street\"");
	}

	private void WriteScreenYaml()
	{
		string[] buffer = new string[5];
		buffer[0] = _rootDirectory;
		buffer[1] = "assets";
		buffer[2] = "game_data";
		buffer[3] = "screen_info";
		buffer[4] = "_od_merged.yml";
		File.WriteAllText(Path.Combine(buffer), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 按键-交互\n      pc_rect: [10, 10, 30, 30]\n      text: 交互\n      lcs_percent: 0.8\n    - area_name: 按键-普通攻击\n      pc_rect: [10, 40, 30, 60]\n      text: 普通攻击\n      lcs_percent: 0.8\n    - area_name: 战斗结果-完成\n      pc_rect: [10, 90, 30, 110]\n      text: 完成\n      lcs_percent: 0.8\n    - area_name: 战斗结果-再来一次\n      pc_rect: [10, 120, 30, 140]\n      text: 再来一次\n      lcs_percent: 0.8\n    - area_name: 战斗结果-已达成\n      pc_rect: [10, 150, 30, 170]\n      text: 已达成\n      lcs_percent: 0.8\n    - area_name: 战斗结果-倒带\n      pc_rect: [10, 180, 30, 200]\n      text: 倒带\n      lcs_percent: 0.8\n    - area_name: 战斗结果-撤退\n      pc_rect: [10, 210, 30, 230]\n      text: 撤退\n      lcs_percent: 0.8\n    - area_name: 战斗结果-退出\n      pc_rect: [10, 240, 30, 260]\n      text: 退出\n      lcs_percent: 0.8\n    - area_name: 距离显示区域\n      pc_rect: [432, 132, 1640, 842]\n- screen_id: compendium_training\n  screen_name: 快捷手册-训练\n  area_list:\n    - area_name: 标题\n      pc_rect: [0, 0, 300, 40]\n      text: 快捷手册训练\n      lcs_percent: 0.8\n- screen_id: notorious\n  screen_name: 恶名狩猎\n  area_list:\n    - area_name: 标识-BOSS血条\n      pc_rect: [10, 70, 30, 90]\n      text: BOSS血条\n      lcs_percent: 0.8\n    - area_name: 当期剩余奖励次数\n      pc_rect: [0, 0, 300, 40]\n      text: 当期剩余奖励次数\n      lcs_percent: 0.8\n    - area_name: 按钮-街区\n      pc_rect: [10, 280, 30, 300]\n      text: 街区\n      lcs_percent: 0.8\n    - area_name: 标题-副本名称\n      pc_rect: [0, 80, 300, 120]\n    - area_name: 副本名称列表\n      pc_rect: [0, 80, 300, 130]\n    - area_name: 深度追猎-信息\n      pc_rect: [0, 0, 300, 40]\n      text: 深度追猎信息\n      lcs_percent: 0.8\n    - area_name: 剩余次数\n      pc_rect: [0, 0, 300, 40]\n    - area_name: 按钮-深度追猎-确认\n      pc_rect: [10, 300, 30, 320]\n      text: 深度追猎确认\n      lcs_percent: 0.8\n    - area_name: 按钮-深度追猎-ON\n      pc_rect: [10, 330, 30, 350]\n      text: 深度追猎ON\n      lcs_percent: 0.8\n    - area_name: 按钮-无报酬模式\n      pc_rect: [10, 360, 30, 380]\n      text: 无报酬模式\n      lcs_percent: 0.8\n    - area_name: 难度选择入口\n      pc_rect: [10, 390, 30, 410]\n    - area_name: 难度选择区域\n      pc_rect: [0, 400, 300, 440]\n- screen_id: simulation\n  screen_name: 实战模拟室\n  area_list:\n    - area_name: 挑战等级\n      pc_rect: [10, 100, 30, 120]\n      text: 挑战等级\n      lcs_percent: 0.8\n    - area_name: 下一步\n      pc_rect: [10, 70, 30, 90]\n      text: 下一步\n      lcs_percent: 0.8\n    - area_name: 出战\n      pc_rect: [10, 130, 30, 150]\n      text: 出战\n      lcs_percent: 0.8\n    - area_name: 副本类型列表\n      pc_rect: [0, 0, 300, 40]\n    - area_name: 副本名称列表\n      pc_rect: [0, 40, 300, 80]\n    - area_name: 副本名称列表顶部\n      pc_rect: [0, 40, 300, 80]\n    - area_name: 外层-卡片1\n      pc_rect: [10, 180, 30, 200]\n    - area_name: 保存方案\n      pc_rect: [10, 210, 30, 230]\n      text: 保存方案\n      lcs_percent: 0.8\n    - area_name: 内层-已选择卡片1\n      pc_rect: [10, 240, 30, 260]\n    - area_name: 内层-卡片1\n      pc_rect: [10, 270, 30, 290]\n- screen_id: coupon\n  screen_name: 家政券\n  area_list:\n    - area_name: 使用\n      pc_rect: [10, 10, 30, 30]\n      text: 使用\n      lcs_percent: 0.8\n    - area_name: 确认\n      pc_rect: [10, 40, 30, 60]\n      text: 确认\n      lcs_percent: 0.8\n    - area_name: 绳网信用\n      pc_rect: [10, 70, 30, 90]\n      text: 绳网信用\n      lcs_percent: 0.8\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      pc_rect: [10, 160, 30, 180]\n      text: 返回\n      lcs_percent: 0.8");
		File.AppendAllText(Path.Combine(buffer), "\n- screen_id: compendium_training_test\n  screen_name: 快捷手册-训练\n  area_list:\n    - area_name: 标题\n      pc_rect: [0, 0, 300, 40]\n      text: 快捷手册训练\n      lcs_percent: 0.8\n      id_mark: true");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}
}
