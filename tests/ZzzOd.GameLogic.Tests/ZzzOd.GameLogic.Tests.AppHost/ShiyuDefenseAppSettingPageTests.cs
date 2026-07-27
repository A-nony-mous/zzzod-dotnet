using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 式舆防卫战设置页、真实配置和运行记录重置测试。
/// </summary>
[Trait("Category", "GuiHeavy")]
public sealed class ShiyuDefenseAppSettingPageTests
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
	/// 队伍选项应写入当前实例和应用组的真实配置路径。
	/// </summary>
	[Fact]
	public void ScopePersistsCriticalAndWeaknessSelections()
	{
		string text = CreateRunRoot("zzzod-shiyu-scope");
		try
		{
			ZzzConfigScopeService zzzConfigScopeService = new ZzzConfigScopeService(text);
			int num = 1;
			List<ShiyuDefenseTeamConfig> list = new List<ShiyuDefenseTeamConfig>(num);
			CollectionsMarshal.SetCount(list, num);
			ref ShiyuDefenseTeamConfig reference = ref CollectionsMarshal.AsSpan(list)[0];
			ShiyuDefenseTeamConfig obj = new ShiyuDefenseTeamConfig
			{
				TeamIndex = 2,
				ForCritical = true
			};
			int num2 = 2;
			List<DmgTypeEnum> list2 = new List<DmgTypeEnum>(num2);
			CollectionsMarshal.SetCount(list2, num2);
			Span<DmgTypeEnum> span = CollectionsMarshal.AsSpan(list2);
			span[0] = DmgTypeEnum.ELECTRIC;
			span[1] = DmgTypeEnum.ICE;
			obj.WeaknessList = list2;
			reference = obj;
			List<ShiyuDefenseTeamConfig> value = list;
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = zzzConfigScopeService.Save(new ZzzSaveConfigScopeRequest("shiyu-defense", new Dictionary<string, object> { ["team_list"] = value }, 4, "critical"));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			string[] buffer = new string[5];
			buffer[0] = text;
			buffer[1] = "config";
			buffer[2] = "04";
			buffer[3] = "critical";
			buffer[4] = "shiyu_defense.yml";
			string path = Path.Combine(buffer);
			Assert.True(File.Exists(path));
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = zzzConfigScopeService.Read("shiyu-defense", 4, "critical");
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			ShiyuDefenseTeamConfig shiyuDefenseTeamConfig = Assert.Single(Assert.IsType<List<ShiyuDefenseTeamConfig>>(zzzBackendResult2.Value.Values["team_list"]));
			Assert.True(shiyuDefenseTeamConfig.ForCritical);
			num = 2;
			List<string> list3 = new List<string>(num);
			CollectionsMarshal.SetCount(list3, num);
			Span<string> span2 = CollectionsMarshal.AsSpan(list3);
			span2[0] = "ELECTRIC";
			span2[1] = "ICE";
			Assert.Equal<List<string>>(list3, shiyuDefenseTeamConfig.WeaknessListRaw);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	/// <summary>
	/// AppHost 命令应重置真实 ShiyuDefenseRunRecord 文件。
	/// </summary>
	[Fact]
	public void BackendResetsPersistedCriticalHistory()
	{
		string text = CreateRunRoot("zzzod-shiyu-record");
		WriteInstanceConfig(text);
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(text);
			ShiyuDefenseConfig config = ShiyuDefenseConfig.Load(environment, 0, "one_dragon");
			ShiyuDefenseRunRecord shiyuDefenseRunRecord = ShiyuDefenseRunRecord.Load(environment, 0, config, 4);
			shiyuDefenseRunRecord.AddNodeFinished(1);
			shiyuDefenseRunRecord.AddNodeFinished(3);
			int num = 2;
			List<int> list = new List<int>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<int> span = CollectionsMarshal.AsSpan(list);
			span[0] = 1;
			span[1] = 3;
			Assert.Equal(list, shiyuDefenseRunRecord.CriticalHistory);
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> zzzBackendResult = backendSession.Backend.ResetShiyuDefenseRunRecord(0);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.Empty(zzzBackendResult.Value.CriticalHistory);
			ShiyuDefenseRunRecord shiyuDefenseRunRecord2 = ShiyuDefenseRunRecord.Load(environment, 0, config, 4);
			Assert.Empty(shiyuDefenseRunRecord2.CriticalHistory);
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

	private static void WriteInstanceConfig(string runRoot)
	{
		Directory.CreateDirectory(Path.Combine(runRoot, "config", "00"));
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(runRoot, "assets", "game_data", "screen_info"));
		File.WriteAllText(Path.Combine(runRoot, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
	}

}
