using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.Gui.Pages.Standalone;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 独立运行列表的数据保真和 BaselineParity 注册顺序测试。
/// </summary>
[Trait("Category", "GuiHeavy")]
public sealed class StandaloneRunProductionParityTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot)
		{
			_runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance);
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	/// <summary>
	/// 可见应用排序和多选新增顺序应使用生产注册表对应的 BaselineParity default-group 顺序。
	/// </summary>
	[Fact]
	public void ProductionAppsUsePythonDefaultGroupRegistrationOrderForStandaloneAdditions()
	{
		string text = CreateRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<IReadOnlyList<ZzzAppDto>> standaloneApps = backendSession.Backend.GetStandaloneApps();
			Assert.True(standaloneApps.Success, standaloneApps.Error);
			IReadOnlyList<ZzzAppDto> value = standaloneApps.Value;
			string[] expected = (from appId in ZApplicationDirectoryCatalog.BuiltInDirectories.Where((ZApplicationDirectoryMetadata directory) => directory.DefaultGroup).SelectMany((ZApplicationDirectoryMetadata directory) => directory.AppIds)
				where !string.Equals(appId, "one_dragon", StringComparison.Ordinal)
				select appId).ToArray();
			Assert.Equal(expected, value.Select((ZzzAppDto app) => app.AppId));
			IReadOnlyList<ZzzAppDto> source = ZzzStandaloneAppRunPage.OrderRequestedApps(value, new string[3] { "world_patrol", "city_fund", "charge_plan" });
			Assert.Equal(new string[3] { "charge_plan", "city_fund", "world_patrol" }, source.Select((ZzzAppDto app) => app.AppId));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 刷新和选择应保留未注册 app_id，列表变更应按 BaselineParity 用可见 app_ids 覆盖真实 YAML。
	/// </summary>
	[Fact]
	public void ProductionBackendPersistsPythonStandaloneMutationSemantics()
	{
		string text = CreateRunRoot();
		string text2 = Path.Combine(text, "config", "00");
		string path = Path.Combine(text2, "standalone_app.yml");
		Directory.CreateDirectory(text2);
		File.WriteAllText(path, "app_list:\n- removed_before\n- coffee\n- removed_middle\n- charge_plan\nactive_app_id: removed_before");
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendSession.Backend.GetConfigScope("standalone-app", 0);
			Assert.True(configScope.Success, configScope.Error);
			List<string> actual = Assert.IsType<List<string>>(configScope.Value.Values["app_list"]);
			int num = 4;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "removed_before";
			span[1] = "coffee";
			span[2] = "removed_middle";
			span[3] = "charge_plan";
			Assert.Equal(list, actual);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendSession.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("standalone-app", new Dictionary<string, object> { ["active_app_id"] = "coffee" }, 0));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			num = 4;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<string> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = "removed_before";
			span2[1] = "coffee";
			span2[2] = "removed_middle";
			span2[3] = "charge_plan";
			Assert.Equal(list2, Assert.IsType<List<string>>(zzzBackendResult.Value.Values["app_list"]));
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = backendSession.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("standalone-app", new Dictionary<string, object>
			{
				["app_list"] = new List<string> { "coffee", "city_fund" },
				["active_app_id"] = "coffee"
			}, 0));
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			string text3 = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
			int num2 = text3.IndexOf("- coffee", StringComparison.Ordinal);
			int num3 = text3.IndexOf("- city_fund", StringComparison.Ordinal);
			Assert.True(num2 >= 0);
			Assert.True(num3 > num2);
			Assert.DoesNotContain("removed_before", text3, StringComparison.Ordinal);
			Assert.DoesNotContain("removed_middle", text3, StringComparison.Ordinal);
			Assert.Contains("active_app_id: coffee", text3, StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-standalone-production-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "game_data", "screen_info"));
		return text;
	}
}
