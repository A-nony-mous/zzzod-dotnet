using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class RedemptionCodeAppTests
{
	private sealed class RecordingRedemptionCodeFlow : IRedemptionCodeAppFlow
	{
		public int RunCount { get; private set; }

		public Task<OperationResult> RunAsync(ZContext context, RedemptionCodeConfig config, RedemptionCodeRunRecord runRecord, CancellationToken cancellationToken)
		{
			RunCount++;
			return Task.FromResult(new OperationResult(IsSuccess: true, "兑换码完成"));
		}
	}

	[Fact]
	public void Factory_ExposesPythonMetadataAndCreatesRedemptionCodeApp()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RedemptionCodeFactory redemptionCodeFactory = zContext.ApplicationFactoryRegistry.CreateRedemptionCodeFactory();
			IApplication application = redemptionCodeFactory.CreateApplication(0, "one_dragon");
			IApplicationConfig config = redemptionCodeFactory.GetConfig(0, "one_dragon");
			IApplicationRunRecord runRecord = redemptionCodeFactory.GetRunRecord(0);
			Assert.Equal("redemption_code", redemptionCodeFactory.AppId);
			Assert.Equal("兑换码", redemptionCodeFactory.AppName);
			Assert.Equal("one_dragon", redemptionCodeFactory.GroupId);
			Assert.True(redemptionCodeFactory.NeedNotify);
			Assert.True(condition: true);
			Assert.IsType<RedemptionCodeApp>(application);
			Assert.IsType<RedemptionCodeConfig>(config);
			RedemptionCodeRunRecord redemptionCodeRunRecord = Assert.IsType<RedemptionCodeRunRecord>(runRecord);
			Assert.Equal("redemption_code", redemptionCodeRunRecord.AppId);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void Registry_RegistersRedemptionCodeAsDefaultNotifyApplication()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			zContext.ApplicationFactoryRegistry.RegisterRedemptionCodeApplication();
			Assert.True(zContext.RunContext.IsAppRegistered("redemption_code"));
			Assert.True(zContext.RunContext.IsAppNeedNotify("redemption_code"));
			Assert.Contains("redemption_code", (IEnumerable<string>)zContext.RunContext.DefaultGroupApps);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeConfig_LoadsAndMergesPythonCompatibleGlobalYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "redemption_codes.sample.yml"), "codes:\n  SAMPLE1: 20990101\n  OVERRIDE: 20260707");
			File.WriteAllText(Path.Combine(text2, "redemption_codes.yml"), "codes:\n  USER1: 20990102\n  OVERRIDE: 20991231\nunknown_field: \"ignored\"");
			RedemptionCodeConfig redemptionCodeConfig = RedemptionCodeConfig.Load(new OneDragonEnvironment(text), 3, "custom");
			Assert.Equal("redemption_code", redemptionCodeConfig.AppId);
			Assert.Equal(3, redemptionCodeConfig.InstanceIndex);
			Assert.Equal("custom", redemptionCodeConfig.GroupId);
			Assert.Equal(3, redemptionCodeConfig.CodesDict.Count);
			Assert.Equal(20990101, redemptionCodeConfig.CodesDict["SAMPLE1"]);
			Assert.Equal(20990102, redemptionCodeConfig.CodesDict["USER1"]);
			Assert.Equal(20991231, redemptionCodeConfig.CodesDict["OVERRIDE"]);
			Assert.Equal(new string[3] { "SAMPLE1", "OVERRIDE", "USER1" }, redemptionCodeConfig.CodesList);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeConfig_UpdatesUserAndSampleCodes()
	{
		string text = CreateTempRoot();
		try
		{
			RedemptionCodeConfig redemptionCodeConfig = new RedemptionCodeConfig(new OneDragonEnvironment(text));
			redemptionCodeConfig.AddCode(" USER1 ");
			redemptionCodeConfig.UpdateCode("USER1", "USER2", 20990102);
			redemptionCodeConfig.AddSampleCode("SAMPLE1", 20260705);
			redemptionCodeConfig.AddSampleCode("SAMPLE2", 20260707);
			int actual = redemptionCodeConfig.CleanExpiredSampleCodes(20260706);
			redemptionCodeConfig.DeleteCode("USER2");
			Assert.Equal(1, actual);
			Assert.DoesNotContain("USER2", redemptionCodeConfig.UserCodesDict.Keys);
			Assert.DoesNotContain("SAMPLE1", redemptionCodeConfig.SampleCodesDict.Keys);
			Assert.Equal(20260707, redemptionCodeConfig.SampleCodesDict["SAMPLE2"]);
			Assert.Contains("SAMPLE2", File.ReadAllText(Path.Combine(text, "config", "redemption_codes.sample.yml")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeRunRecord_TracksUnusedCodesAndResetsWhenNewCodeExists()
	{
		RedemptionCodeRunRecord redemptionCodeRunRecord = new RedemptionCodeRunRecord(0, () => new DateTimeOffset(2026, 7, 6, 1, 0, 0, TimeSpan.Zero), () => CreateInMemoryConfig(new Dictionary<string, int>
		{
			["VALID"] = 20260706,
			["EXPIRED"] = 20260705
		}));
		Assert.Equal(new string[] { "VALID" }, redemptionCodeRunRecord.GetUnusedCodeList("20260706"));
		redemptionCodeRunRecord.AddUsedCode("VALID");
		Assert.Empty(redemptionCodeRunRecord.GetUnusedCodeList("20260706"));
		redemptionCodeRunRecord.UpdateStatus(1);
		Assert.True(redemptionCodeRunRecord.IsDone);
		redemptionCodeRunRecord.UsedCodeList.Clear();
		redemptionCodeRunRecord.CheckAndUpdateStatus();
		Assert.Equal(0, redemptionCodeRunRecord.RunStatus);
	}

	[Fact]
	public void RedemptionCodeRunRecord_AddUsedCodePersistsToYaml()
	{
		string text = CreateTempRoot();
		try
		{
			string text2 = Path.Combine(text, "config");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "redemption_codes.yml"), "codes:\n  CODE1: 20990101");
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			RedemptionCodeRunRecord redemptionCodeRunRecord = RedemptionCodeRunRecord.Load(environment, 0);
			redemptionCodeRunRecord.AddUsedCode("CODE1");
			RedemptionCodeRunRecord redemptionCodeRunRecord2 = RedemptionCodeRunRecord.Load(environment, 0);
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "CODE1";
			Assert.Equal<List<string>>(list, redemptionCodeRunRecord2.UsedCodeList);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeApp_DefaultConstructorLoadsExistingRunRecord()
	{
		string text = CreateTempRoot();
		try
		{
			string path = Path.Combine(text, "config");
			string text2 = Path.Combine(path, "00", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(path, "redemption_codes.yml"), "codes:\n  CODE1: 20990101");
			File.WriteAllText(Path.Combine(text2, "redemption_code.yml"), "used_code_list:\n  - CODE1");
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RedemptionCodeApp redemptionCodeApp = new RedemptionCodeApp(zContext);
			RedemptionCodeRunRecord redemptionCodeRunRecord = Assert.IsType<RedemptionCodeRunRecord>(redemptionCodeApp.RunRecord);
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "CODE1";
			Assert.Equal<List<string>>(list, redemptionCodeRunRecord.UsedCodeList);
			Assert.Empty(redemptionCodeRunRecord.GetUnusedCodeList("20260716"));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public async Task RedemptionCodeApp_RunsInjectedFlowAndUpdatesRecord()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RecordingRedemptionCodeFlow flow = new RecordingRedemptionCodeFlow();
			RedemptionCodeRunRecord runRecord = new RedemptionCodeRunRecord(0, null, () => CreateInMemoryConfig(new Dictionary<string, int>()));
			RedemptionCodeApp app = new RedemptionCodeApp(context, new RedemptionCodeConfig(context.Environment), runRecord, flow);
			OperationResult result = await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(result.IsSuccess);
			Assert.Equal("兑换码完成", result.Status);
			Assert.Equal(1, flow.RunCount);
			Assert.Equal(1, runRecord.RunStatus);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public async Task RedemptionCodeOperation_UsesInjectedInputAndBackFlowWithoutGameWindow()
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory));
			context.AttachController(new ReadyController());
			RedemptionCodeRunRecord runRecord = new RedemptionCodeRunRecord(0, null, () => CreateInMemoryConfig(new Dictionary<string, int> { ["CODE1"] = 20990101 }));
			int backCount = 0;
			string inputCode = null;
			RedemptionCodeOperation operation = new RedemptionCodeOperation(context, runRecord, delegate
			{
				backCount++;
				return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
			}, delegate(ZContext _, string code)
			{
				inputCode = code;
			}, () => new OperationRoundResult(OperationRoundResultKind.Success, "兑换码输入框"), () => new OperationRoundResult(OperationRoundResultKind.Success, "兑换码兑换"), () => new OperationRoundResult(OperationRoundResultKind.Success, "兑换码兑换"), delegate
			{
			});
			OperationRoundResult check = operation.CheckNewCode();
			OperationRoundResult input = operation.InputCode();
			OperationRoundResult confirm = operation.ConfirmCode();
			OperationRoundResult finish = operation.InputCode();
			OperationRoundResult back = await operation.Back().WaitAsync(TimeSpan.FromSeconds(2L));
			Assert.True(check.IsSuccess);
			Assert.Equal("有新的兑换码", check.Status);
			Assert.True(input.IsSuccess);
			Assert.Equal("兑换码兑换", input.Status);
			Assert.Equal("CODE1", inputCode);
			Assert.True(confirm.IsSuccess);
			Assert.Equal<List<string>>(new List<string>(1) { "CODE1" }, runRecord.UsedCodeList);
			Assert.True(finish.IsSuccess);
			Assert.Equal("全部兑换完毕", finish.Status);
			Assert.True(back.IsSuccess);
			Assert.Equal("返回大世界", back.Status);
			Assert.Equal(1, backCount);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeOperation_InputCodeContinuesAfterInputBoxClickFails()
	{
		string text = CreateTempRoot();
		try
		{
			using ZContext zContext = new ZContext(new OneDragonEnvironment(text));
			zContext.AttachController(new ReadyController());
			RedemptionCodeRunRecord runRecord = new RedemptionCodeRunRecord(0, null, () => CreateInMemoryConfig(new Dictionary<string, int> { ["CODE1"] = 20990101 }));
			string inputCode = null;
			RedemptionCodeOperation redemptionCodeOperation = new RedemptionCodeOperation(zContext, runRecord, null, delegate(ZContext _, string code)
			{
				inputCode = code;
			}, () => new OperationRoundResult(OperationRoundResultKind.Retry, "点击失败 兑换码输入框"), () => new OperationRoundResult(OperationRoundResultKind.Success, "兑换码兑换"), null, delegate
			{
			});
			redemptionCodeOperation.CheckNewCode();
			OperationRoundResult operationRoundResult = redemptionCodeOperation.InputCode();
			Assert.True(operationRoundResult.IsSuccess);
			Assert.Equal("兑换码兑换", operationRoundResult.Status);
			Assert.Equal("CODE1", inputCode);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	[Fact]
	public void RedemptionCodeOperation_DeclaresPythonFlowNodesAndEdges()
	{
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in typeof(RedemptionCodeOperation).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[7] { "检测新兑换码", "打开菜单", "点击更多", "点击兑换码", "输入兑换码", "兑换后确认", "返回大世界" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["检测新兑换码"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["打开菜单"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检测新兑换码" && edge.Status == "有新的兑换码");
		Assert.Contains(readOnlyDictionary["输入兑换码"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "兑换后确认");
		Assert.Contains(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "输入兑换码" && edge.Status == "全部兑换完毕");
		Assert.DoesNotContain(readOnlyDictionary["返回大世界"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "检测新兑换码" && edge.Status == "无新的兑换码");
		Assert.Contains(readOnlyDictionary["兑换后确认"].GetCustomAttributes<OperationNodeNotifyAttribute>(), (OperationNodeNotifyAttribute annotation) => annotation.Timing == OperationNodeNotifyTiming.CurrentSuccess);
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static RedemptionCodeConfig CreateInMemoryConfig(Dictionary<string, int> codes)
	{
		string text = CreateTempRoot();
		string text2 = Path.Combine(text, "config");
		Directory.CreateDirectory(text2);
		File.WriteAllText(Path.Combine(text2, "redemption_codes.yml"), string.Concat("codes:\n", string.Concat(codes.Select<KeyValuePair<string, int>, string>((KeyValuePair<string, int> item) => $"  {item.Key}: {item.Value}\n"))));
		return new RedemptionCodeConfig(new OneDragonEnvironment(text));
	}
}
