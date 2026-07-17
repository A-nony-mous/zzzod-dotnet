using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class IntelBoardSettingsFlyoutTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> ConfigValues { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["predefined_team_idx"] = -1,
			["auto_battle_config"] = "全配队通用",
			["exp_grind_mode"] = false
		};

		public List<(string Scope, int? InstanceIndex, string? GroupId)> Reads { get; } = new List<(string, int?, string)>();

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		public int CatalogReads { get; private set; }

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "GetConfigScope" && args != null && args.Length == 3 && args[0] is string text)
			{
				int? num = args[1] as int?;
				string text2 = args[2] as string;
				Reads.Add((text, num, text2));
				return (text == "team") ? TeamSnapshot(num) : ConfigSnapshot(num, text2);
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (var (key, value) in zzzSaveConfigScopeRequest.Values)
				{
					ConfigValues[key] = value;
				}
				return ConfigSnapshot(zzzSaveConfigScopeRequest.InstanceIndex, zzzSaveConfigScopeRequest.GroupId);
			}
			if (targetMethod.Name == "GetBattleAssistantConfigCatalog")
			{
				CatalogReads++;
				return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(new ZzzBattleAssistantConfigCatalogDto(new string[2] { "全配队通用", "安比模板" }, Array.Empty<string>()));
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> ConfigSnapshot(int? instanceIndex, string? groupId)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto("intel-board", "情报板", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, groupId, new Dictionary<string, object>(ConfigValues, StringComparer.Ordinal)));
		}

		private static ZzzBackendResult<ZzzConfigScopeValuesDto> TeamSnapshot(int? instanceIndex)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto("team", "预备编队", InstanceBound: true, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, null, new Dictionary<string, object> { ["team_list"] = new List<PredefinedTeamInfo>
			{
				new PredefinedTeamInfo(0, "编队一", "全配队通用", new List<string>()),
				new PredefinedTeamInfo(1, "编队二", "安比模板", new List<string>())
			} }));
		}
	}

	public class RecordingProgressBackendProxy : DispatchProxy
	{
		public List<int?> ResetInstances { get; } = new List<int?>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "ResetIntelBoardProgress")
			{
				int? item = ((args != null && args.Length == 1 && args[0] is int value) ? new int?(value) : ((int?)null));
				ResetInstances.Add(item);
				return ZzzBackendResult<bool>.Ok(value: true);
			}
			throw new NotSupportedException(targetMethod.Name);
		}
	}

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
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	[Fact]
	public void FlyoutUsesAxamlFluentControlsAndPythonOrder()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzIntelBoardSettingsFlyoutContent.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzIntelBoardSettingsFlyoutContent.axaml.cs"));
		AssertOrder(text, "预备编队", "自动战斗", "刷满经验", "重置进度");
		Assert.Contains("fa:SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(\"team\", _instanceIndex)", actualString, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantConfigCatalog", actualString, StringComparison.Ordinal);
		Assert.Contains("ResetIntelBoardProgress(_instanceIndex)", actualString, StringComparison.Ordinal);
		Assert.Contains("value == -1", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	[Fact]
	public void FlyoutReadsRealCatalogsWritesRequestedScopeAndResetsCurrentInstance()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		IZzzIntelBoardProgressBackend progressBackend = DispatchProxy.Create<IZzzIntelBoardProgressBackend, RecordingProgressBackendProxy>();
		RecordingProgressBackendProxy recordingProgressBackendProxy = (RecordingProgressBackendProxy)progressBackend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzIntelBoardSettingsFlyoutContent zzzIntelBoardSettingsFlyoutContent = new ZzzIntelBoardSettingsFlyoutContent(backend, progressBackend, 5, "daily");
			Assert.True(zzzIntelBoardSettingsFlyoutContent.AutoBattleVisible);
			zzzIntelBoardSettingsFlyoutContent.SaveForTest("exp_grind_mode", true);
			zzzIntelBoardSettingsFlyoutContent.ResetProgressForTest();
			Assert.Equal("已重置", zzzIntelBoardSettingsFlyoutContent.ResetButtonText);
			Assert.False(zzzIntelBoardSettingsFlyoutContent.ResetButtonEnabled);
		});
		Assert.Contains<(string, int?, string)>(recordingBackendProxy.Reads, delegate((string Scope, int? InstanceIndex, string GroupId) read)
		{
			(string, int?, string) tuple = read;
			return tuple.Item1 == "intel-board" && tuple.Item2 == 5 && tuple.Item3 == "daily";
		});
		Assert.Contains<(string, int?, string)>(recordingBackendProxy.Reads, delegate((string Scope, int? InstanceIndex, string GroupId) read)
		{
			(string, int?, string) tuple = read;
			return tuple.Item1 == "team" && tuple.Item2 == 5 && tuple.Item3 == null;
		});
		Assert.Equal(1, recordingBackendProxy.CatalogReads);
		Assert.Equal(5, Assert.Single(recordingProgressBackendProxy.ResetInstances));
		ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest = Assert.Single(recordingBackendProxy.Requests);
		Assert.Equal("intel-board", zzzSaveConfigScopeRequest.Scope);
		Assert.Equal(5, zzzSaveConfigScopeRequest.InstanceIndex);
		Assert.Equal("daily", zzzSaveConfigScopeRequest.GroupId);
		Assert.Equal(true, recordingBackendProxy.ConfigValues["exp_grind_mode"]);
	}

	[Fact]
	public void BackendResetPersistsAllPythonProgressFieldsToRealRunRecord()
	{
		string text = CreateTempRunRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "03", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "intel_board.yml"), "dt: \"20260706\"\nprogress_complete: true\nnotorious_hunt_count: 4\nexpert_challenge_count: 7\nbase_exp: 1250");
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<bool> zzzBackendResult = backendSession.Backend.ResetIntelBoardProgress(3);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.True(zzzBackendResult.Value);
			IntelBoardConfig config = IntelBoardConfig.Load(new OneDragonEnvironment(text), 3, "one_dragon");
			IntelBoardRunRecord intelBoardRunRecord = IntelBoardRunRecord.Load(new OneDragonEnvironment(text), 3, config);
			Assert.False(intelBoardRunRecord.ProgressComplete);
			Assert.Equal(0, intelBoardRunRecord.NotoriousHuntCount);
			Assert.Equal(0, intelBoardRunRecord.ExpertChallengeCount);
			Assert.Equal(0, intelBoardRunRecord.BaseExp);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static void AssertOrder(string text, params string[] markers)
	{
		int num = -1;
		foreach (string text2 in markers)
		{
			int num2 = text.IndexOf(text2, StringComparison.Ordinal);
			Assert.True(num2 > num, "未按顺序找到 " + text2 + "。");
			num = num2;
		}
	}

	private static string FindDirectory()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string[] buffer = new string[5];
			buffer[0] = directoryInfo.FullName;
			buffer[1] = "src";
			buffer[2] = "ZzzOd.Gui";
			buffer[3] = "Pages";
			buffer[4] = "ApplicationSettings";
			string text = Path.Combine(buffer);
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到应用设置目录。");
	}

	private static string CreateTempRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-intel-board-settings", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}
