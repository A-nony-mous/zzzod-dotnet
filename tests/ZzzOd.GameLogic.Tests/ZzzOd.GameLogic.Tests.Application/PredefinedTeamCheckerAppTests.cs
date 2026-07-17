using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class PredefinedTeamCheckerAppTests
{
	private sealed class RecordingCheckerFlow : IPredefinedTeamCheckerFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "预备编队角色识别完成"));
		}
	}

	private sealed class RecordingCheckerServices : IPredefinedTeamCheckerOperationServices
	{
		public string OcrTeamName { get; init; } = "猫叉队";

		public List<OneDragon.Core.Abstractions.Geometry.Rect> AvatarRects { get; } = new List<OneDragon.Core.Abstractions.Geometry.Rect>();

		public Task<OperationResult> GotoMenuAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "前往菜单画面"));
		}

		public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true, "成功后返回"));
		}

		public IReadOnlyDictionary<string, MatchResultList> RunOcr(ZContext context, Mat screen)
		{
			MatchResultList matchResultList = new MatchResultList(onlyBest: false);
			matchResultList.Append(new MatchResult(0.99, 100, 40, 80, 20), autoMerge: false);
			return new Dictionary<string, MatchResultList>(StringComparer.Ordinal) { [OcrTeamName] = matchResultList };
		}

		public IReadOnlyList<MatchResult> MatchTeamAgentTemplate(ZContext context, Mat screen, OneDragon.Core.Abstractions.Geometry.Rect avatarRect)
		{
			AvatarRects.Add(avatarRect);
			return new MatchResult[3]
			{
				new MatchResult(0.8, 150, 100, 50, 50, 1.0, AgentEnum.NEKOMATA.Value),
				new MatchResult(0.4, 250, 100, 50, 50, 1.0, AgentEnum.ANBY.Value),
				new MatchResult(0.95, 255, 100, 50, 50, 1.0, AgentEnum.NICOLE.Value)
			};
		}
	}

	private sealed record DragRecord(OneDragon.Core.Abstractions.Geometry.Point End, OneDragon.Core.Abstractions.Geometry.Point? Start);

	private sealed class RecordingController : ControllerBase, IDisposable
	{
		private readonly Mat _screenshot = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.Black);

		public List<DragRecord> Drags { get; } = new List<DragRecord>();

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
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
			return _screenshot.Clone();
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesPredefinedTeamCheckerApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			PredefinedTeamCheckerFactory predefinedTeamCheckerFactory = zContext.ApplicationFactoryRegistry.CreatePredefinedTeamCheckerFactory();
			IApplication application = predefinedTeamCheckerFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = predefinedTeamCheckerFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = predefinedTeamCheckerFactory.GetRunRecord(0);
			Assert.Equal("predefined_team_checker", "predefined_team_checker");
			Assert.Equal("预备编队角色识别", "预备编队角色识别");
			Assert.Equal("predefined_team_checker", predefinedTeamCheckerFactory.AppId);
			Assert.Equal("预备编队角色识别", predefinedTeamCheckerFactory.AppName);
			Assert.Equal("one_dragon", predefinedTeamCheckerFactory.GroupId);
			Assert.False(predefinedTeamCheckerFactory.NeedNotify);
			Assert.False(condition: false);
			Assert.IsType<PredefinedTeamCheckerApp>(application);
			Assert.IsType<ZApplicationConfig>(config);
			ZApplicationRunRecord zApplicationRunRecord = Assert.IsType<ZApplicationRunRecord>(runRecord);
			Assert.Equal("predefined_team_checker", zApplicationRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersPredefinedTeamCheckerWithoutDefaultGroupOrNotify()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterPredefinedTeamCheckerApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("predefined_team_checker"));
			Assert.False(zContext.RunContext.IsAppNeedNotify("predefined_team_checker"));
			Assert.DoesNotContain("predefined_team_checker", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task App_RunsInjectedCheckerFlowAndUpdatesRunRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			ZApplicationRunRecord runRecord = new ZApplicationRunRecord("predefined_team_checker");
			RecordingCheckerFlow flow = new RecordingCheckerFlow();
			PredefinedTeamCheckerApp app = new PredefinedTeamCheckerApp(context, runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("预备编队角色识别完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void Operation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(PredefinedTeamCheckerOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[5] { "前往菜单画面", "前往更多功能画面", "点击预备编队", "识别编队角色", "成功后返回" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["前往菜单画面"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["前往更多功能画面"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "前往菜单画面");
		Assert.Contains(readOnlyDictionary["点击预备编队"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "前往更多功能画面");
		Assert.Contains(readOnlyDictionary["识别编队角色"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "点击预备编队");
		Assert.Contains(readOnlyDictionary["成功后返回"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "识别编队角色");
	}

	[Fact]
	public void UpdateTeamMembers_MatchesOcrTeamNamesAndFiltersOverlappedAgents()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			TeamConfig teamConfig = zContext.TeamConfig;
			int num = 2;
			List<PredefinedTeamInfo> list = new List<PredefinedTeamInfo>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<PredefinedTeamInfo> span = CollectionsMarshal.AsSpan(list);
			ref PredefinedTeamInfo reference = ref span[0];
			int num2 = 3;
			List<string> list2 = new List<string>(num2);
			CollectionsMarshal.SetCount(list2, num2);
			Span<string> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = "unknown";
			span2[1] = "unknown";
			span2[2] = "unknown";
			reference = new PredefinedTeamInfo(0, "猫又队", "全配队通用", list2);
			ref PredefinedTeamInfo reference2 = ref span[1];
			num2 = 3;
			List<string> list3 = new List<string>(num2);
			CollectionsMarshal.SetCount(list3, num2);
			Span<string> span3 = CollectionsMarshal.AsSpan(list3);
			span3[0] = "unknown";
			span3[1] = "unknown";
			span3[2] = "unknown";
			reference2 = new PredefinedTeamInfo(1, "妮可队", "全配队通用", list3);
			teamConfig.TeamList = list;
			RecordingCheckerServices recordingCheckerServices = new RecordingCheckerServices();
			PredefinedTeamCheckerOperation predefinedTeamCheckerOperation = new PredefinedTeamCheckerOperation(zContext, recordingCheckerServices);
			using Mat screen = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.Black);
			predefinedTeamCheckerOperation.UpdateTeamMembers(screen);
			num = 3;
			List<string> list4 = new List<string>(num);
			CollectionsMarshal.SetCount(list4, num);
			Span<string> span4 = CollectionsMarshal.AsSpan(list4);
			span4[0] = "nekomata";
			span4[1] = "nicole";
			span4[2] = "unknown";
			Assert.Equal<List<string>>(list4, zContext.TeamConfig.TeamList[0].AgentIdList);
			num = 3;
			List<string> list5 = new List<string>(num);
			CollectionsMarshal.SetCount(list5, num);
			Span<string> span5 = CollectionsMarshal.AsSpan(list5);
			span5[0] = "unknown";
			span5[1] = "unknown";
			span5[2] = "unknown";
			Assert.Equal<List<string>>(list5, zContext.TeamConfig.TeamList[1].AgentIdList);
			YamlConfig<TeamConfig> yamlConfig = new YamlConfig<TeamConfig>(zContext.Environment, "team", null, zContext.InstanceIndex);
			num = 3;
			List<string> list6 = new List<string>(num);
			CollectionsMarshal.SetCount(list6, num);
			Span<string> span6 = CollectionsMarshal.AsSpan(list6);
			span6[0] = "nekomata";
			span6[1] = "nicole";
			span6[2] = "unknown";
			Assert.Equal<List<string>>(list6, yamlConfig.Current.TeamList[0].AgentIdList);
			num = 3;
			List<string> list7 = new List<string>(num);
			CollectionsMarshal.SetCount(list7, num);
			Span<string> span7 = CollectionsMarshal.AsSpan(list7);
			span7[0] = "unknown";
			span7[1] = "unknown";
			span7[2] = "unknown";
			Assert.Equal<List<string>>(list7, yamlConfig.Current.TeamList[1].AgentIdList);
			OneDragon.Core.Abstractions.Geometry.Rect rect = Assert.Single(recordingCheckerServices.AvatarRects);
			Assert.Equal(90, rect.X1);
			Assert.Equal(40, rect.Y1);
			Assert.Equal(900, rect.X2);
			Assert.Equal(290, rect.Y2);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void UpdateTeamMembers_UsesPythonDifflibCandidateOrderingForAmbiguousOcr()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			TeamConfig teamConfig = zContext.TeamConfig;
			int num = 2;
			List<PredefinedTeamInfo> list = new List<PredefinedTeamInfo>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<PredefinedTeamInfo> span = CollectionsMarshal.AsSpan(list);
			ref PredefinedTeamInfo reference = ref span[0];
			int num2 = 3;
			List<string> list2 = new List<string>(num2);
			CollectionsMarshal.SetCount(list2, num2);
			Span<string> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = "unknown";
			span2[1] = "unknown";
			span2[2] = "unknown";
			reference = new PredefinedTeamInfo(0, "ab", "全配队通用", list2);
			ref PredefinedTeamInfo reference2 = ref span[1];
			num2 = 3;
			List<string> list3 = new List<string>(num2);
			CollectionsMarshal.SetCount(list3, num2);
			Span<string> span3 = CollectionsMarshal.AsSpan(list3);
			span3[0] = "unknown";
			span3[1] = "unknown";
			span3[2] = "unknown";
			reference2 = new PredefinedTeamInfo(1, "ac", "全配队通用", list3);
			teamConfig.TeamList = list;
			RecordingCheckerServices services = new RecordingCheckerServices
			{
				OcrTeamName = "a"
			};
			PredefinedTeamCheckerOperation predefinedTeamCheckerOperation = new PredefinedTeamCheckerOperation(zContext, services);
			using Mat screen = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.Black);
			predefinedTeamCheckerOperation.UpdateTeamMembers(screen);
			num = 3;
			List<string> list4 = new List<string>(num);
			CollectionsMarshal.SetCount(list4, num);
			Span<string> span4 = CollectionsMarshal.AsSpan(list4);
			span4[0] = "unknown";
			span4[1] = "unknown";
			span4[2] = "unknown";
			Assert.Equal<List<string>>(list4, zContext.TeamConfig.TeamList[0].AgentIdList);
			num = 3;
			List<string> list5 = new List<string>(num);
			CollectionsMarshal.SetCount(list5, num);
			Span<string> span5 = CollectionsMarshal.AsSpan(list5);
			span5[0] = "nekomata";
			span5[1] = "nicole";
			span5[2] = "unknown";
			Assert.Equal<List<string>>(list5, zContext.TeamConfig.TeamList[1].AgentIdList);
			YamlConfig<TeamConfig> yamlConfig = new YamlConfig<TeamConfig>(zContext.Environment, "team", null, zContext.InstanceIndex);
			num = 3;
			List<string> list6 = new List<string>(num);
			CollectionsMarshal.SetCount(list6, num);
			Span<string> span6 = CollectionsMarshal.AsSpan(list6);
			span6[0] = "unknown";
			span6[1] = "unknown";
			span6[2] = "unknown";
			Assert.Equal<List<string>>(list6, yamlConfig.Current.TeamList[0].AgentIdList);
			num = 3;
			List<string> list7 = new List<string>(num);
			CollectionsMarshal.SetCount(list7, num);
			Span<string> span7 = CollectionsMarshal.AsSpan(list7);
			span7[0] = "nekomata";
			span7[1] = "nicole";
			span7[2] = "unknown";
			Assert.Equal<List<string>>(list7, yamlConfig.Current.TeamList[1].AgentIdList);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void CheckTeamMembers_DragsFourTimesThenSucceeds()
	{
		OpenCvTestRuntime.RequireAvailable();
		string text = CreateTempRoot();
		try
		{
			using RecordingController recordingController = new RecordingController();
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.AttachController(recordingController);
			PredefinedTeamCheckerOperation predefinedTeamCheckerOperation = new PredefinedTeamCheckerOperation(zContext, new RecordingCheckerServices(), TimeSpan.Zero);
			using Mat screen = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.Black);
			SetLastScreenshot(predefinedTeamCheckerOperation, screen);
			OperationRoundResult operationRoundResult = predefinedTeamCheckerOperation.CheckTeamMembers();
			OperationRoundResult operationRoundResult2 = predefinedTeamCheckerOperation.CheckTeamMembers();
			OperationRoundResult operationRoundResult3 = predefinedTeamCheckerOperation.CheckTeamMembers();
			OperationRoundResult operationRoundResult4 = predefinedTeamCheckerOperation.CheckTeamMembers();
			OperationRoundResult operationRoundResult5 = predefinedTeamCheckerOperation.CheckTeamMembers();
			Assert.False(operationRoundResult.IsSuccess);
			Assert.Equal("继续识别", operationRoundResult.Status);
			Assert.False(operationRoundResult2.IsSuccess);
			Assert.False(operationRoundResult3.IsSuccess);
			Assert.False(operationRoundResult4.IsSuccess);
			Assert.True(operationRoundResult5.IsSuccess);
			Assert.Equal(4, recordingController.Drags.Count);
			Assert.All(recordingController.Drags, delegate(DragRecord drag)
			{
				Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), drag.Start);
				Assert.Equal(new OneDragon.Core.Abstractions.Geometry.Point(960, 40), drag.End);
			});
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void SetLastScreenshot(PredefinedTeamCheckerOperation operation, Mat screen)
	{
		FieldInfo field = typeof(ZOperation).GetField("<LastScreenshot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		field.SetValue(operation, screen);
	}
}
