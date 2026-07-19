using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui.Pages.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class LifeOnLineSettingsFlyoutTests
{
	public class RecordingBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> LifeValues { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["daily_plan_times"] = 12,
			["predefined_team_idx"] = -1
		};

		public List<ZzzSaveConfigScopeRequest> Requests { get; } = new List<ZzzSaveConfigScopeRequest>();

		public int? RunRecordInstanceIndex { get; private set; }

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			object value;
			if (targetMethod.Name == "GetConfigScope" && args != null && args.Length == 3 && args[0] is string text)
			{
				value = args[1];
				if (value is int instanceIndex)
				{
					object obj = args[2];
					if (true)
					{
						string groupId = obj as string;
						return (text == "life-on-line") ? LifeSnapshot(text, instanceIndex, groupId) : TeamSnapshot(instanceIndex);
					}
				}
			}
			if (targetMethod.Name == "GetLifeOnLineRunRecord" && args != null && args.Length == 1)
			{
				value = args[0];
				if (value is int num)
				{
					RunRecordInstanceIndex = num;
					return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Ok(new ZzzLifeOnLineRunRecordDto(num, 7));
				}
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				Requests.Add(zzzSaveConfigScopeRequest);
				foreach (KeyValuePair<string, object> value3 in zzzSaveConfigScopeRequest.Values)
				{
					value3.Deconstruct(out var key, out value);
					string key2 = key;
					object value2 = value;
					LifeValues[key2] = value2;
				}
				return LifeSnapshot(zzzSaveConfigScopeRequest.Scope, zzzSaveConfigScopeRequest.InstanceIndex.Value, zzzSaveConfigScopeRequest.GroupId);
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> LifeSnapshot(string scope, int instanceIndex, string groupId)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto(scope, "生命热线", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, groupId, new Dictionary<string, object>(LifeValues, StringComparer.Ordinal)));
		}

		private static ZzzBackendResult<ZzzConfigScopeValuesDto> TeamSnapshot(int instanceIndex)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto("team", "预备编队", InstanceBound: true, GroupBound: false, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instanceIndex, null, new Dictionary<string, object> { ["team_list"] = new List<PredefinedTeamInfo>
			{
				new PredefinedTeamInfo(0, "一队", "", new List<string>()),
				new PredefinedTeamInfo(1, "二队", "", new List<string>())
			} }));
		}
	}

	[Fact]
	public void FlyoutUsesAxamlFluentControlsAndPythonTexts()
	{
		string path = FindDirectory();
		string text = File.ReadAllText(Path.Combine(path, "ZzzLifeOnLineSettingsFlyoutContent.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "ZzzLifeOnLineSettingsFlyoutContent.axaml.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "ZzzLifeOnLineSettingsFlyoutViewModel.cs"));
		Assert.True(text.IndexOf("每日次数", StringComparison.Ordinal) < text.IndexOf("完成次数", StringComparison.Ordinal));
		Assert.True(text.IndexOf("完成次数", StringComparison.Ordinal) < text.IndexOf("预备编队", StringComparison.Ordinal));
		Assert.Contains("Minimum=\"0\"", text, StringComparison.Ordinal);
		Assert.Contains("Maximum=\"20000\"", text, StringComparison.Ordinal);
		Assert.Contains("fa:FANumberBox", text, StringComparison.Ordinal);
		Assert.Contains("fa:FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("当日: {_viewModel.DailyRunTimes}", actualString, StringComparison.Ordinal);
		Assert.Contains("List<PredefinedTeamInfo>", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Python", text, StringComparison.Ordinal);
	}

	[Fact]
	public void FlyoutReadsRealDailyRunTimesAndTeamCatalogAndWritesRequestedScope()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingBackendProxy>();
		RecordingBackendProxy recordingBackendProxy = (RecordingBackendProxy)backend;
		GuiParityAndFacadeTests.RunOnUiThread(delegate
		{
			ZzzLifeOnLineSettingsFlyoutContent zzzLifeOnLineSettingsFlyoutContent = new ZzzLifeOnLineSettingsFlyoutContent(backend, 3, "daily");
			Assert.Equal(12, zzzLifeOnLineSettingsFlyoutContent.ViewModel.DailyPlanTimes);
			Assert.Equal(7, zzzLifeOnLineSettingsFlyoutContent.ViewModel.DailyRunTimes);
			string[] buffer = new string[3];
			buffer[0] = "游戏内配队";
			buffer[1] = "一队";
			buffer[2] = "二队";
			Assert.Equal(buffer, zzzLifeOnLineSettingsFlyoutContent.ViewModel.TeamOptions.Select((ZzzLifeOnLineTeamOption option) => option.Label).ToArray());
			zzzLifeOnLineSettingsFlyoutContent.SaveForTest("daily_plan_times", 18);
			zzzLifeOnLineSettingsFlyoutContent.SaveForTest("predefined_team_idx", 1);
		});
		Assert.Equal(3, recordingBackendProxy.RunRecordInstanceIndex);
		Assert.All(recordingBackendProxy.Requests, delegate(ZzzSaveConfigScopeRequest request)
		{
			Assert.Equal("life-on-line", request.Scope);
			Assert.Equal(3, request.InstanceIndex);
			Assert.Equal("daily", request.GroupId);
		});
		Assert.Equal(18, recordingBackendProxy.LifeValues["daily_plan_times"]);
		Assert.Equal(1, recordingBackendProxy.LifeValues["predefined_team_idx"]);
	}

	[Fact]
	public void BackendReadsDailyRunTimesFromRealRunRecordFile()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-life-on-line-settings", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00", "app_run_record"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "game_data", "screen_info"));
		File.WriteAllText(Path.Combine(text, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "config";
		buffer[2] = "00";
		buffer[3] = "app_run_record";
		buffer[4] = "life_on_line.yml";
		File.WriteAllText(Path.Combine(buffer), "daily_run_times: 9\n");
		ZzzRuntimeManager zzzRuntimeManager = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
		ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
		ZzzBattleAssistantRuntimeSource zzzBattleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
		ZzzLogFanOutLoggerProvider zzzLogFanOutLoggerProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(text), eventBus);
		try
		{
			ZzzAppBackend zzzAppBackend = new ZzzAppBackend(zzzRuntimeManager, eventBus, zzzBattleAssistantRuntimeSource, zzzLogFanOutLoggerProvider, new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
			ZzzBackendResult<ZzzLifeOnLineRunRecordDto> lifeOnLineRunRecord = zzzAppBackend.GetLifeOnLineRunRecord(0);
			Assert.True(lifeOnLineRunRecord.Success, lifeOnLineRunRecord.Error);
			Assert.NotNull(lifeOnLineRunRecord.Value);
			Assert.Equal(0, lifeOnLineRunRecord.Value.InstanceIndex);
			Assert.Equal(9, lifeOnLineRunRecord.Value.DailyRunTimes);
		}
		finally
		{
			zzzRuntimeManager.Dispose();
			zzzBattleAssistantRuntimeSource.Dispose();
			zzzLogFanOutLoggerProvider.Dispose();
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
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
}
