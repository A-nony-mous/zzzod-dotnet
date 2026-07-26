using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 枯萎之都组合设置页、真实挑战配置和运行记录测试。
/// </summary>
public sealed class WitheredDomainAppSettingPageTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot)
		{
			_runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance);
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	/// <summary>
	/// 挑战配置服务应读取 sample 和用户文件，并执行真实校验、重命名和删除。
	/// </summary>
	[Fact]
	public void ChallengeStoreUsesRealFilesAndCatalogValidation()
	{
		string text = CreateRunRoot("zzzod-withered-store");
		try
		{
			CopyHollowCatalogs(text);
			string text2 = Path.Combine(text, "config", "hollow_zero_challenge");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "默认配置.sample.yml"), "auto_battle: 专属配队\nresonium_priority:\n- 冻结\ntarget_agents:\n- ellen\n- 击破\n- soukaku\npath_finding: 默认\nbuy_only_priority: true");
			File.WriteAllText(Path.Combine(text2, "用户配置.yml"), "auto_battle: 用户战斗\nresonium_priority: []\nevent_priority:\n- ''");
			WitheredDomainChallengeConfigStore witheredDomainChallengeConfigStore = new WitheredDomainChallengeConfigStore(new OneDragonEnvironment(text));
			IReadOnlyList<WitheredDomainChallengeConfigEntry> all = witheredDomainChallengeConfigStore.GetAll();
			Assert.Equal(2, all.Count);
			Assert.Contains((IEnumerable<WitheredDomainChallengeConfigEntry>)all, (Predicate<WitheredDomainChallengeConfigEntry>)((WitheredDomainChallengeConfigEntry item) => item.ModuleName == "用户配置"));
			Assert.Contains((IEnumerable<WitheredDomainChallengeConfigEntry>)all, (Predicate<WitheredDomainChallengeConfigEntry>)((WitheredDomainChallengeConfigEntry item) => item.ModuleName == "默认配置"));
			Assert.True(all.Single((WitheredDomainChallengeConfigEntry item) => item.ModuleName == "默认配置").IsSample);
			Assert.False(all.Single((WitheredDomainChallengeConfigEntry item) => item.ModuleName == "用户配置").IsSample);
			Assert.Contains("危机", (IEnumerable<string>)witheredDomainChallengeConfigStore.ValidateEntryText("危机").Values);
			Assert.NotEmpty(witheredDomainChallengeConfigStore.ValidateEntryText("不存在入口").Error);
			Assert.Contains("冻结", (IEnumerable<string>)witheredDomainChallengeConfigStore.ValidateResoniumText("冻结").Values);
			WitheredDomainChallengeConfig obj = new WitheredDomainChallengeConfig
			{
				AutoBattle = "真实战斗"
			};
			int num = 1;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			CollectionsMarshal.AsSpan(list)[0] = "危机";
			obj.Avoid = list;
			WitheredDomainChallengeConfigEntry witheredDomainChallengeConfigEntry = witheredDomainChallengeConfigStore.Save("用户配置", "用户配置-改名", obj);
			Assert.Equal("用户配置-改名", witheredDomainChallengeConfigEntry.ModuleName);
			Assert.False(File.Exists(Path.Combine(text2, "用户配置.yml")));
			Assert.True(File.Exists(Path.Combine(text2, "用户配置-改名.yml")));
			witheredDomainChallengeConfigStore.Delete("用户配置-改名");
			Assert.False(File.Exists(Path.Combine(text2, "用户配置-改名.yml")));
			Assert.True(File.Exists(Path.Combine(text2, "默认配置.sample.yml")));
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// 专用 AppHost 后端应返回真实目录、保存挑战配置并重置真实周记录。
	/// </summary>
	[Fact]
	public void DedicatedBackendProvidesCatalogCrudAndWeeklyReset()
	{
		string text = CreateRunRoot("zzzod-withered-backend");
		WriteInstanceConfig(text);
		CopyHollowCatalogs(text);
		CopyCompendiumCatalog(text);
		Directory.CreateDirectory(Path.Combine(text, "config", "auto_battle"));
		Directory.CreateDirectory(Path.Combine(text, "config", "hollow_zero_challenge"));
		File.WriteAllText(Path.Combine(text, "config", "hollow_zero_challenge", "默认配置.sample.yml"), "resonium_priority: []\n");
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			IZzzWitheredDomainSettingsBackend backend = backendSession.Backend;
			ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto> witheredDomainSettingsCatalog = backend.GetWitheredDomainSettingsCatalog(0);
			Assert.True(witheredDomainSettingsCatalog.Success, witheredDomainSettingsCatalog.Error);
			Assert.NotEmpty(witheredDomainSettingsCatalog.Value.Missions);
			Assert.Single(witheredDomainSettingsCatalog.Value.ChallengeConfigs);
			Assert.NotEmpty(witheredDomainSettingsCatalog.Value.AgentOptions);
			ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto> zzzBackendResult = backend.SaveWitheredDomainChallengeConfig(new ZzzSaveWitheredDomainChallengeConfigRequest(null, "自定义-01", "真实战斗配置", "冻结", string.Empty, new string[3] { "ellen", "击破", "soukaku" }, "自定义", "危机", "呼叫增援", "限时战斗", BuyOnlyPriority: true));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.Equal("真实战斗配置", zzzBackendResult.Value.AutoBattle);
			Assert.True(File.Exists(Path.Combine(text, "config", "hollow_zero_challenge", "自定义-01.yml")));
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			WitheredDomainConfig config = WitheredDomainConfig.Load(environment, 0, "default");
			WitheredDomainRunRecord witheredDomainRunRecord = WitheredDomainRunRecord.Load(environment, config, 0, 4);
			witheredDomainRunRecord.AddTimes();
			witheredDomainRunRecord.AddDailyTimes();
			ZzzBackendResult<ZzzWitheredDomainRunRecordDto> zzzBackendResult2 = backend.ResetWitheredDomainRunRecord(0);
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.Equal(0, zzzBackendResult2.Value.WeeklyRunTimes);
			Assert.Equal(0, zzzBackendResult2.Value.DailyRunTimes);
			WitheredDomainRunRecord witheredDomainRunRecord2 = WitheredDomainRunRecord.Load(environment, config, 0, 4);
			Assert.Equal(0, witheredDomainRunRecord2.WeeklyRunTimes);
			Assert.Equal(0, witheredDomainRunRecord2.DailyRunTimes);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateRunRoot(string prefix)
	{
		string text = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void CopyHollowCatalogs(string runRoot)
	{
		string text = FindWorkspaceRoot();
		string text2 = Path.Combine(runRoot, "assets", "game_data", "hollow_zero");
		Directory.CreateDirectory(text2);
		File.Copy(Path.Combine(text, "assets", "game_data", "hollow_zero", "entry_list.yml"), Path.Combine(text2, "entry_list.yml"));
		File.Copy(Path.Combine(text, "assets", "game_data", "hollow_zero", "resonium.yml"), Path.Combine(text2, "resonium.yml"));
	}

	private static void CopyCompendiumCatalog(string runRoot)
	{
		string text = FindWorkspaceRoot();
		string text2 = Path.Combine(runRoot, "assets", "game_data");
		Directory.CreateDirectory(text2);
		File.Copy(Path.Combine(text, "assets", "game_data", "compendium_data.yml"), Path.Combine(text2, "compendium_data.yml"));
	}

	private static void WriteInstanceConfig(string runRoot)
	{
		Directory.CreateDirectory(Path.Combine(runRoot, "config", "00"));
		File.WriteAllText(Path.Combine(runRoot, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 D:\\zzz-od-dotnet 工作区。");
	}
}
