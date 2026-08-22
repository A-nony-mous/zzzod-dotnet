using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.ChallengeMission;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class ChallengeMissionPhase213Tests : IDisposable
{
	private sealed class StageController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);

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
			return string.Concat(from result in CreateResults()
				select result.Text);
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			Dictionary<string, MatchResultList> dictionary = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
			foreach (OcrMatchResult item in CreateResults())
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
			return stageWords(controller.ScreenshotStage).Select((string word, int index) => new OcrMatchResult(0.99, 4 + index * 80, 4, 60, 16, word)).ToArray();
		}
	}

	private sealed class RestorePopupOcrMatcher : IOcrMatcher
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
			return Ocr(image).Single().Text;
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(Mat image, double? threshold = null, double mergeLineDistance = -1.0)
		{
			OcrMatchResult match = Ocr(image).Single();
			MatchResultList matches = new MatchResultList(onlyBest: false);
			matches.Append(match, autoMerge: false);
			return new Dictionary<string, MatchResultList>(StringComparer.Ordinal) { [match.Text] = matches };
		}

		public IReadOnlyList<OcrMatchResult> Ocr(Mat image, double threshold = 0.0, double mergeLineDistance = -1.0)
		{
			string text = image.Width <= 20 ? "取消" : "恢复电量";
			return new OcrMatchResult[] { new OcrMatchResult(0.99, 4, 4, 12, 12, text) };
		}
	}

	private readonly string _rootDirectory;

	public ChallengeMissionPhase213Tests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-challenge-mission-213-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "assets", "game_data", "screen_info"));
		WriteScreenYaml();
	}

	[Fact]
	public async Task ExitInBattle_OpensMenuClicksExitConfirmAndWaitsTargetArea()
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
			if (stage < 6)
			{
				switch (stage)
				{
				case 1:
					result2 = Array.Empty<string>();
					break;
				case 2:
				case 3:
					result2 = new string[] { "退出战斗" };
					break;
				case 4:
				case 5:
					result2 = new string[] { "退出战斗确认" };
					break;
				default:
					result2 = Array.Empty<string>();
					break;
				}
			}
			else
			{
				result2 = new string[] { "街区" };
			}
			if (1 == 0)
			{
			}
			return result2;
		});
		ExitInBattle operation = new ExitInBattle(context, "战斗-挑战结果-失败", "按钮-退出", TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("按钮-退出", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 20), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.True(controller.Clicks.Count >= 3);
	}

	[Fact]
	public async Task RestartInBattle_OpensMenuClicksRestartAndConfirm()
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
			switch (stage)
			{
			case 1:
				result2 = Array.Empty<string>();
				break;
			case 2:
			case 3:
				result2 = new string[2] { "退出战斗", "重新开始" };
				break;
			case 4:
			case 5:
				result2 = new string[] { "退出战斗确认" };
				break;
			default:
				result2 = Array.Empty<string>();
				break;
			}
			if (1 == 0)
			{
			}
			return result2;
		});
		RestartInBattle operation = new RestartInBattle(context, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("按钮-退出战斗-确认", result.Status);
		Assert.Contains(new OneDragon.Core.Abstractions.Geometry.Point(20, 20), (IEnumerable<OneDragon.Core.Abstractions.Geometry.Point>)controller.Clicks);
		Assert.True(controller.Clicks.Count >= 3);
	}

	[Fact]
	public void ExitAndRestartInBattle_MenuClickKeepsPythonZeroPreDelay()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		ZOperation[] array = new ZOperation[2]
		{
			new ExitInBattle(context, null, null, TimeSpan.Zero, TimeSpan.FromSeconds(1L)),
			new RestartInBattle(context, TimeSpan.Zero, TimeSpan.FromSeconds(1L))
		};
		ZOperation[] array2 = array;
		foreach (ZOperation zOperation in array2)
		{
			using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
			SetLastScreenshot(zOperation, screen);
			MethodInfo method = zOperation.GetType().GetMethod("CheckScreen", BindingFlags.Instance | BindingFlags.NonPublic);
			Stopwatch stopwatch = Stopwatch.StartNew();
			OperationRoundResult operationRoundResult = Assert.IsType<OperationRoundResult>(method.Invoke(zOperation, null));
			stopwatch.Stop();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500L), $"菜单区域点击多等待了 {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
		}
	}

	[Fact]
	public void ExitInBattle_WaitTargetSuccessKeepsPythonZeroSuccessDelay()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => new string[] { "街区" });
		ExitInBattle exitInBattle = new ExitInBattle(context, "战斗-挑战结果-失败", "按钮-退出");
		using Mat screen = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
		SetLastScreenshot(exitInBattle, screen);
		MethodInfo method = typeof(ExitInBattle).GetMethod("WaitAfterExit", BindingFlags.Instance | BindingFlags.NonPublic);
		OperationRoundResult operationRoundResult = Assert.IsType<OperationRoundResult>(method.Invoke(exitInBattle, null));
		Assert.True(operationRoundResult.IsSuccess);
		Assert.Equal("按钮-退出", operationRoundResult.Status);
		Assert.Null(operationRoundResult.Delay);
	}

	[Fact]
	public async Task ChooseNextOrFinishAfterBattle_AgentPlanFinishedClicksFinishAndReturnsFinishedStatus()
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
			IReadOnlyList<string> result2 = ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[2] { "已达成", "完成" }));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(context, tryNext: true, isAgentPlan: true, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("特训目标已达成", result.Status);
		Assert.Single(controller.Clicks);
	}

	[Fact]
	public async Task ChooseNextOrFinishAfterBattle_TryNextReturnsRetryStatusWhenNoRestorePopup()
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
			IReadOnlyList<string> result2 = ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[] { "再来一次" }));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(context, tryNext: true, isAgentPlan: false, new ChargePlanConfig
		{
			RestoreCharge = RestoreChargeMode.None.DisplayName
		}, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("战斗结果-再来一次", result.Status);
	}

	[Fact]
	public async Task ChooseNextOrFinishAfterBattle_RestorePopupDisabledFallsBackToFinish()
	{
		if (!CanUseOpenCv())
		{
			return;
		}
		using StageController controller = new StageController();
		using ZContext context = CreateContext(controller, (int _) => Array.Empty<string>());
		context.OcrService.Matcher = new RestorePopupOcrMatcher();
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(context, tryNext: true, isAgentPlan: false, new ChargePlanConfig
		{
			RestoreCharge = RestoreChargeMode.None.DisplayName
		}, TimeSpan.Zero, TimeSpan.Zero);
		using Mat screenshot = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
		SetLastScreenshot(operation, screenshot);
		MethodInfo method = typeof(ChooseNextOrFinishAfterBattle).GetMethod("RestoreChargeAfterRetry", BindingFlags.Instance | BindingFlags.NonPublic);
		OperationRoundResult result = await Assert.IsType<Task<OperationRoundResult>>(method.Invoke(operation, null));
		Assert.True(result.IsSuccess, result.Status);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Single(controller.Clicks);
	}

	[Fact]
	public async Task ChooseNextOrFinishAfterBattle_ClicksFinishWhenNoNextRun()
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
			IReadOnlyList<string> result2 = ((stage != 1) ? ((IReadOnlyList<string>)Array.Empty<string>()) : ((IReadOnlyList<string>)new string[] { "完成" }));
			if (1 == 0)
			{
			}
			return result2;
		});
		ChooseNextOrFinishAfterBattle operation = new ChooseNextOrFinishAfterBattle(context, tryNext: false, isAgentPlan: false, null, TimeSpan.Zero, TimeSpan.Zero);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("战斗结果-完成", result.Status);
		Assert.Single(controller.Clicks);
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
		ScreenSeed.WriteScreens(Path.Combine(buffer[..4]), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 菜单\n      pc_rect: [10, 10, 30, 30]\n    - area_name: 战斗结果-已达成\n      pc_rect: [0, 0, 200, 40]\n      text: 已达成\n      lcs_percent: 0.8\n    - area_name: 战斗结果-再来一次\n      pc_rect: [0, 0, 200, 40]\n      text: 再来一次\n      lcs_percent: 0.8\n    - area_name: 战斗结果-完成\n      pc_rect: [0, 0, 200, 40]\n      text: 完成\n      lcs_percent: 0.8\n- screen_id: battle_menu\n  screen_name: 战斗-菜单\n  area_list:\n    - area_name: 按钮-退出战斗\n      pc_rect: [0, 0, 200, 40]\n      text: 退出战斗\n      lcs_percent: 0.8\n    - area_name: 按钮-重新开始\n      pc_rect: [0, 0, 200, 40]\n      text: 重新开始\n      lcs_percent: 0.8\n    - area_name: 按钮-退出战斗-确认\n      pc_rect: [0, 0, 200, 40]\n      text: 退出战斗确认\n      lcs_percent: 0.8\n- screen_id: restore_charge\n  screen_name: 恢复电量\n  area_list:\n    - area_name: 标题-恢复电量\n      pc_rect: [0, 0, 200, 40]\n      text: 恢复电量\n      lcs_percent: 0.8\n    - area_name: 取消\n      pc_rect: [30, 0, 50, 20]\n      text: 取消\n      lcs_percent: 0.8\n- screen_id: battle_fail_result\n  screen_name: 战斗-挑战结果-失败\n  area_list:\n    - area_name: 按钮-退出\n      pc_rect: [0, 0, 200, 40]\n      text: 街区\n      lcs_percent: 0.8");
	}

	private static bool CanUseOpenCv()
	{
		OpenCvTestRuntime.RequireAvailable();
		return true;
	}

	private static void SetLastScreenshot(ZOperation operation, Mat screen)
	{
		PropertyInfo property = typeof(ZOperation).GetProperty("LastScreenshot", BindingFlags.Instance | BindingFlags.NonPublic);
		property.SetValue(operation, screen);
	}
}
