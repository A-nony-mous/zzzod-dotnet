using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Operations;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class IntelBoardAppTests
{
	private sealed class RecordingIntelBoardFlow : IIntelBoardAppFlow
	{
		public int RunCount { get; private set; }

		public int PauseCount { get; private set; }

		public int ResumeCount { get; private set; }

		public int StopCount { get; private set; }

		public IntelBoardRunRecord? RunRecord { get; private set; }

		public bool IntelBoardScopeWasActive { get; private set; }

		public bool OtherApplicationScreenWasActive { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, IntelBoardConfig config, IntelBoardRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			RunRecord = runRecord;
			IntelBoardScopeWasActive = context.ScreenContext.ActiveScreenNames?.Contains("情报板") ?? false;
			OtherApplicationScreenWasActive = context.ScreenContext.ActiveScreenNames?.Contains("其他应用画面") ?? false;
			return Task.FromResult(new OperationResult(IsSuccess: true, "完成"));
		}

		public void Pause(ZContext context)
		{
			PauseCount++;
		}

		public void Resume(ZContext context)
		{
			ResumeCount++;
		}

		public void Stop(ZContext context)
		{
			StopCount++;
		}
	}

	private sealed class RecordingIntelBoardServices : IIntelBoardOperationServices
	{
		public IntelBoardCommissionType? CommissionType { get; set; }

		public Queue<IntelBoardCommissionType?> CommissionTypes { get; } = new Queue<IntelBoardCommissionType?>();

		public string? ProgressText { get; set; }

		public Queue<string?> ProgressTexts { get; } = new Queue<string>();

		public bool BattleScreenReady { get; set; }

		public bool DetectorLoadSucceeds { get; set; } = true;

		public bool DetectorLoadCalled { get; private set; }

		public bool BackToList { get; set; }

		public string AutoBattleName { get; set; } = "测试模板";

		public OperationResult ResetFilterResult { get; set; } = new OperationResult(IsSuccess: true, "重置");

		public OperationResult AcceptCommissionResult { get; set; } = new OperationResult(IsSuccess: true, "接取委托");

		public OperationResult NextStepResult { get; set; } = new OperationResult(IsSuccess: true, "预备编队");

		public Queue<OperationResult> AcceptCommissionResults { get; } = new Queue<OperationResult>();

		public List<int> ChosenTeams { get; } = new List<int>();

		public bool AutoBattleLoaded { get; private set; }

		public bool AutoBattleStarted { get; private set; }

		public int ReadProgressCallCount { get; private set; }

		public bool LoadLostVoidDetectorModel(ZContext context)
		{
			DetectorLoadCalled = true;
			return DetectorLoadSucceeds;
		}

		public Task<OperationResult> BackToVideoStoreAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回录像店"));
		}

		public Task<OperationResult> OpenBoardAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "功能导览"));
		}

		public Task<OperationResult> ClickBoardAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "情报板"));
		}

		public Task<OperationResult> RefreshCommissionAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "刷新按钮"));
		}

		public Task<OperationResult> OpenFilterAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "筛选按钮"));
		}

		public Task<OperationResult> ResetFilterAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(ResetFilterResult);
		}

		public Task<OperationResult> SelectCommissionTypeAsync(ZContext context, IntelBoardCommissionType commissionType, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, commissionType.ToDisplayName()));
		}

		public Task<OperationResult> CloseFilterAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "关闭筛选"));
		}

		public Task<IntelBoardCommissionType?> FindCommissionAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult((CommissionTypes.Count > 0) ? CommissionTypes.Dequeue() : CommissionType);
		}

		public Task ScrollCommissionListAsync(ZContext context)
		{
			return Task.CompletedTask;
		}

		public Task<OperationResult> AcceptCommissionAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult((AcceptCommissionResults.Count > 0) ? AcceptCommissionResults.Dequeue() : AcceptCommissionResult);
		}

		public Task<OperationResult> NextStepAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(NextStepResult);
		}

		public Task<OperationResult> ConfirmAcceptFailedAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "确认"));
		}

		public Task<OperationResult> ChooseTeamAsync(ZContext context, int teamIndex)
		{
			ChosenTeams.Add(teamIndex);
			return Task.FromResult(new OperationResult(IsSuccess: true, "选择预备编队"));
		}

		public Task<OperationResult> DeployAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "出战"));
		}

		public Task<OperationResult> ConfirmCommissionAgentAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "无弹窗"));
		}

		public void InitAutoBattle(ZContext context, IntelBoardConfig config)
		{
			AutoBattleLoaded = true;
		}

		public OperationResult CheckBattleScreenReady(ZContext context, Mat? screen)
		{
			return BattleScreenReady ? new OperationResult(IsSuccess: true, "按键-普通攻击") : new OperationResult(IsSuccess: false, "未找到 按键-交互");
		}

		public Task<OperationResult> PreBattleMoveAsync(ZContext context, IntelBoardCommissionType? commissionType)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, commissionType?.ToDisplayName()));
		}

		public void StartAutoBattle(ZContext context)
		{
			AutoBattleStarted = true;
		}

		public Task<OperationResult> RunBattleAsync(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战斗结束-完成"));
		}

		public Task<OperationResult> CheckBackToListAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(BackToList, BackToList ? "周期内可获取" : "未回到列表"));
		}

		public Task<OperationResult> ClickSettlementButtonAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "完成"));
		}

		public Task<OperationResult> ReadProgressAsync(ZContext context, Mat? screen)
		{
			ReadProgressCallCount++;
			string text = ((ProgressTexts.Count > 0) ? ProgressTexts.Dequeue() : ProgressText);
			int current;
			return Task.FromResult(IntelBoardOperation.TryParseProgress(text, out current) ? new OperationResult(IsSuccess: true, null, current) : new OperationResult(IsSuccess: false, "解析进度文本失败"));
		}
	}

	private sealed class ScriptedIntelBoardServices : IIntelBoardOperationServices
	{
		private readonly Queue<int> _progress = new Queue<int>(new int[2] { 0, 1000 });

		private readonly Queue<IntelBoardCommissionType?> _commissions = new Queue<IntelBoardCommissionType?>(new IntelBoardCommissionType?[3]
		{
			null,
			IntelBoardCommissionType.NotoriousHunt,
			IntelBoardCommissionType.ExpertChallenge
		});

		private readonly Queue<OperationResult> _acceptResults = new Queue<OperationResult>(new OperationResult[3]
		{
			new OperationResult(IsSuccess: true, "接取委托"),
			new OperationResult(IsSuccess: true, "委托代行中"),
			new OperationResult(IsSuccess: true, "委托代行中")
		});

		private readonly Queue<OperationResult> _nextResults = new Queue<OperationResult>(new OperationResult[2]
		{
			new OperationResult(IsSuccess: true, "接取失败"),
			new OperationResult(IsSuccess: true, "预备编队")
		});

		private readonly Queue<OperationResult> _battleResults = new Queue<OperationResult>(new OperationResult[2]
		{
			new OperationResult(IsSuccess: false, "自动战斗中"),
			new OperationResult(IsSuccess: true, "战斗结束-完成")
		});

		private readonly Queue<bool> _backToListResults = new Queue<bool>(new bool[2] { false, true });

		public List<string> Calls { get; } = new List<string>();

		public List<int> ChosenTeams { get; } = new List<int>();

		public int ScrollCount { get; private set; }

		public int SettlementClickCount { get; private set; }

		public int BackToListCount { get; private set; }

		public bool LoadLostVoidDetectorModel(ZContext context)
		{
			Calls.Add("init_detector");
			return true;
		}

		public Task<OperationResult> BackToVideoStoreAsync(ZContext context)
		{
			Calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回录像店"));
		}

		public Task<OperationResult> OpenBoardAsync(ZContext context, Mat? screen)
		{
			Calls.Add("open_board");
			return Task.FromResult(new OperationResult(IsSuccess: true, "功能导览"));
		}

		public Task<OperationResult> ClickBoardAsync(ZContext context, Mat? screen)
		{
			Calls.Add("click_board");
			return Task.FromResult(new OperationResult(IsSuccess: true, "情报板"));
		}

		public Task<OperationResult> RefreshCommissionAsync(ZContext context, Mat? screen)
		{
			Calls.Add("refresh");
			return Task.FromResult(new OperationResult(IsSuccess: true, "刷新按钮"));
		}

		public Task<OperationResult> OpenFilterAsync(ZContext context, Mat? screen)
		{
			Calls.Add("open_filter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "筛选按钮"));
		}

		public Task<OperationResult> ResetFilterAsync(ZContext context, Mat? screen)
		{
			Calls.Add("reset_filter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "重置"));
		}

		public Task<OperationResult> SelectCommissionTypeAsync(ZContext context, IntelBoardCommissionType commissionType, Mat? screen)
		{
			Calls.Add("select:" + commissionType.ToDisplayName());
			return Task.FromResult(new OperationResult(IsSuccess: true, commissionType.ToDisplayName()));
		}

		public Task<OperationResult> CloseFilterAsync(ZContext context)
		{
			Calls.Add("close_filter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "关闭筛选"));
		}

		public Task<IntelBoardCommissionType?> FindCommissionAsync(ZContext context, Mat? screen)
		{
			IntelBoardCommissionType? intelBoardCommissionType = _commissions.Dequeue();
			Calls.Add("find:" + (intelBoardCommissionType?.ToDisplayName() ?? "none"));
			return Task.FromResult(intelBoardCommissionType);
		}

		public Task ScrollCommissionListAsync(ZContext context)
		{
			ScrollCount++;
			Calls.Add("scroll");
			return Task.CompletedTask;
		}

		public Task<OperationResult> AcceptCommissionAsync(ZContext context, Mat? screen)
		{
			OperationResult operationResult = _acceptResults.Dequeue();
			Calls.Add("accept:" + operationResult.Status);
			return Task.FromResult(operationResult);
		}

		public Task<OperationResult> NextStepAsync(ZContext context, Mat? screen)
		{
			OperationResult operationResult = _nextResults.Dequeue();
			Calls.Add("next:" + operationResult.Status);
			return Task.FromResult(operationResult);
		}

		public Task<OperationResult> ConfirmAcceptFailedAsync(ZContext context, Mat? screen)
		{
			Calls.Add("confirm_accept_failed");
			return Task.FromResult(new OperationResult(IsSuccess: true, "确认"));
		}

		public Task<OperationResult> ChooseTeamAsync(ZContext context, int teamIndex)
		{
			ChosenTeams.Add(teamIndex);
			Calls.Add($"choose_team:{teamIndex}");
			return Task.FromResult(new OperationResult(IsSuccess: true, "选择预备编队"));
		}

		public Task<OperationResult> DeployAsync(ZContext context, Mat? screen)
		{
			Calls.Add("deploy");
			return Task.FromResult(new OperationResult(IsSuccess: true, "出战"));
		}

		public Task<OperationResult> ConfirmCommissionAgentAsync(ZContext context, Mat? screen)
		{
			Calls.Add("confirm_agent");
			return Task.FromResult(new OperationResult(IsSuccess: true, "无弹窗"));
		}

		public void InitAutoBattle(ZContext context, IntelBoardConfig config)
		{
			Calls.Add("init_auto");
		}

		public OperationResult CheckBattleScreenReady(ZContext context, Mat? screen)
		{
			Calls.Add("battle_ready");
			return new OperationResult(IsSuccess: true, "按键-普通攻击");
		}

		public Task<OperationResult> PreBattleMoveAsync(ZContext context, IntelBoardCommissionType? commissionType)
		{
			Calls.Add("pre_battle:" + commissionType?.ToDisplayName());
			return Task.FromResult(new OperationResult(IsSuccess: true, commissionType?.ToDisplayName()));
		}

		public void StartAutoBattle(ZContext context)
		{
			Calls.Add("start_auto");
		}

		public Task<OperationResult> RunBattleAsync(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			OperationResult operationResult = _battleResults.Dequeue();
			Calls.Add("battle:" + operationResult.Status);
			return Task.FromResult(operationResult);
		}

		public Task<OperationResult> CheckBackToListAsync(ZContext context, Mat? screen)
		{
			BackToListCount++;
			bool flag = _backToListResults.Dequeue();
			Calls.Add("back_to_list:" + flag.ToString().ToLowerInvariant());
			return Task.FromResult(new OperationResult(flag, flag ? "周期内可获取" : "未回到列表"));
		}

		public Task<OperationResult> ClickSettlementButtonAsync(ZContext context, Mat? screen)
		{
			SettlementClickCount++;
			Calls.Add("settlement");
			return Task.FromResult(new OperationResult(IsSuccess: true, "完成"));
		}

		public Task<OperationResult> ReadProgressAsync(ZContext context, Mat? screen)
		{
			int num = _progress.Dequeue();
			Calls.Add($"read_progress:{num}");
			return Task.FromResult(new OperationResult(IsSuccess: true, null, num));
		}
	}

	private sealed class RecordingLogSink : ILogEventSink
	{
		public List<LogEvent> Events { get; } = new List<LogEvent>();

		public void Emit(LogEvent logEvent)
		{
			Events.Add(logEvent);
		}
	}

	private sealed class TestScreenshotController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);

		public override bool IsGameWindowReady => true;

		public int ClickCount { get; private set; }

		public int ScreenshotCount { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPosition { get; private set; }

		public bool ClickResult { get; set; } = true;

		public override bool InitBeforeContextRun()
		{
			return true;
		}

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
			LastClickPosition = position;
			return ClickResult;
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
			ScreenshotCount++;
			return _screenshot.Clone();
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

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesIntelBoardApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			IntelBoardAppFactory intelBoardAppFactory = new IntelBoardAppFactory(context);
			IApplication application = intelBoardAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = intelBoardAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = intelBoardAppFactory.GetRunRecord(0);
			Assert.Equal("intel_board", intelBoardAppFactory.AppId);
			Assert.Equal("情报板", intelBoardAppFactory.AppName);
			Assert.Equal("one_dragon", intelBoardAppFactory.GroupId);
			Assert.True(intelBoardAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<IntelBoardApp>(application);
			IntelBoardConfig intelBoardConfig = Assert.IsType<IntelBoardConfig>(config);
			Assert.Equal(-1, intelBoardConfig.PredefinedTeamIndex);
			Assert.Equal("全配队通用", intelBoardConfig.AutoBattleConfig);
			IntelBoardRunRecord intelBoardRunRecord = Assert.IsType<IntelBoardRunRecord>(runRecord);
			Assert.Equal("intel_board", intelBoardRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Config_LoadsPythonFieldsAndSettingMetadata()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "intel_board.yml"), "predefined_team_idx: 2\nauto_battle_config: \"安比模板\"\nexp_grind_mode: true");
			IntelBoardConfig intelBoardConfig = IntelBoardConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("intel_board", intelBoardConfig.AppId);
			Assert.Equal(0, intelBoardConfig.InstanceIndex);
			Assert.Equal("one_dragon", intelBoardConfig.GroupId);
			Assert.Equal(2, intelBoardConfig.PredefinedTeamIndex);
			Assert.Equal("安比模板", intelBoardConfig.AutoBattleConfig);
			Assert.True(intelBoardConfig.ExpGrindMode);
			Assert.Equal("FLYOUT", "FLYOUT");
			Assert.Contains((IEnumerable<IntelBoardSettingField>)IntelBoardSettings.Fields, (Predicate<IntelBoardSettingField>)((IntelBoardSettingField field) => field.Key == "exp_grind_mode" && field.DefaultValue.Equals(false)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_TracksWeeklyProgressAndExperience()
	{
		DateTimeOffset now = new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero);
		IntelBoardConfig config = new IntelBoardConfig
		{
			ExpGrindMode = true
		};
		IntelBoardRunRecord intelBoardRunRecord = new IntelBoardRunRecord(config, 4, () => now)
		{
			Dt = "20260706"
		};
		intelBoardRunRecord.AddCommission(IntelBoardCommissionType.NotoriousHunt);
		intelBoardRunRecord.AddCommission(IntelBoardCommissionType.ExpertChallenge);
		intelBoardRunRecord.UpdateBaseExp(4250);
		Assert.Equal(1, intelBoardRunRecord.NotoriousHuntCount);
		Assert.Equal(1, intelBoardRunRecord.ExpertChallengeCount);
		Assert.Equal(5000, intelBoardRunRecord.TotalExp);
		Assert.True(intelBoardRunRecord.ExpComplete);
		Assert.True(intelBoardRunRecord.IsFinishedByWeek);
		Assert.Equal(1, intelBoardRunRecord.RunStatusUnderNow);
		intelBoardRunRecord.ResetRecord();
		Assert.False(intelBoardRunRecord.ProgressComplete);
		Assert.Equal(0, intelBoardRunRecord.NotoriousHuntCount);
		Assert.Equal(0, intelBoardRunRecord.ExpertChallengeCount);
		Assert.Equal(0, intelBoardRunRecord.BaseExp);
	}

	[Fact]
	public void RunRecord_LoadsAndPersistsPythonFields()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "intel_board.yml"), "dt: \"20260706\"\nrun_time: \"07-06 01:00\"\nrun_time_float: 1783309200\nrun_status: 2\nprogress_complete: false\nnotorious_hunt_count: 1\nexpert_challenge_count: 2\nbase_exp: 250");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			IntelBoardConfig config = new IntelBoardConfig
			{
				ExpGrindMode = true
			};
			IntelBoardRunRecord intelBoardRunRecord = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(1, intelBoardRunRecord.NotoriousHuntCount);
			Assert.Equal(2, intelBoardRunRecord.ExpertChallengeCount);
			Assert.Equal(1250, intelBoardRunRecord.TotalExp);
			Assert.Equal("20260706", intelBoardRunRecord.Dt);
			Assert.Equal("07-06 01:00", intelBoardRunRecord.RunTime);
			Assert.Equal(1783309200.0, intelBoardRunRecord.RunTimeFloat);
			Assert.Equal(2, intelBoardRunRecord.RunStatus);
			intelBoardRunRecord.AddCommission(IntelBoardCommissionType.NotoriousHunt);
			intelBoardRunRecord.MarkProgressComplete();
			IntelBoardRunRecord intelBoardRunRecord2 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(2, intelBoardRunRecord2.NotoriousHuntCount);
			Assert.True(intelBoardRunRecord2.ProgressComplete);
			Assert.Equal("20260706", intelBoardRunRecord2.Dt);
			Assert.Equal("07-06 01:00", intelBoardRunRecord2.RunTime);
			Assert.Equal(1783309200.0, intelBoardRunRecord2.RunTimeFloat);
			Assert.Equal(2, intelBoardRunRecord2.RunStatus);
			intelBoardRunRecord2.UpdateStatus(3);
			IntelBoardRunRecord intelBoardRunRecord3 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(3, intelBoardRunRecord3.RunStatus);
			Assert.Equal(2, intelBoardRunRecord3.NotoriousHuntCount);
			Assert.True(intelBoardRunRecord3.ProgressComplete);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RunRecord_PublicPropertySettersPersistImmediatelyLikePython()
	{
		string text = CreateTempRoot();
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			IntelBoardConfig config = new IntelBoardConfig
			{
				ExpGrindMode = true
			};
			IntelBoardRunRecord intelBoardRunRecord = IntelBoardRunRecord.Load(environment, 0, config);
			intelBoardRunRecord.ProgressComplete = true;
			IntelBoardRunRecord intelBoardRunRecord2 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.True(intelBoardRunRecord2.ProgressComplete);
			intelBoardRunRecord.NotoriousHuntCount = 3;
			IntelBoardRunRecord intelBoardRunRecord3 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(3, intelBoardRunRecord3.NotoriousHuntCount);
			Assert.True(intelBoardRunRecord3.ProgressComplete);
			intelBoardRunRecord.ExpertChallengeCount = 4;
			IntelBoardRunRecord intelBoardRunRecord4 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(4, intelBoardRunRecord4.ExpertChallengeCount);
			Assert.Equal(3, intelBoardRunRecord4.NotoriousHuntCount);
			intelBoardRunRecord.BaseExp = 750;
			IntelBoardRunRecord intelBoardRunRecord5 = IntelBoardRunRecord.Load(environment, 0, config);
			Assert.Equal(750, intelBoardRunRecord5.BaseExp);
			Assert.Equal(4, intelBoardRunRecord5.ExpertChallengeCount);
			Assert.Equal(3, intelBoardRunRecord5.NotoriousHuntCount);
			Assert.True(intelBoardRunRecord5.ProgressComplete);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardScopeScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.ScreenContext.Reload();
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord runRecord = new IntelBoardRunRecord(config);
			RecordingIntelBoardFlow flow = new RecordingIntelBoardFlow();
			IntelBoardApp app = new IntelBoardApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
			Assert.True(flow.IntelBoardScopeWasActive);
			Assert.False(flow.OtherApplicationScreenWasActive);
			Assert.Null(context.ScreenContext.ActiveScreenNames);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardApp_DefaultFlowExecutesCommissionBattleSettlementAndProgressLoop()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardScopeScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.ScreenContext.Reload();
			IntelBoardConfig config = new IntelBoardConfig
			{
				PredefinedTeamIndex = 1,
				AutoBattleConfig = "默认模板"
			};
			IntelBoardRunRecord runRecord = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				BattleScreenReady = true,
				BackToList = true
			};
			services.ProgressTexts.Enqueue("0/1000");
			services.ProgressTexts.Enqueue("1000/1000");
			services.CommissionTypes.Enqueue(IntelBoardCommissionType.NotoriousHunt);
			services.AcceptCommissionResults.Enqueue(new OperationResult(IsSuccess: true, "接取委托"));
			services.AcceptCommissionResults.Enqueue(new OperationResult(IsSuccess: true, "委托代行中"));
			IntelBoardApp app = new IntelBoardApp(context, config, runRecord, null, services);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(result.IsSuccess);
			Assert.Contains("完成 恶名狩猎: 1", result.Status, StringComparison.Ordinal);
			Assert.Equal(1, runRecord.NotoriousHuntCount);
			Assert.True(runRecord.ProgressComplete);
			Assert.Equal(2, services.ReadProgressCallCount);
			Assert.Equal(new List<int>(1) { 1 }, services.ChosenTeams);
			Assert.True(services.AutoBattleLoaded);
			Assert.True(services.AutoBattleStarted);
			Assert.Equal(1, runRecord.RunStatus);
			Assert.Null(context.ScreenContext.ActiveScreenNames);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardApp_DirectConstructionLoadsCurrentInstanceRunRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			string recordDirectory = Path.Combine(rootDirectory, "config", "00", "app_run_record");
			Directory.CreateDirectory(recordDirectory);
			File.WriteAllText(Path.Combine(recordDirectory, "intel_board.yml"), $"dt: \"{DateTimeOffset.Now:yyyyMMdd}\"\nrun_time: \"07-14 01:00\"\nrun_time_float: 1783990800\nrun_status: 0\nprogress_complete: false\nnotorious_hunt_count: 2\nexpert_challenge_count: 3\nbase_exp: 500");
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			RecordingIntelBoardFlow flow = new RecordingIntelBoardFlow();
			IntelBoardApp app = new IntelBoardApp(context, null, null, flow);
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.NotNull(flow.RunRecord);
			Assert.Equal(2, flow.RunRecord.NotoriousHuntCount);
			Assert.Equal(3, flow.RunRecord.ExpertChallengeCount);
			Assert.Equal(2250, flow.RunRecord.TotalExp);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardApp_DelegatesPauseResumeAndStopToFlow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			RecordingIntelBoardFlow flow = new RecordingIntelBoardFlow();
			IntelBoardApp app = new IntelBoardApp(context, new IntelBoardConfig(), new IntelBoardRunRecord(new IntelBoardConfig()), flow);
			await app.OnPauseAsync(CancellationToken.None);
			await app.OnResumeAsync(CancellationToken.None);
			await app.OnStopAsync(CancellationToken.None);
			Assert.Equal(1, flow.PauseCount);
			Assert.Equal(1, flow.ResumeCount);
			Assert.Equal(1, flow.StopCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_CheckProgressMarksNormalCompletion()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				ProgressText = "1000/1000"
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.CheckProgress().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("完成", result.Status);
			Assert.True(record.ProgressComplete);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_EstimatesBaseExperienceInGrindMode()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig
			{
				ExpGrindMode = true
			};
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				ProgressText = "141/1000"
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.CheckProgress().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("继续", result.Status);
			Assert.Equal(750, record.BaseExp);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_CompletedExperienceSkipsProgressOcr()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig
			{
				ExpGrindMode = true
			};
			IntelBoardRunRecord record = new IntelBoardRunRecord(config)
			{
				BaseExp = 5000
			};
			RecordingIntelBoardServices services = new RecordingIntelBoardServices();
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.CheckProgress().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("完成", result.Status);
			Assert.Equal(0, services.ReadProgressCallCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_RunsInjectedCommissionAndBattleFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig
			{
				PredefinedTeamIndex = 1,
				AutoBattleConfig = "默认模板"
			};
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				CommissionType = IntelBoardCommissionType.NotoriousHunt,
				BattleScreenReady = true,
				BackToList = true,
				AutoBattleName = "队伍模板"
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult find = await operation.FindCommission().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult chooseTeam = await operation.ChoosePredefinedTeam().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult init = operation.InitAutoBattle();
			OperationRoundResult waitBattle = operation.WaitBattleScreen();
			OperationRoundResult move = await operation.PreBattleMove().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult start = operation.StartAutoBattle();
			OperationRoundResult battle = await operation.AutoBattle().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(IntelBoardCommissionType.NotoriousHunt, operation.CurrentCommissionType);
			OperationRoundResult backToList = await operation.CheckBackToList().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(find.IsSuccess);
			Assert.True(chooseTeam.IsSuccess);
			Assert.True(init.IsSuccess);
			Assert.True(services.AutoBattleLoaded);
			Assert.True(waitBattle.IsSuccess);
			Assert.True(move.IsSuccess);
			Assert.True(start.IsSuccess);
			Assert.True(battle.IsSuccess);
			Assert.True(backToList.IsSuccess);
			Assert.Null(operation.CurrentCommissionType);
			Assert.Equal(1, record.NotoriousHuntCount);
			Assert.Equal(new List<int>(1) { 1 }, services.ChosenTeams);
			Assert.True(services.AutoBattleLoaded);
			Assert.True(services.AutoBattleStarted);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void IntelBoardOperation_InitForIntelBoardReportsDetectorLoadFailure()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				DetectorLoadSucceeds = false
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);

			OperationRoundResult result = operation.InitForIntelBoard();

			Assert.False(result.IsSuccess);
			Assert.Equal("初始化失败", result.Status);
			Assert.True(services.DetectorLoadCalled);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_ExecuteAsyncRunsCompletePythonStateGraphWithScriptedExternalResults()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			RecordingLogSink sink = new RecordingLogSink();
			using Logger logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(sink).CreateLogger();
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory), logger);
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.BattleAssistantConfig.ScreenshotInterval = 0.0;
			IntelBoardConfig config = new IntelBoardConfig
			{
				PredefinedTeamIndex = 1
			};
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			ScriptedIntelBoardServices services = new ScriptedIntelBoardServices();
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationResult result = await operation.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(30L));
			Assert.True(result.IsSuccess, result.Status);
			Assert.Equal("完成 恶名狩猎: 0, 专业挑战室: 1, 累计经验: 250", result.Status);
			Assert.Equal(0, record.NotoriousHuntCount);
			Assert.Equal(1, record.ExpertChallengeCount);
			Assert.True(record.ProgressComplete);
			Assert.Equal(1, services.ScrollCount);
			Assert.Equal(1, services.SettlementClickCount);
			Assert.Equal(2, services.BackToListCount);
			Assert.Equal(new List<int>(1) { 1 }, services.ChosenTeams);
			Assert.Equal<List<string>>(new List<string>(33)
			{
				"init_detector", "back", "open_board", "click_board", "read_progress:0", "open_filter", "reset_filter", "select:恶名狩猎", "select:专业挑战室", "close_filter", "find:none",
				"scroll", "find:恶名狩猎", "accept:接取委托", "accept:委托代行中", "next:接取失败", "confirm_accept_failed", "find:专业挑战室", "accept:委托代行中", "next:预备编队", "choose_team:1",
				"deploy", "confirm_agent", "init_auto", "battle_ready", "pre_battle:专业挑战室", "start_auto", "battle:自动战斗中", "battle:战斗结束-完成", "back_to_list:false", "settlement",
				"back_to_list:true", "read_progress:1000"
			}, services.Calls);
			string[] enteredNodes = (from logEvent in sink.Events
				select logEvent.RenderMessage() into message
				where message.Contains(" 节点 ", StringComparison.Ordinal) && message.Contains(" 返回状态 ", StringComparison.Ordinal)
				select message).Aggregate(new List<string>(), delegate(List<string> nodes, string message)
			{
				int num = message.IndexOf(" 节点 ", StringComparison.Ordinal) + 4;
				int num2 = message.IndexOf(" 返回状态 ", num, StringComparison.Ordinal);
				int num3 = num;
				string text = message.Substring(num3, num2 - num3);
				int num4 = text.LastIndexOf(" -> ", StringComparison.Ordinal);
				if (nodes.Count == 0 || num4 >= 0)
				{
					nodes.Add(((num4 >= 0) ? text.Substring(num4 + 4) : text).Trim('"'));
				}
				return nodes;
			}).ToArray();
			string[] buffer = new string[32];
			buffer[0] = "初始化加载";
			buffer[1] = "返回录像店";
			buffer[2] = "打开情报板";
			buffer[3] = "点击情报板";
			buffer[4] = "检查进度";
			buffer[5] = "刷新委托";
			buffer[6] = "打开筛选";
			buffer[7] = "重置筛选";
			buffer[8] = "选择恶名狩猎";
			buffer[9] = "选择专业挑战室";
			buffer[10] = "关闭筛选";
			buffer[11] = "寻找委托";
			buffer[12] = "寻找委托";
			buffer[13] = "接取委托";
			buffer[14] = "接取委托";
			buffer[15] = "下一步";
			buffer[16] = "接取失败";
			buffer[17] = "寻找委托";
			buffer[18] = "接取委托";
			buffer[19] = "下一步";
			buffer[20] = "选择预备编队";
			buffer[21] = "点击出战";
			buffer[22] = "委托代行中弹窗";
			buffer[23] = "加载自动战斗指令";
			buffer[24] = "等待战斗画面加载";
			buffer[25] = "战斗前移动";
			buffer[26] = "开始自动战斗";
			buffer[27] = "检查回到委托列表";
			buffer[28] = "点击结算按钮";
			buffer[29] = "检查回到委托列表";
			buffer[30] = "检查进度";
			buffer[31] = "结束处理";
			Assert.Equal(buffer, enteredNodes);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_ResetFilterRetriesWhenOcrTextIsTemporarilyMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				ResetFilterResult = new OperationResult(IsSuccess: false, "找不到 重置")
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.ResetFilter().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("找不到 重置", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_AcceptCommissionWaitsAfterClickingAccept()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				AcceptCommissionResult = new OperationResult(IsSuccess: true, "接取委托")
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.AcceptCommission().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Wait, result.Kind);
			Assert.Equal("接取委托", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_AcceptCommissionRetriesWhenTargetIsMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardRunRecord record = new IntelBoardRunRecord(config);
			RecordingIntelBoardServices services = new RecordingIntelBoardServices
			{
				AcceptCommissionResult = new OperationResult(IsSuccess: false, "未匹配到目标文本")
			};
			IntelBoardOperation operation = new IntelBoardOperation(context, config, record, services);
			OperationRoundResult result = await operation.AcceptCommission().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("未匹配到目标文本", result.Status);
			Assert.Equal(TimeSpan.FromMilliseconds(500L), result.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_NextStepRetriesWhenPythonActionTargetsAreMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation operation = new IntelBoardOperation(services: new RecordingIntelBoardServices
			{
				NextStepResult = new OperationResult(IsSuccess: false, "未匹配到目标文本")
			}, context: context, config: config, runRecord: new IntelBoardRunRecord(config));
			OperationRoundResult result = await operation.NextStep().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Retry, result.Kind);
			Assert.Equal("未匹配到目标文本", result.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), result.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "预备编队" })]
	[InlineData(new object[] { "接取失败" })]
	public async Task IntelBoardOperation_NextStepTransitionsImmediatelyForPythonTerminalScreens(string status)
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation operation = new IntelBoardOperation(services: new RecordingIntelBoardServices
			{
				NextStepResult = new OperationResult(IsSuccess: true, status)
			}, context: context, config: config, runRecord: new IntelBoardRunRecord(config));
			OperationRoundResult result = await operation.NextStep().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal(status, result.Status);
			Assert.Null(result.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_OpenFilterReportsMissingPythonAreaAsFailure()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			using Mat screen = new Mat(100, 100, MatType.CV_8UC3, Scalar.Black);
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			OperationResult result = await services.OpenFilterAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("区域未配置 点数兑换", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_KeepsPythonCommissionAndSettlementWaits()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation operation = new IntelBoardOperation(services: new RecordingIntelBoardServices(), context: context, config: config, runRecord: new IntelBoardRunRecord(config));
			OperationRoundResult page = await operation.FindCommission().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult settlement = await operation.ClickSettlementButton().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.Equal(OperationRoundResultKind.Wait, page.Kind);
			Assert.Equal(TimeSpan.FromSeconds(1L), page.Delay);
			Assert.Equal(OperationRoundResultKind.Success, settlement.Kind);
			Assert.Equal(TimeSpan.FromSeconds(1L), settlement.Delay);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task IntelBoardOperation_AcceptsCommissionListWhenTypeWasNotRecorded()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation operation = new IntelBoardOperation(context, config, new IntelBoardRunRecord(config), new RecordingIntelBoardServices
			{
				BackToList = true
			});
			OperationRoundResult result = await operation.CheckBackToList().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("结算完成", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void IntelBoardOperation_WaitBattleScreenKeepsPythonOneSecondRetryDelay()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation intelBoardOperation = new IntelBoardOperation(context, config, new IntelBoardRunRecord(config), new RecordingIntelBoardServices
			{
				BattleScreenReady = false
			});
			OperationRoundResult operationRoundResult = intelBoardOperation.WaitBattleScreen();
			Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
			Assert.Equal("未找到 按键-交互", operationRoundResult.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), operationRoundResult.Delay);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultIntelBoardOperationServices_CheckBattleScreenReadyPreservesPythonSecondAreaStatus()
	{
		string text = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			DefaultIntelBoardOperationServices defaultIntelBoardOperationServices = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = testScreenshotController.Screenshot().Screen;
			OperationResult operationResult = defaultIntelBoardOperationServices.CheckBattleScreenReady(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("区域未配置 按键-交互", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void IntelBoardOperation_DeclaresCompletePythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(IntelBoardOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[27]
		{
			"初始化加载", "返回录像店", "打开情报板", "点击情报板", "刷新委托", "打开筛选", "重置筛选", "选择恶名狩猎", "选择专业挑战室", "关闭筛选", "寻找委托",
			"接取委托", "下一步", "接取失败", "选择预备编队", "点击出战", "委托代行中弹窗", "选择任意预备编队", "加载自动战斗指令", "等待战斗画面加载", "战斗前移动", "开始自动战斗",
			"战斗中", "检查回到委托列表", "点击结算按钮", "检查进度", "结束处理"
		}, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["初始化加载"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		string[] actual = readOnlyDictionary.SelectMany((KeyValuePair<string, MethodInfo> pair) => from edge in pair.Value.GetCustomAttributes<NodeFromAttribute>()
			select Edge(edge.FromName, pair.Key, edge.Success, edge.Status, edge.IgnoreStatus)).Order<string>(StringComparer.Ordinal).ToArray();
		string[] source = new string[35]
		{
			Edge("初始化加载", "返回录像店"),
			Edge("返回录像店", "打开情报板"),
			Edge("打开情报板", "点击情报板"),
			Edge("检查进度", "刷新委托", success: false),
			Edge("接取委托", "刷新委托", success: false),
			Edge("刷新委托", "打开筛选", success: true, "未筛选"),
			Edge("打开筛选", "重置筛选"),
			Edge("重置筛选", "选择恶名狩猎"),
			Edge("选择恶名狩猎", "选择专业挑战室"),
			Edge("选择专业挑战室", "关闭筛选"),
			Edge("刷新委托", "寻找委托"),
			Edge("关闭筛选", "寻找委托"),
			Edge("寻找委托", "寻找委托", success: true, "翻页"),
			Edge("接取失败", "寻找委托"),
			Edge("寻找委托", "接取委托"),
			Edge("接取委托", "下一步"),
			Edge("下一步", "接取失败", success: true, "接取失败"),
			Edge("下一步", "选择预备编队"),
			Edge("选择预备编队", "点击出战"),
			Edge("选择任意预备编队", "点击出战"),
			Edge("点击出战", "委托代行中弹窗"),
			Edge("委托代行中弹窗", "选择任意预备编队", success: true, "未选择代理人"),
			Edge("委托代行中弹窗", "加载自动战斗指令"),
			Edge("加载自动战斗指令", "等待战斗画面加载"),
			Edge("等待战斗画面加载", "战斗前移动"),
			Edge("战斗前移动", "开始自动战斗"),
			Edge("开始自动战斗", "战斗中"),
			Edge("战斗中", "检查回到委托列表"),
			Edge("点击结算按钮", "检查回到委托列表"),
			Edge("检查回到委托列表", "点击结算按钮", success: false),
			Edge("检查回到委托列表", "检查进度"),
			Edge("点击情报板", "检查进度"),
			Edge("打开情报板", "结束处理", success: true, "本周期已完成"),
			Edge("检查进度", "结束处理"),
			Edge("寻找委托", "结束处理", success: true, "无委托")
		};
		Assert.Equal<string[]>(source.Order<string>(StringComparer.Ordinal).ToArray(), actual);
		static string Edge(string from, string to, bool success = true, string? status = null, bool ignoreStatus = true)
		{
			return $"{from}|{to}|{success}|{status ?? "<null>"}|{ignoreStatus}";
		}
	}

	[Fact]
	public void IntelBoardOperation_UsesPythonDefaultNodeRetryCount()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(text));
			IntelBoardConfig config = new IntelBoardConfig();
			IntelBoardOperation obj = new IntelBoardOperation(context, config, new IntelBoardRunRecord(config));
			PropertyInfo property = typeof(Operation).GetProperty("DefaultNodeMaxRetryTimes", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.Equal(1, Assert.IsType<int>(property.GetValue(obj)));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "1000/1000", 1000 })]
	[InlineData(new object[] { "当前 70／1000", 70 })]
	[InlineData(new object[] { "abc", 0 })]
	public void TryParseProgress_ParsesPythonOcrText(string text, int expected)
	{
		int current;
		bool actual = IntelBoardOperation.TryParseProgress(text, out current);
		Assert.Equal(expected > 0, actual);
		Assert.Equal(expected, current);
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_UsesAutoBattleEndResult()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			context.AutoBattleContext.LastCheckEndResult = "战斗结束-完成";
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			OperationResult result = await services.RunBattleAsync(context, null, null).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("战斗结束-完成", result.Status);
			Assert.False(context.AutoBattleContext.IsRuntimeRunning);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_ConsumesAnyNonNullPythonEndStatus()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AutoBattleContext.LastCheckEndResult = string.Empty;
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			OperationResult result = await services.RunBattleAsync(context, null, null).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal(string.Empty, result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_DefersNormalBattleResultToNextTick()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "完成") });
			context.ScreenContext.Reload();
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.RunBattleAsync(context, screen, DateTimeOffset.UtcNow).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("自动战斗中", result.Status);
			context.AutoBattleContext.LastCheckEndResult = "普通战斗-完成";
			result = await services.RunBattleAsync(context, null, null).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("普通战斗-完成", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_DefersRetreatResultToNextTick()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "撤退") });
			context.ScreenContext.Reload();
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.RunBattleAsync(context, screen, DateTimeOffset.UtcNow).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("自动战斗中", result.Status);
			context.AutoBattleContext.LastCheckEndResult = "普通战斗-撤退";
			result = await services.RunBattleAsync(context, null, null).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("普通战斗-撤退", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_DoesNotTreatIntelBoardListAsBattleResult()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "点数兑换") });
			context.ScreenContext.Reload();
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.RunBattleAsync(context, screen, DateTimeOffset.UtcNow).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("自动战斗中", result.Status);
			Assert.Null(context.AutoBattleContext.LastCheckEndResult);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_ReturnsFailureWhenScreenshotIsMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			OperationResult result = await services.RunBattleAsync(context, null, null).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未获取截图", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_RunBattleAsync_ReturnsRunningWhileBattleKeepsRunning()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.RunBattleAsync(context, screen, DateTimeOffset.UtcNow).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("自动战斗中", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_OpenBoardAsync_DoesNotFallbackToAreaCenterWhenTemplateMisses()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteFunctionGuideScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.OpenBoardAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未找到 功能导览", result.Status);
			Assert.Equal(0, controller.ClickCount);
			Assert.Null(controller.LastClickPosition);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_ReadProgressReportsMissingConfiguredArea()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices();
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.ReadProgressAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("区域未配置 进度文本", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_ClickSettlementUsesPythonTargetPriority()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 0, 0, 20, 8, "确认"),
				new OcrMatchResult(0.5, 100, 0, 20, 8, "完成")
			});
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.ClickSettlementButtonAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("完成", result.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(110, 4), controller.LastClickPosition);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_ClickSettlementDoesNotTreatExitAsSettlementAction()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.873, 1668, 1007, 66, 41, "退出") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.ClickSettlementButtonAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("找不到 完成/下一步/确认", result.Status);
			Assert.Equal(0, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Theory]
	[InlineData(new object[] { "accept", "接取委托" })]
	[InlineData(new object[] { "next", "下一步" })]
	[InlineData(new object[] { "settlement", "完成" })]
	public async Task DefaultIntelBoardOperationServices_ActionOcrKeepsPythonResultWhenControllerRejectsClick(string action, string targetText)
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController
			{
				ClickResult = false
			};
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 100, 100, 80, 30, targetText) });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			if (1 == 0)
			{
			}
			OperationResult operationResult = ((action == "accept") ? (await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L))) : ((!(action == "next")) ? (await services.ClickSettlementButtonAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L))) : (await services.NextStepAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L)))));
			if (1 == 0)
			{
			}
			OperationResult result = operationResult;
			Assert.True(result.IsSuccess);
			Assert.Equal(targetText, result.Status);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AcceptCommissionKeepsPythonRetrySemantics()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 944, 836, 112, 33, "接取委托") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = new OperationResult(IsSuccess: false, "");
			for (int i = 0; i < 5; i++)
			{
				result = await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			}
			Assert.True(result.IsSuccess);
			Assert.Equal("接取委托", result.Status);
			Assert.Equal(5, controller.ClickCount);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1000, 852), controller.LastClickPosition);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AcceptCommissionDoesNotClickManagementOrInstructionText()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.9, 180, 32, 123, 37, "委托管理"),
				new OcrMatchResult(0.9, 320, 700, 620, 32, "暂无已发布或已接取的委托，请点击发布委托或前往情报板接取委托")
			});
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未匹配到目标文本", result.Status);
			Assert.Equal(0, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AcceptCommissionClicksGoBeforeAcceptedState()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.9, 655, 420, 260, 36, "委托进行中 29分49秒"),
				new OcrMatchResult(0.95, 1130, 838, 64, 34, "前往")
			});
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("前往", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(1162, 855), controller.LastClickPosition);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AcceptCommissionDoesNotTreatRunningTextAsAccepted()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.9, 655, 420, 260, 36, "委托进行中 29分49秒") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未匹配到目标文本", result.Status);
			Assert.Equal(0, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AcceptCommissionClicksFuzzyAcceptedState()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.96, 700, 420, 180, 36, "委托代行巾") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			OperationResult result = await services.AcceptCommissionAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("委托代行中", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(790, 438), controller.LastClickPosition);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_NextStepReusesCurrentRoundScreenshot()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 700, 420, 180, 36, "预备编队") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.Zero);
			using Mat screen = controller.Screenshot().Screen;
			int capturesBefore = controller.ScreenshotCount;
			OperationResult result = await services.NextStepAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("预备编队", result.Status);
			Assert.Equal(capturesBefore, controller.ScreenshotCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_UsesPythonPreClickDelay()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 100, 100, 80, 30, "完成") });
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices();
			using Mat screen = controller.Screenshot().Screen;
			Stopwatch stopwatch = Stopwatch.StartNew();
			OperationResult result = await services.ClickSettlementButtonAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			stopwatch.Stop();
			Assert.True(result.IsSuccess);
			Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(250L), $"实际点击前等待 {stopwatch.Elapsed.TotalMilliseconds:0}ms");
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultIntelBoardOperationServices_AreaClicksUsePythonZeroPreDelay()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteIntelBoardBattleScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 60, 15, "点数兑换") });
			context.ScreenContext.Reload();
			context.ScreenContext.EnterScope("intel_board");
			DefaultIntelBoardOperationServices services = new DefaultIntelBoardOperationServices(TimeSpan.FromMilliseconds(500L));
			using Mat screen = controller.Screenshot().Screen;
			Stopwatch openStopwatch = Stopwatch.StartNew();
			OperationResult openResult = await services.OpenFilterAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			openStopwatch.Stop();
			Stopwatch closeStopwatch = Stopwatch.StartNew();
			OperationResult closeResult = await services.CloseFilterAsync(context).WaitAsync(TimeSpan.FromSeconds(2L));
			closeStopwatch.Stop();
			Assert.True(openResult.IsSuccess, openResult.Status);
			Assert.True(closeResult.IsSuccess, closeResult.Status);
			Assert.True(openStopwatch.Elapsed < TimeSpan.FromMilliseconds(300L), $"打开筛选点击前实际等待 {openStopwatch.Elapsed.TotalMilliseconds:0}ms");
			Assert.True(closeStopwatch.Elapsed < TimeSpan.FromMilliseconds(300L), $"关闭筛选点击前实际等待 {closeStopwatch.Elapsed.TotalMilliseconds:0}ms");
			Assert.Equal(2, controller.ClickCount);
			context.ScreenContext.ExitScope();
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteIntelBoardBattleScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 按键-普通攻击\n      pc_rect: [0, 0, 100, 100]\n    - area_name: 距离显示区域\n      pc_rect: [0, 0, 100, 100]\n    - area_name: 战斗结果-完成\n      pc_rect: [10, 10, 80, 30]\n      text: 完成\n      lcs_percent: 0.5\n    - area_name: 战斗结果-撤退\n      pc_rect: [10, 10, 80, 30]\n      text: 撤退\n      lcs_percent: 0.5\n    - area_name: 战斗结果-倒带\n      pc_rect: [10, 10, 80, 30]\n      text: 倒带\n      lcs_percent: 0.5\n- screen_id: intel_board\n  screen_name: 情报板\n  app_id: intel_board\n  area_list:\n    - area_name: 点数兑换\n      pc_rect: [10, 10, 80, 30]\n      text: 点数兑换\n      lcs_percent: 0.5\n    - area_name: 筛选按钮\n      pc_rect: [100, 100, 180, 140]\n    - area_name: 关闭筛选\n      pc_rect: [200, 100, 280, 140]");
	}

	private static void WriteFunctionGuideScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: normal_world_basic\n  screen_name: 大世界-普通\n  pc_alt: true\n  area_list:\n    - area_name: 功能导览\n      pc_rect: [1776, 28, 1869, 117]\n      template_sub_dir: normal_world\n      template_id: function_menu\n      template_match_threshold: 0.7\n      gamepad_key: function_menu");
	}

	private static void WriteIntelBoardScopeScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "_od_merged.yml"), "- screen_id: normal_world\n  screen_name: 大世界-普通\n  area_list: []\n- screen_id: intel_board\n  screen_name: 情报板\n  app_id: intel_board\n  area_list: []\n- screen_id: other_application\n  screen_name: 其他应用画面\n  app_id: other_application\n  area_list: []");
	}
}
