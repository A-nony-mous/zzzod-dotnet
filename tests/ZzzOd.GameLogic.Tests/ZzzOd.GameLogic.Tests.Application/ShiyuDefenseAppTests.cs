using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Events;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Tests.TestSupport;
using ZzzOd.GameLogic.Vision;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ShiyuDefenseAppTests
{
	private sealed class RecordingShiyuDefenseFlow : IShiyuDefenseAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "所有节点都完成挑战"));
		}
	}

	private sealed class RecordingShiyuDefenseServices : IShiyuDefenseOperationServices
	{
		public int? NextNodeIndex { get; set; }

		public List<DefensePhaseTeamInfo> PhaseTeams { get; set; } = new List<DefensePhaseTeamInfo>();

		public List<DefensePhaseTeamInfo> MultiRoomTeams { get; set; } = new List<DefensePhaseTeamInfo>();

		public List<int> ChosenTeamIndexes { get; } = new List<int>();

		public List<int> BattleTeamIndexes { get; } = new List<int>();

		public int EnterTeamSelectionCount { get; private set; }

		public OperationResult AdvanceResult { get; set; } = new OperationResult(IsSuccess: true, "下一步");

		public int? AdvanceFromNodeIndex { get; private set; }

		public Task<OperationResult> TransportAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "传送"));
		}

		public Task<OperationResult> WaitForMainScreenAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战报"));
		}

		public Task<int?> GetNextNodeIndexAsync(ZContext context, ShiyuDefenseConfig config, ShiyuDefenseRunRecord runRecord, Mat? screen)
		{
			return Task.FromResult(NextNodeIndex);
		}

		public Task<OperationResult> SelectNodeAsync(ZContext context, int nodeIndex, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, $"节点-{nodeIndex:00}"));
		}

		public Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen)
		{
			return Task.FromResult((IReadOnlyList<DefensePhaseTeamInfo>)PhaseTeams);
		}

		public Task<OperationResult> EnterTeamSelectionAsync(ZContext context)
		{
			EnterTeamSelectionCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "角色头像"));
		}

		public Task<OperationResult> PrepareMultiRoomAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "多间模式"));
		}

		public Task<IReadOnlyList<DefensePhaseTeamInfo>> CalculateMultiRoomTeamsAsync(ZContext context, ShiyuDefenseConfig config, int nodeIndex, Mat? screen)
		{
			return Task.FromResult((IReadOnlyList<DefensePhaseTeamInfo>)MultiRoomTeams);
		}

		public Task<OperationResult> ChooseTeamAsync(ZContext context, IReadOnlyList<int> teamIndexes)
		{
			ChosenTeamIndexes.AddRange(teamIndexes);
			return Task.FromResult(new OperationResult(IsSuccess: true, "选择配队"));
		}

		public Task<OperationResult> SelectRoomAsync(ZContext context, int roomIndex, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, ShiyuDefenseConstants.RoomNames[roomIndex]));
		}

		public Task<OperationResult> DeployAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "出战"));
		}

		public Task<OperationResult> WaitAndChooseMultiRoomTeamAsync(ZContext context, int teamIndex, Mat? screen)
		{
			return ChooseTeamAsync(context, new int[] { teamIndex });
		}

		public Task<OperationResult> BattleAsync(ZContext context, int teamIndex)
		{
			BattleTeamIndexes.Add(teamIndex);
			return Task.FromResult(new OperationResult(IsSuccess: true, "战斗完成"));
		}

		public Task<OperationResult> ExitMultiRoomAfterBattleAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战斗结束-退出"));
		}

		public Task<OperationResult> BackToMainScreenAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "式舆防卫战"));
		}

		public Task<OperationResult> RecoverFromMultiRoomFailureAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战报"));
		}

		public Task<OperationResult> AdvanceAfterBattleAsync(ZContext context, int currentNodeIndex, ShiyuDefenseConfig config, Mat? screen)
		{
			AdvanceFromNodeIndex = currentNodeIndex;
			return Task.FromResult(AdvanceResult);
		}

		public Task<OperationResult> FinishAllNodesAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战报"));
		}

		public Task<OperationResult> ClaimRewardAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "全部领取"));
		}

		public Task<OperationResult> CloseRewardAsync(ZContext context, Mat? screen)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "战报"));
		}

		public Task<OperationResult> BackToWorldAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}
	}

	private sealed class TestScreenshotController : ControllerBase, IZzzControllerActions, IDisposable
	{
		private Mat _screenshot = new Mat(new Size(160, 90), MatType.CV_8UC3, Scalar.Black);

		public int ClickCount { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastClickPoint { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastDragStart { get; private set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastDragEnd { get; private set; }

		public int InteractCount { get; private set; }

		public int MoveWCount { get; private set; }

		public int TurnByDistanceCount { get; private set; }

		public int ScreenshotCount { get; private set; }

		public void SetScreenshot(Mat screenshot)
		{
			ArgumentNullException.ThrowIfNull(screenshot, "screenshot");
			_screenshot.Dispose();
			_screenshot = screenshot.Clone();
		}

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			ClickCount++;
			LastClickPoint = position;
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
			LastDragStart = start;
			LastDragEnd = end;
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			MoveWCount++;
		}

		public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
		}

		public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false)
		{
			InteractCount++;
		}

		public void TurnByDistance(float distance)
		{
			TurnByDistanceCount++;
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

	private sealed class FailureShiyuDefenseBattleServices : IShiyuDefenseBattleServices
	{
		public void LoadAutoOperation(ZContext context, int predefinedTeamIndex)
		{
		}

		public bool IsBattleScreenReady(ZContext context, Mat? screen)
		{
			return true;
		}

		public OperationResult PrepareBattle(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true);
		}

		public OperationResult RunAutoBattle(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
		{
			return new OperationResult(IsSuccess: true, "战斗结束-撤退");
		}

		public OperationResult MoveAfterBattle(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true, "下一阶段");
		}

		public void StopAutoBattle(ZContext context)
		{
		}

		public OperationResult PrepareVoluntaryExit(ZContext context, Mat? screen)
		{
			return new OperationResult(IsSuccess: true, "菜单");
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
	public void Factory_ExposesPythonMetadataAndCreatesShiyuDefenseApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			ShiyuDefenseAppFactory shiyuDefenseAppFactory = zContext.ApplicationFactoryRegistry.CreateShiyuDefenseFactory();
			IApplication application = shiyuDefenseAppFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = shiyuDefenseAppFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = shiyuDefenseAppFactory.GetRunRecord(0);
			Assert.Equal("shiyu_defense", shiyuDefenseAppFactory.AppId);
			Assert.Equal("式舆防卫战", shiyuDefenseAppFactory.AppName);
			Assert.Equal("one_dragon", shiyuDefenseAppFactory.GroupId);
			Assert.True(shiyuDefenseAppFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<ShiyuDefenseApp>(application);
			Assert.IsType<ShiyuDefenseConfig>(config);
			Assert.IsType<ShiyuDefenseRunRecord>(runRecord);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersShiyuDefenseAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterShiyuDefenseApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("shiyu_defense"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("shiyu_defense"));
			Assert.Contains("shiyu_defense", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ShiyuDefenseConfig_LoadsPythonFieldsAndUpdatesWeakness()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "one_dragon");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "shiyu_defense.yml"), "critical_max_node_idx: 5\nteam_list:\n  - team_idx: 0\n    for_critical: true\n    weakness_list:\n      - ELECTRIC\n      - FIRE\n  - team_idx: 1\n    for_critical: false\n    weakness_list:\n      - ICE");
			ShiyuDefenseConfig shiyuDefenseConfig = ShiyuDefenseConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal("shiyu_defense", shiyuDefenseConfig.AppId);
			Assert.Equal(5, shiyuDefenseConfig.CriticalMaxNodeIndex);
			Assert.True(shiyuDefenseConfig.TeamList[0].ForCritical);
			int num = 2;
			List<DmgTypeEnum> list = new List<DmgTypeEnum>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<DmgTypeEnum> span = CollectionsMarshal.AsSpan(list);
			span[0] = DmgTypeEnum.ELECTRIC;
			span[1] = DmgTypeEnum.FIRE;
			Assert.Equal(list, shiyuDefenseConfig.TeamList[0].WeaknessList);
			Assert.False(shiyuDefenseConfig.AddWeakness(0, DmgTypeEnum.FIRE));
			Assert.True(shiyuDefenseConfig.AddWeakness(0, DmgTypeEnum.ETHER));
			Assert.Contains("ETHER", (IEnumerable<string>)shiyuDefenseConfig.TeamList[0].WeaknessListRaw);
			Assert.True(shiyuDefenseConfig.RemoveWeakness(0, DmgTypeEnum.ELECTRIC));
			Assert.True(shiyuDefenseConfig.ChangeForCritical(1, forCritical: true));
			shiyuDefenseConfig.CriticalMaxNodeIndex = 6;
			ShiyuDefenseConfig shiyuDefenseConfig2 = ShiyuDefenseConfig.Load(new OneDragonEnvironment(text), 0, "one_dragon");
			Assert.Equal(6, shiyuDefenseConfig2.CriticalMaxNodeIndex);
			num = 2;
			List<DmgTypeEnum> list2 = new List<DmgTypeEnum>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<DmgTypeEnum> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = DmgTypeEnum.FIRE;
			span2[1] = DmgTypeEnum.ETHER;
			Assert.Equal(list2, shiyuDefenseConfig2.TeamList[0].WeaknessList);
			Assert.True(shiyuDefenseConfig2.TeamList[1].ForCritical);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void ShiyuDefenseRunRecord_TracksNextNodeAndPersistsHistory()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "shiyu_defense.yml"), "dt: \"20260706\"\nrun_time: \"07-06 12:34\"\nrun_time_float: 1783312440\nrun_status: 3\ncritical_history:\n  - 1\n  - 3");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 3
			};
			ShiyuDefenseRunRecord shiyuDefenseRunRecord = ShiyuDefenseRunRecord.Load(environment, 0, config);
			Assert.Equal(2, shiyuDefenseRunRecord.NextNodeIndex());
			shiyuDefenseRunRecord.AddNodeFinished(2);
			Assert.Null(shiyuDefenseRunRecord.NextNodeIndex());
			Assert.Equal(1, shiyuDefenseRunRecord.RunStatusUnderNow);
			ShiyuDefenseRunRecord shiyuDefenseRunRecord2 = ShiyuDefenseRunRecord.Load(environment, 0, config);
			int num = 3;
			List<int> list = new List<int>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<int> span = CollectionsMarshal.AsSpan(list);
			span[0] = 1;
			span[1] = 3;
			span[2] = 2;
			Assert.Equal(list, shiyuDefenseRunRecord2.CriticalHistory);
			Assert.Equal("07-06 12:34", shiyuDefenseRunRecord2.RunTime);
			Assert.Equal(1783312440.0, shiyuDefenseRunRecord2.RunTimeFloat);
			Assert.Equal(3, shiyuDefenseRunRecord2.RunStatus);
			shiyuDefenseRunRecord2.ResetRecord();
			Assert.Empty(shiyuDefenseRunRecord2.CriticalHistory);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefenseTeamSearcher_PrefersWeaknessAndAvoidsAgentConflict()
	{
		ShiyuDefenseConfig shiyuDefenseConfig = new ShiyuDefenseConfig();
		int num = 3;
		List<ShiyuDefenseTeamConfig> list = new List<ShiyuDefenseTeamConfig>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<ShiyuDefenseTeamConfig> span = CollectionsMarshal.AsSpan(list);
		ref ShiyuDefenseTeamConfig reference = ref span[0];
		ShiyuDefenseTeamConfig obj = new ShiyuDefenseTeamConfig
		{
			TeamIndex = 0,
			ForCritical = true
		};
		int num2 = 1;
		List<DmgTypeEnum> list2 = new List<DmgTypeEnum>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = DmgTypeEnum.ELECTRIC;
		obj.WeaknessList = list2;
		reference = obj;
		ref ShiyuDefenseTeamConfig reference2 = ref span[1];
		ShiyuDefenseTeamConfig obj2 = new ShiyuDefenseTeamConfig
		{
			TeamIndex = 1,
			ForCritical = true
		};
		num2 = 1;
		List<DmgTypeEnum> list3 = new List<DmgTypeEnum>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = DmgTypeEnum.ELECTRIC;
		obj2.WeaknessList = list3;
		reference2 = obj2;
		ref ShiyuDefenseTeamConfig reference3 = ref span[2];
		ShiyuDefenseTeamConfig obj3 = new ShiyuDefenseTeamConfig
		{
			TeamIndex = 2,
			ForCritical = true
		};
		num2 = 1;
		List<DmgTypeEnum> list4 = new List<DmgTypeEnum>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = DmgTypeEnum.FIRE;
		obj3.WeaknessList = list4;
		reference3 = obj3;
		shiyuDefenseConfig.TeamList = list;
		ShiyuDefenseConfig config = shiyuDefenseConfig;
		num = 3;
		List<PredefinedTeamInfo> list5 = new List<PredefinedTeamInfo>(num);
		CollectionsMarshal.SetCount(list5, num);
		Span<PredefinedTeamInfo> span2 = CollectionsMarshal.AsSpan(list5);
		ref PredefinedTeamInfo reference4 = ref span2[0];
		num2 = 3;
		List<string> list6 = new List<string>(num2);
		CollectionsMarshal.SetCount(list6, num2);
		Span<string> span3 = CollectionsMarshal.AsSpan(list6);
		span3[0] = "anby";
		span3[1] = "anton";
		span3[2] = "unknown";
		reference4 = new PredefinedTeamInfo(0, "电队A", "电队A-auto", list6);
		ref PredefinedTeamInfo reference5 = ref span2[1];
		num2 = 3;
		List<string> list7 = new List<string>(num2);
		CollectionsMarshal.SetCount(list7, num2);
		Span<string> span4 = CollectionsMarshal.AsSpan(list7);
		span4[0] = "anby";
		span4[1] = "grace";
		span4[2] = "unknown";
		reference5 = new PredefinedTeamInfo(1, "电队B", "电队B-auto", list7);
		ref PredefinedTeamInfo reference6 = ref span2[2];
		num2 = 3;
		List<string> list8 = new List<string>(num2);
		CollectionsMarshal.SetCount(list8, num2);
		Span<string> span5 = CollectionsMarshal.AsSpan(list8);
		span5[0] = "lucy";
		span5[1] = "koleda";
		span5[2] = "unknown";
		reference6 = new PredefinedTeamInfo(2, "火队", "火队-auto", list8);
		List<PredefinedTeamInfo> predefinedTeamList = list5;
		num = 2;
		List<DefensePhaseTeamInfo> list9 = new List<DefensePhaseTeamInfo>(num);
		CollectionsMarshal.SetCount(list9, num);
		Span<DefensePhaseTeamInfo> span6 = CollectionsMarshal.AsSpan(list9);
		span6[0] = new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.ELECTRIC }, Array.Empty<DmgTypeEnum>());
		span6[1] = new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.FIRE }, Array.Empty<DmgTypeEnum>());
		List<DefensePhaseTeamInfo> detectedPhaseList = list9;
		IReadOnlyList<DefensePhaseTeamInfo> source = ShiyuDefenseTeamUtils.CalculateTeams(config, predefinedTeamList, detectedPhaseList);
		Assert.Equal((ReadOnlySpan<int>)new int[2] { 0, 2 }, source.Select((DefensePhaseTeamInfo team) => team.TeamIndex).ToArray());
		Assert.Equal(2, source.Sum((DefensePhaseTeamInfo team) => team.Score));
	}

	[Fact]
	public async Task ShiyuDefenseApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ShiyuDefenseConfig config = new ShiyuDefenseConfig();
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config);
			RecordingShiyuDefenseFlow flow = new RecordingShiyuDefenseFlow();
			ShiyuDefenseApp app = new ShiyuDefenseApp(context, config, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("所有节点都完成挑战", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ShiyuDefenseOperation_RunsInjectedNormalNodeFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 1
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config);
			RecordingShiyuDefenseServices services = new RecordingShiyuDefenseServices
			{
				NextNodeIndex = 1,
				PhaseTeams = new List<DefensePhaseTeamInfo>(2)
				{
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.ELECTRIC }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 0
					},
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.FIRE }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 1
					}
				}
			};
			ShiyuDefenseOperation operation = new ShiyuDefenseOperation(context, config, runRecord, services);
			OperationRoundResult transport = await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult wait = await operation.WaitLoading().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult choose = await operation.ChooseNodeIndex().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult weakness = await operation.CheckWeakness().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult chooseTeam = await operation.ChooseTeam().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult deploy = await operation.Deploy().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult firstBattle = await operation.ShiyuBattle().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult secondBattle = await operation.ShiyuBattle().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult back = await operation.BackAfterAll().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(transport.IsSuccess);
			Assert.True(wait.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, choose.Kind);
			Assert.Equal(1, operation.CurrentNodeIndex);
			Assert.True(weakness.IsSuccess);
			Assert.Equal("角色头像", weakness.Status);
			Assert.True(chooseTeam.IsSuccess);
			Assert.True(deploy.IsSuccess);
			Assert.Equal(OperationRoundResultKind.Wait, firstBattle.Kind);
			Assert.True(secondBattle.IsSuccess);
			Assert.Equal("下一节点", secondBattle.Status);
			Assert.Equal(new List<int>(1) { 1 }, runRecord.CriticalHistory);
			Assert.True(back.IsSuccess);
			Assert.Equal(new List<int>(2) { 0, 1 }, services.ChosenTeamIndexes);
			Assert.Equal(new List<int>(2) { 0, 1 }, services.BattleTeamIndexes);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ShiyuDefenseOperation_RunsInjectedMultiRoomFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 5
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config)
			{
				CriticalHistory = new List<int>(4) { 1, 2, 3, 4 }
			};
			RecordingShiyuDefenseServices services = new RecordingShiyuDefenseServices
			{
				NextNodeIndex = 5,
				MultiRoomTeams = new List<DefensePhaseTeamInfo>(3)
				{
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.ELECTRIC }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 0
					},
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.FIRE }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = -1
					},
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.ICE }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 2
					}
				}
			};
			ShiyuDefenseOperation operation = new ShiyuDefenseOperation(context, config, runRecord, services);
			await operation.Transport().WaitAsync(TimeSpan.FromSeconds(2L));
			await operation.WaitLoading().WaitAsync(TimeSpan.FromSeconds(2L));
			await operation.ChooseNodeIndex().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult weakness = await operation.CheckWeakness().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult selectFirst = await operation.MultiRoomSelect().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult prepareFirst = await operation.MultiRoomWaitPrepare().WaitAsync(TimeSpan.FromSeconds(2L));
			int firstRoomIndex = operation.CurrentRoomIndex;
			await operation.MultiRoomDeploy().WaitAsync(TimeSpan.FromSeconds(2L));
			await operation.MultiRoomBattle().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult exitFirst = await operation.MultiRoomExit().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult selectSecond = await operation.MultiRoomSelect().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(weakness.IsSuccess);
			Assert.Equal("多间模式", weakness.Status);
			Assert.True(selectFirst.IsSuccess);
			Assert.Equal(0, firstRoomIndex);
			Assert.True(prepareFirst.IsSuccess);
			Assert.True(exitFirst.IsSuccess);
			Assert.True(selectSecond.IsSuccess);
			Assert.Equal(2, operation.CurrentRoomIndex);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ShiyuDefenseOperation_NormalWeaknessClicksAvatarBeforeChooseTeam()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 3
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config)
			{
				CriticalHistory = new List<int>(1) { 1 }
			};
			RecordingShiyuDefenseServices services = new RecordingShiyuDefenseServices
			{
				NextNodeIndex = 2,
				PhaseTeams = new List<DefensePhaseTeamInfo>(2)
				{
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.ELECTRIC }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 0
					},
					new DefensePhaseTeamInfo(new DmgTypeEnum[] { DmgTypeEnum.FIRE }, Array.Empty<DmgTypeEnum>())
					{
						TeamIndex = 1
					}
				}
			};
			ShiyuDefenseOperation operation = new ShiyuDefenseOperation(context, config, runRecord, services);
			await operation.ChooseNodeIndex().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult weakness = await operation.CheckWeakness().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(weakness.IsSuccess);
			Assert.Equal("角色头像", weakness.Status);
			Assert.Equal(TimeSpan.FromSeconds(1L), weakness.Delay);
			Assert.Equal(1, services.EnterTeamSelectionCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task ShiyuDefenseOperation_ToNextNodeUsesPythonSettlementStatus()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 7
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config);
			RecordingShiyuDefenseServices services = new RecordingShiyuDefenseServices
			{
				NextNodeIndex = 4,
				AdvanceResult = new OperationResult(IsSuccess: true, "节点-05", 5)
			};
			ShiyuDefenseOperation operation = new ShiyuDefenseOperation(context, config, runRecord, services);
			await operation.ChooseNodeIndex().WaitAsync(TimeSpan.FromSeconds(2L));
			OperationRoundResult next = await operation.ToNextNode().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(next.IsSuccess);
			Assert.Equal("节点-05", next.Status);
			Assert.Equal(5, operation.CurrentNodeIndex);
			Assert.Equal(4, services.AdvanceFromNodeIndex);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_WaitForMainScreen_ReturnsRetryWithoutScreenshot()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			OperationResult result = await services.WaitForMainScreenAsync(context, null);
			Assert.False(result.IsSuccess);
			Assert.Equal("未获取截图", result.Status);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_WaitForMainScreen_DetectsBattleReport()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 16, 8, "战报") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.WaitForMainScreenAsync(context, screen);
			Assert.True(result.IsSuccess);
			Assert.Equal("战报", result.Status);
			Assert.Equal(0, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_WaitForMainScreen_ClosesPreviousBestRecordPopup()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "前次行动最佳记录") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.WaitForMainScreenAsync(context, screen);
			Assert.True(result.IsSuccess);
			Assert.Equal("前次行动最佳记录", result.Status);
			Assert.Equal(1, controller.ClickCount);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(40, 40), controller.LastClickPoint);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_SelectNodeAsync_ClicksConfiguredNode()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 16, 8, "节点01") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.SelectNodeAsync(context, 1, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("节点-01", result.Status);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_GetNextNodeIndexAsync_ReadsProgressOcrAndUpdatesMaxNode()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "3/5") });
			context.ScreenContext.Reload();
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 7
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config)
			{
				CriticalHistory = new List<int>(1) { 1 }
			};
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			Assert.Equal(actual: await services.GetNextNodeIndexAsync(context, config, runRecord, screen).WaitAsync(TimeSpan.FromSeconds(2L)), expected: 4);
			Assert.Equal(5, config.CriticalMaxNodeIndex);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_GetNextNodeIndexAsync_FallsBackToRunRecordWhenProgressMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			ShiyuDefenseConfig config = new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 3
			};
			ShiyuDefenseRunRecord runRecord = new ShiyuDefenseRunRecord(config)
			{
				CriticalHistory = new List<int>(1) { 1 }
			};
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			Assert.Equal(actual: await services.GetNextNodeIndexAsync(context, config, runRecord, screen).WaitAsync(TimeSpan.FromSeconds(2L)), expected: 2);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_SelectNodeAsync_DragsNodeAreaWhenNodeIsNotVisible()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.SelectNodeAsync(context, 1, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("未找到 节点-01", result.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(50, 50), controller.LastDragStart);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(-250, 50), controller.LastDragEnd);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_SelectRoomAsync_ClicksTargetRoom()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "属性") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.SelectRoomAsync(context, 0, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("前往第一间", result.Status);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_WaitAndChooseMultiRoomTeam_ClicksNextWhenPrepareMissing()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.WaitAndChooseMultiRoomTeamAsync(context, 0, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("下一步", result.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(75, 85), controller.LastClickPoint);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_AdvanceAfterBattle_SelectsNodeFiveOnSameFrame()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 0, 0, 16, 8, "退出"),
				new OcrMatchResult(0.99, 0, 0, 16, 8, "节点05")
			});
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.AdvanceAfterBattleAsync(context, 4, new ShiyuDefenseConfig
			{
				CriticalMaxNodeIndex = 7
			}, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("节点-05", result.Status);
			Assert.Equal(5, result.Data);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_ClaimRewardAsync_ClicksRewardEntryFromMainScreen()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 16, 8, "战报") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.ClaimRewardAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("奖励入口", result.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(15, 75), controller.LastClickPoint);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_ClaimRewardAsync_ClicksClaimAllInsideRewardPanel()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 40, 8, "剧变节点奖励领取") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.ClaimRewardAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("全部领取", result.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(75, 75), controller.LastClickPoint);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task DefaultShiyuDefenseOperationServices_CloseRewardAsync_ClicksConfirm()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "确认") });
			context.ScreenContext.Reload();
			DefaultShiyuDefenseOperationServices services = new DefaultShiyuDefenseOperationServices();
			using Mat screen = Capture(controller);
			OperationResult result = await services.CloseRewardAsync(context, screen).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("领取奖励-确认", result.Status);
			Assert.Equal(1, controller.ClickCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_IsBattleScreenReady_ReturnsFalseWithoutScreenshot()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			bool condition = defaultShiyuDefenseBattleServices.IsBattleScreenReady(zContext, null);
			Assert.False(condition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_IsBattleScreenReady_DetectsNormalAttackButton()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "普通攻击") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			bool condition = defaultShiyuDefenseBattleServices.IsBattleScreenReady(zContext, screen);
			Assert.True(condition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_IsBattleScreenReady_RejectsInteractButton()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 16, 8, "交互") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			bool condition = defaultShiyuDefenseBattleServices.IsBattleScreenReady(zContext, screen);
			Assert.False(condition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_IsBattleScreenReady_ReturnsFalseWhenBattleUiIsMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			bool condition = defaultShiyuDefenseBattleServices.IsBattleScreenReady(zContext, screen);
			Assert.False(condition);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ImageAnalysisPipelineRunner_CountdownUsesExactlyFourContoursAndAbsoluteCoordinates()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.ScreenContext.Reload();
			TaskCompletionSource<PerformanceMetricSample> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
			using IDisposable subscription = zContext.EventBus.Subscribe<PerformanceMetricEventPayload>(
				PerformanceMetricEventIds.Sample,
				envelope =>
				{
					if (envelope.Payload.Sample.Metric == "cv_pipeline_ms")
					{
						received.TrySetResult(envelope.Payload.Sample);
					}
				});
			using Mat mat = new Mat(new Size(160, 90), MatType.CV_8UC3, Scalar.Black);
			for (int i = 0; i < 4; i++)
			{
				Cv2.Rectangle(mat, new OpenCvSharp.Rect(15 + i * 15, 45, 5, 10), Scalar.White, -1);
			}
			ImageAnalysisPipelineRunResult imageAnalysisPipelineRunResult = new ImageAnalysisPipelineRunner().Run(zContext, "防卫战倒计时", mat);
			Assert.True(imageAnalysisPipelineRunResult.IsSuccess);
			Assert.Equal(4, imageAnalysisPipelineRunResult.Contours.Count);
			Assert.All(imageAnalysisPipelineRunResult.Contours, delegate(ImageAnalysisContour contour)
			{
				Assert.InRange(contour.Rect.Y, 45, 54);
			});
			PerformanceMetricSample sample = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
			Assert.Equal("cv_pipeline_ms", sample.Metric);
			Assert.True(sample.Value >= 0d);
			Assert.Equal("防卫战倒计时", sample.Metadata!["pipeline"]);
			Assert.Equal(imageAnalysisPipelineRunResult.PipelinePath, sample.Metadata["pipeline_path"]);
			OverlayDebugSnapshot overlaySnapshot = zContext.OverlayDebugBus.Snapshot();
			Assert.Equal(4, overlaySnapshot.VisionItems.Count(item => item.Source == "cv" && item.Label == "防卫战倒计时"));
			Assert.All(overlaySnapshot.VisionItems.Where(item => item.Source == "cv"), item => Assert.Equal(VisionCoordinateSpace.StandardGame, item.CoordinateSpace));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_PrepareBattleStartsOnlyAfterCountdownPipelineFindsFourContours()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.ScreenContext.Reload();
			using Mat mat = new Mat(new Size(160, 90), MatType.CV_8UC3, Scalar.Black);
			for (int i = 0; i < 4; i++)
			{
				Cv2.Rectangle(mat, new OpenCvSharp.Rect(15 + i * 15, 45, 5, 10), Scalar.White, -1);
			}
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			OperationResult operationResult = defaultShiyuDefenseBattleServices.PrepareBattle(zContext, mat);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal(0, testScreenshotController.MoveWCount);
			Assert.Equal(0, testScreenshotController.TurnByDistanceCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_MoveAfterBattleUsesTeleportPipelineBeforeBlindTurn()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "普通攻击") });
			zContext.ScreenContext.Reload();
			using Mat mat = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.Black);
			Cv2.Rectangle(mat, new OpenCvSharp.Rect(900, 400, 120, 100), BgrFromHsv(125, 200, 100), -1);
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			OperationResult operationResult = defaultShiyuDefenseBattleServices.MoveAfterBattle(zContext, mat);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("等待战斗后移动", operationResult.Status);
			Assert.Equal(1, testScreenshotController.MoveWCount);
			Assert.Equal(0, testScreenshotController.TurnByDistanceCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_MissingCountdownPipelineStillUsesRealTeleportDetection()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text, writePipelines: false);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "普通攻击") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(testScreenshotController);
			OperationResult operationResult = defaultShiyuDefenseBattleServices.MoveAfterBattle(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("等待战斗后移动", operationResult.Status);
			Assert.Equal(0, testScreenshotController.MoveWCount);
			Assert.Equal(1, testScreenshotController.TurnByDistanceCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_RunAutoBattle_UsesPreviousAutoBattleEndResult()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			zContext.AutoBattleContext.LastCheckEndResult = "下一阶段";
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			OperationResult operationResult = defaultShiyuDefenseBattleServices.RunAutoBattle(zContext, null, DateTimeOffset.UtcNow);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("下一阶段", operationResult.Status);
			Assert.False(zContext.AutoBattleContext.IsRuntimeRunning);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_RunAutoBattle_LeavesCurrentFrameResultForNextRound()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "完成") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			OperationResult operationResult = defaultShiyuDefenseBattleServices.RunAutoBattle(zContext, screen, DateTimeOffset.UtcNow);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("自动战斗中", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_RunAutoBattle_UsesSuppliedFrameWithoutCapturingAgain()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 12, 8, "撤退") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(testScreenshotController);
			int screenshotCount = testScreenshotController.ScreenshotCount;
			OperationResult operationResult = defaultShiyuDefenseBattleServices.RunAutoBattle(zContext, screen, DateTimeOffset.UtcNow);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("自动战斗中", operationResult.Status);
			Assert.Equal(screenshotCount, testScreenshotController.ScreenshotCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_RunAutoBattle_UsesCurrentFrameTimestampForCountdown()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			OperationResult operationResult = defaultShiyuDefenseBattleServices.RunAutoBattle(zContext, screen, utcNow);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("自动战斗中", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_MoveAfterBattle_InteractsWhenInteractButtonVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 16, 8, "交互") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(testScreenshotController);
			OperationResult operationResult = defaultShiyuDefenseBattleServices.MoveAfterBattle(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("等待交互完成", operationResult.Status);
			Assert.Equal(1, testScreenshotController.InteractCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_MoveAfterBattle_MovesForwardWhenBattleUiStillVisible()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[] { new OcrMatchResult(0.99, 0, 0, 20, 8, "普通攻击") });
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(testScreenshotController);
			OperationResult operationResult = defaultShiyuDefenseBattleServices.MoveAfterBattle(zContext, screen);
			Assert.False(operationResult.IsSuccess);
			Assert.Equal("等待战斗后移动", operationResult.Status);
			Assert.Equal(1, testScreenshotController.TurnByDistanceCount);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_MoveAfterBattle_ReturnsNextPhaseWhenBattleUiDisappears()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			zContext.AttachController(controller);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			using Mat screen = Capture(controller);
			OperationResult operationResult = defaultShiyuDefenseBattleServices.MoveAfterBattle(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("下一阶段", operationResult.Status);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void DefaultShiyuDefenseBattleServices_PrepareVoluntaryExit_ClicksMenuWhenExitButtonMissing()
	{
		string text = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(text);
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text, text));
			zContext.AttachController(new ReadyController());
			using TestScreenshotController testScreenshotController = new TestScreenshotController();
			zContext.AttachController(testScreenshotController);
			zContext.OcrService.Matcher = new FakeOcrMatcher(Array.Empty<OcrMatchResult>());
			zContext.ScreenContext.Reload();
			using Mat screen = new Mat(new Size(160, 90), MatType.CV_8UC3, Scalar.Black);
			DefaultShiyuDefenseBattleServices defaultShiyuDefenseBattleServices = new DefaultShiyuDefenseBattleServices();
			OperationResult operationResult = defaultShiyuDefenseBattleServices.PrepareVoluntaryExit(zContext, screen);
			Assert.True(operationResult.IsSuccess);
			Assert.Equal("菜单", operationResult.Status);
			Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(15, 15), testScreenshotController.LastClickPoint);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task ShiyuDefenseBattle_FailureExitFlowUsesScreenshotsAndReturnsFailure()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteShiyuDefenseScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ReadyController());
			using TestScreenshotController controller = new TestScreenshotController();
			context.AttachController(controller);
			context.OcrService.Matcher = new FakeOcrMatcher(new OcrMatchResult[2]
			{
				new OcrMatchResult(0.99, 0, 0, 16, 8, "撤退"),
				new OcrMatchResult(0.99, 0, 0, 16, 8, "战报")
			});
			context.ScreenContext.Reload();
			ShiyuDefenseBattle operation = new ShiyuDefenseBattle(context, 0, new FailureShiyuDefenseBattleServices());
			OperationResult result = await operation.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.False(result.IsSuccess);
			Assert.Equal("战斗结束-撤退", result.Status);
			Assert.True(controller.ClickCount > 0);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void ShiyuDefenseOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(ShiyuDefenseOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[19]
		{
			"传送", "等待画面加载", "选择节点", "识别弱点并计算配队", "选择配队", "多间-选择房间", "多间-等待预备编队", "多间-出战", "多间-战斗", "多间-战斗结束",
			"多间-返回主界面", "多间-战斗失败", "出战", "自动战斗", "下一节点", "所有节点完成", "领取奖励", "关闭奖励", "结束后返回"
		}, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["传送"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["领取奖励"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "选择节点" && edge.Status == "所有节点都完成挑战");
		Assert.Contains(readOnlyDictionary["多间-选择房间"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "多间-战斗结束" && edge.Status == "房间挑战完成");
		Assert.Contains(readOnlyDictionary["结束后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "关闭奖励");
	}

	[Fact]
	public void ShiyuDefenseBattle_DeclaresPythonBattleNodes()
	{
		string[] actualArray = (from method in typeof(ShiyuDefenseBattle).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select method.GetCustomAttribute<OperationNodeAttribute>() into attribute
			where attribute != null
			select attribute.Name).ToArray();
		string[] buffer = new string[11];
		buffer[0] = "加载自动战斗指令";
		buffer[1] = "等待战斗画面加载";
		buffer[2] = "向前移动准备战斗";
		buffer[3] = "自动战斗";
		buffer[4] = "战斗后移动";
		buffer[5] = "战斗超时";
		buffer[6] = "主动退出";
		buffer[7] = "点击退出";
		buffer[8] = "点击退出确认";
		buffer[9] = "战斗失败撤退";
		buffer[10] = "等待退出";
		Assert.Equal(buffer, actualArray);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteShiyuDefenseScreenYaml(string rootDirectory, bool writePipelines = true)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		ScreenSeed.WriteScreens(text, "- screen_id: shiyu_defense\n  screen_name: 式舆防卫战\n  app_id: shiyu_defense\n  area_list:\n    - area_name: 战报\n      id_mark: true\n      pc_rect: [10, 10, 80, 30]\n      text: 战报\n      lcs_percent: 0.5\n    - area_name: 节点区域\n      pc_rect: [0, 0, 100, 100]\n    - area_name: 节点-01\n      pc_rect: [10, 10, 80, 30]\n      text: 节点01\n      lcs_percent: 0.5\n    - area_name: 节点-05\n      pc_rect: [10, 10, 80, 30]\n      text: 节点05\n      lcs_percent: 0.5\n    - area_name: 下一步\n      pc_rect: [10, 10, 80, 30]\n      text: 下一步\n      lcs_percent: 0.5\n    - area_name: 剧变节点进度\n      pc_rect: [10, 10, 80, 30]\n    - area_name: 角色头像\n      pc_rect: [30, 30, 50, 50]\n    - area_name: 前次行动最佳记录\n      pc_rect: [10, 10, 120, 30]\n      text: 前次行动最佳记录\n      lcs_percent: 0.5\n    - area_name: 前次-关闭\n      pc_rect: [30, 30, 50, 50]\n    - area_name: 奖励入口\n      pc_rect: [10, 70, 20, 80]\n    - area_name: 全部领取\n      pc_rect: [70, 70, 80, 80]\n    - area_name: 领取奖励-界面\n      pc_rect: [10, 10, 120, 30]\n      text: 剧变节点奖励领取\n      lcs_percent: 0.5\n    - area_name: 领取奖励-确认\n      pc_rect: [10, 10, 80, 30]\n      text: 确认\n      lcs_percent: 0.5\n    - area_name: 领取奖励-关闭\n      pc_rect: [90, 70, 100, 80]\n    - area_name: 战斗结束-撤退\n      pc_rect: [10, 10, 80, 30]\n      text: 撤退\n      lcs_percent: 0.5\n    - area_name: 战斗结束-下一防线\n      pc_rect: [10, 10, 80, 30]\n      text: 下一防线\n      lcs_percent: 0.5\n    - area_name: 战斗结束-退出\n      pc_rect: [10, 10, 80, 30]\n      text: 退出\n      lcs_percent: 0.8\n    - area_name: 退出战斗\n      pc_rect: [10, 10, 80, 30]\n      text: 退出战斗\n      lcs_percent: 0.5\n- screen_id: shiyu_defense_select_3\n  screen_name: 式舆防卫战-三间选择\n  app_id: shiyu_defense\n  area_list:\n    - area_name: 本期最佳总分\n      id_mark: true\n      pc_rect: [10, 10, 80, 30]\n      text: 总分\n      lcs_percent: 0.5\n    - area_name: 确认\n      pc_rect: [10, 70, 20, 80]\n      text: 确认\n      lcs_percent: 0.5\n    - area_name: 第一间\n      pc_rect: [10, 10, 80, 30]\n    - area_name: 第二间\n      pc_rect: [10, 30, 80, 50]\n    - area_name: 第三间\n      pc_rect: [10, 50, 80, 70]\n    - area_name: 重置全部\n      pc_rect: [90, 70, 100, 80]\n    - area_name: 前往第一间\n      pc_rect: [10, 10, 80, 30]\n      text: 属性\n      lcs_percent: 0.5\n    - area_name: 前往第二间\n      pc_rect: [10, 30, 80, 50]\n      text: 属性\n      lcs_percent: 0.5\n    - area_name: 前往第三间\n      pc_rect: [10, 50, 80, 70]\n      text: 属性\n      lcs_percent: 0.5\n- screen_id: combat_simulation\n  screen_name: 实战模拟室\n  area_list:\n    - area_name: 预备编队\n      pc_rect: [10, 10, 80, 30]\n      text: 预备编队\n      lcs_percent: 0.5\n    - area_name: 下一步\n      pc_rect: [70, 80, 80, 90]\n    - area_name: 出战\n      pc_rect: [80, 80, 90, 90]\n- screen_id: battle\n  screen_name: 战斗画面\n  area_list:\n    - area_name: 菜单\n      pc_rect: [10, 10, 20, 20]\n    - area_name: 按键-普通攻击\n      pc_rect: [10, 10, 80, 30]\n      text: 普通攻击\n      lcs_percent: 0.5\n    - area_name: 按键-交互\n      pc_rect: [10, 10, 80, 30]\n      text: 交互\n      lcs_percent: 0.5\n    - area_name: 距离显示区域\n      pc_rect: [10, 60, 80, 90]\n    - area_name: 战斗结果-完成\n      pc_rect: [10, 10, 80, 30]\n      text: 完成\n      lcs_percent: 0.5\n    - area_name: 战斗结果-撤退\n      pc_rect: [10, 10, 80, 30]\n      text: 撤退\n      lcs_percent: 0.5\n    - area_name: 式舆防卫战-倒计时\n      pc_rect: [10, 40, 80, 60]\n      text: 倒计时\n      lcs_percent: 0.5\n    - area_name: 式舆防卫战-倒计时-精英\n      pc_rect: [10, 40, 80, 60]\n      text: 倒计时精英\n      lcs_percent: 0.5\n- screen_id: menu\n  screen_name: 菜单\n  area_list:\n    - area_name: 返回\n      pc_rect: [10, 10, 20, 20]");
		if (writePipelines)
		{
			WriteShiyuDefensePipelineYaml(rootDirectory);
		}
	}

	private static void WriteShiyuDefensePipelineYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "image_analysis_pipelines");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "防卫战倒计时.yml"), "- step: 按区域裁剪\n  params:\n    screen_name: 战斗画面\n    area_name: 式舆防卫战-倒计时\n- step: HSV 范围过滤\n  params:\n    hsv_color: [0, 0, 255]\n    hsv_diff: [90, 255, 200]\n- step: 腐蚀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 膨胀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 查找轮廓\n  params: { mode: EXTERNAL, method: SIMPLE }");
		File.WriteAllText(Path.Combine(text, "防卫战倒计时-精英.yml"), "- step: 按区域裁剪\n  params:\n    screen_name: 战斗画面\n    area_name: 式舆防卫战-倒计时-精英\n- step: HSV 范围过滤\n  params:\n    hsv_color: [0, 0, 255]\n    hsv_diff: [90, 255, 200]\n- step: 腐蚀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 膨胀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 查找轮廓\n  params: { mode: EXTERNAL, method: SIMPLE }");
		File.WriteAllText(Path.Combine(text, "防卫战空洞传送点.yml"), "- step: HSV 范围过滤\n  params:\n    hsv_color: [125, 200, 100]\n    hsv_diff: [10, 80, 50]\n- step: 腐蚀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 膨胀\n  params: { kernel_size: 3, iterations: 1 }\n- step: 查找轮廓\n  params: { mode: EXTERNAL, method: SIMPLE }\n- step: 按面积过滤\n  params: { min_area: 5000, max_area: 99999 }");
	}

	private static Scalar BgrFromHsv(byte hue, byte saturation, byte value)
	{
		using Mat mat = new Mat(1, 1, MatType.CV_8UC3, new Scalar((int)hue, (int)saturation, (int)value));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.HSV2BGR);
		Vec3b vec3b = mat2.At<Vec3b>(0, 0);
		return new Scalar((int)vec3b.Item0, (int)vec3b.Item1, (int)vec3b.Item2);
	}

	private static Mat Capture(TestScreenshotController controller)
	{
		return controller.Screenshot().Screen ?? throw new InvalidOperationException("测试控制器未返回截图");
	}
}
