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
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.Gui.PageModels.ApplicationSettings;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class LostVoidAppSettingPageTests
{
	public class RecordingMainBackendProxy : DispatchProxy
	{
		public Dictionary<string, object?> Values { get; } = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["daily_plan_times"] = 5,
			["weekly_plan_times"] = 2,
			["extra_task"] = "完成悬赏委托",
			["mission_name"] = "战线肃清",
			["challenge_config"] = "默认-成就模式"
		};

		public List<ZzzSaveConfigScopeRequest> SaveRequests { get; } = new List<ZzzSaveConfigScopeRequest>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod, "targetMethod");
			if (targetMethod.Name == "GetConfigScope")
			{
				return Snapshot("lost-void", 3, "daily");
			}
			if (targetMethod.Name == "SaveConfigScope" && args != null && args.Length == 1 && args[0] is ZzzSaveConfigScopeRequest zzzSaveConfigScopeRequest)
			{
				SaveRequests.Add(zzzSaveConfigScopeRequest);
				foreach (var (key, value) in zzzSaveConfigScopeRequest.Values)
				{
					Values[key] = value;
				}
				return Snapshot(zzzSaveConfigScopeRequest.Scope, zzzSaveConfigScopeRequest.InstanceIndex, zzzSaveConfigScopeRequest.GroupId);
			}
			throw new NotSupportedException(targetMethod.Name);
		}

		private ZzzBackendResult<ZzzConfigScopeValuesDto> Snapshot(string scope, int? instance, string? group)
		{
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(new ZzzConfigScopeDescriptorDto(scope, "迷失之地", InstanceBound: true, GroupBound: true, Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>()), instance, group, new Dictionary<string, object>(Values, StringComparer.Ordinal)));
		}
	}

	public sealed class RecordingLostVoidBackend : IZzzLostVoidSettingsBackend
	{
		private readonly ZzzLostVoidChallengeConfigDto _sample = new ZzzLostVoidChallengeConfigDto("默认-成就模式", IsSample: true, Exists: true, -1, ChooseTeamByPriority: false, ManuallyChooseAgent: false, new string[3] { "unknown", "unknown", "unknown" }, "全配队通用", ChaseNewMode: false, "鸣徽狂热战略", "第一个", StoreGold: true, StoreBlood: false, 50, ArtifactPriorityNew: false, 1, 3, Array.Empty<string>(), Array.Empty<string>(), new string[] { "入口" });

		public int? CatalogInstanceIndex { get; private set; }

		public int? ResetInstanceIndex { get; private set; }

		public List<ZzzLostVoidChallengeConfigDto> SavedConfigs { get; } = new List<ZzzLostVoidChallengeConfigDto>();

		public ZzzBackendResult<ZzzLostVoidSettingsCatalogDto> GetLostVoidSettingsCatalog(int instanceIndex)
		{
			CatalogInstanceIndex = instanceIndex;
			return ZzzBackendResult<ZzzLostVoidSettingsCatalogDto>.Ok(new ZzzLostVoidSettingsCatalogDto(new string[2] { "战线肃清", "特遣调查" }, new string[2] { "默认-成就模式", "自定义-01" }, new ZzzLostVoidRunRecordDto(instanceIndex, 2, 6, BountyCommissionComplete: false, EvalPointComplete: false, PeriodRewardComplete: false)));
		}

		public ZzzBackendResult<ZzzLostVoidRunRecordDto> ResetLostVoidRunRecord(int instanceIndex)
		{
			ResetInstanceIndex = instanceIndex;
			return ZzzBackendResult<ZzzLostVoidRunRecordDto>.Ok(new ZzzLostVoidRunRecordDto(instanceIndex, 0, 0, BountyCommissionComplete: false, EvalPointComplete: false, PeriodRewardComplete: false));
		}

		public ZzzBackendResult<ZzzLostVoidChallengeCatalogDto> GetLostVoidChallengeCatalog(int instanceIndex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeCatalogDto>.Ok(new ZzzLostVoidChallengeCatalogDto(new ZzzLostVoidChallengeSummaryDto[2]
			{
				new ZzzLostVoidChallengeSummaryDto("默认-成就模式", IsSample: true),
				new ZzzLostVoidChallengeSummaryDto("自定义-01", IsSample: false)
			}, new ZzzLostVoidTeamDto[] { new ZzzLostVoidTeamDto(0, "一队") }, new string[] { "全配队通用" }, new ZzzLostVoidOptionDto[2]
			{
				new ZzzLostVoidOptionDto("代理人", "unknown"),
				new ZzzLostVoidOptionDto("安比", "anby")
			}, new string[] { "鸣徽狂热战略" }));
		}

		public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> GetLostVoidChallengeConfig(string moduleName)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(_sample with
			{
				ModuleName = moduleName
			});
		}

		public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CreateLostVoidChallengeConfigDraft()
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(_sample with
			{
				ModuleName = "自定义-02",
				IsSample = false,
				Exists = false
			});
		}

		public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CopyLostVoidChallengeConfigDraft(string moduleName)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(_sample with
			{
				ModuleName = moduleName + "_copy",
				IsSample = false,
				Exists = false
			});
		}

		public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> SaveLostVoidChallengeConfig(ZzzSaveLostVoidChallengeConfigRequest request)
		{
			ZzzLostVoidChallengeConfigDto zzzLostVoidChallengeConfigDto = request.Config with
			{
				IsSample = false,
				Exists = true
			};
			SavedConfigs.Add(zzzLostVoidChallengeConfigDto);
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(zzzLostVoidChallengeConfigDto);
		}

		public ZzzBackendResult<bool> DeleteLostVoidChallengeConfig(string moduleName)
		{
			return ZzzBackendResult<bool>.Ok(value: true);
		}

		public ZzzBackendResult<ZzzLostVoidPriorityParseDto> ParseLostVoidPriority(ZzzLostVoidPriorityKind kind, string text)
		{
			if (kind == ZzzLostVoidPriorityKind.RegionTypePriority)
			{
				return ZzzBackendResult<ZzzLostVoidPriorityParseDto>.Ok(new ZzzLostVoidPriorityParseDto(new string[] { "入口" }, "输入非法 不存在区域"));
			}
			return ZzzBackendResult<ZzzLostVoidPriorityParseDto>.Ok(new ZzzLostVoidPriorityParseDto(text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), string.Empty));
		}
	}

	[Fact]
	public void ViewModelUsesRealCatalogRunRecordAndRequestedScope()
	{
		IZzzAppBackend zzzAppBackend = DispatchProxy.Create<IZzzAppBackend, RecordingMainBackendProxy>();
		RecordingMainBackendProxy recordingMainBackendProxy = (RecordingMainBackendProxy)zzzAppBackend;
		RecordingLostVoidBackend recordingLostVoidBackend = new RecordingLostVoidBackend();
		ZzzLostVoidAppSettingViewModel zzzLostVoidAppSettingViewModel = new ZzzLostVoidAppSettingViewModel(zzzAppBackend, recordingLostVoidBackend, 3, "daily");
		Assert.True(zzzLostVoidAppSettingViewModel.ReloadBase());
		Assert.Equal(new string[2] { "战线肃清", "特遣调查" }, zzzLostVoidAppSettingViewModel.Missions);
		Assert.Equal(new string[2] { "默认-成就模式", "自定义-01" }, zzzLostVoidAppSettingViewModel.ChallengeConfigNames);
		Assert.Equal("通关次数 本日: 2, 本周: 6", zzzLostVoidAppSettingViewModel.RunRecordText);
		Assert.True(zzzLostVoidAppSettingViewModel.SaveBase("daily_plan_times", 8));
		Assert.True(zzzLostVoidAppSettingViewModel.ResetRunRecord());
		Assert.Equal(3, recordingLostVoidBackend.CatalogInstanceIndex);
		Assert.Equal(3, recordingLostVoidBackend.ResetInstanceIndex);
		Assert.Single(recordingMainBackendProxy.SaveRequests);
		Assert.Equal("lost-void", recordingMainBackendProxy.SaveRequests[0].Scope);
		Assert.Equal(3, recordingMainBackendProxy.SaveRequests[0].InstanceIndex);
		Assert.Equal("daily", recordingMainBackendProxy.SaveRequests[0].GroupId);
		Assert.Equal(8, recordingMainBackendProxy.Values["daily_plan_times"]);
	}

	[Fact]
	public void ViewModelEditsRealChallengeDraftWithoutInjectingCatalogEntries()
	{
		IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, RecordingMainBackendProxy>();
		RecordingLostVoidBackend recordingLostVoidBackend = new RecordingLostVoidBackend();
		ZzzLostVoidAppSettingViewModel zzzLostVoidAppSettingViewModel = new ZzzLostVoidAppSettingViewModel(backend, recordingLostVoidBackend, 0, "default");
		Assert.True(zzzLostVoidAppSettingViewModel.ReloadChallengeCatalog());
		Assert.True(zzzLostVoidAppSettingViewModel.ChooseConfig("默认-成就模式"));
		Assert.True(zzzLostVoidAppSettingViewModel.ChosenConfig.IsSample);
		Assert.False(zzzLostVoidAppSettingViewModel.UpdateConfig((ZzzLostVoidChallengeConfigDto config) => config with
		{
			StoreGold = false
		}));
		Assert.True(zzzLostVoidAppSettingViewModel.CopyConfig());
		Assert.False(zzzLostVoidAppSettingViewModel.ChosenConfig.IsSample);
		Assert.Equal("默认-成就模式_copy", zzzLostVoidAppSettingViewModel.ChosenConfig.ModuleName);
		Assert.True(zzzLostVoidAppSettingViewModel.UpdateConfig((ZzzLostVoidChallengeConfigDto config) => config with
		{
			StoreGold = false
		}));
		Assert.True(zzzLostVoidAppSettingViewModel.UpdatePriority(ZzzLostVoidPriorityKind.RegionTypePriority, "入口\n不存在区域"));
		Assert.True(recordingLostVoidBackend.SavedConfigs.Count >= 2);
		Assert.False(zzzLostVoidAppSettingViewModel.ChosenConfig.StoreGold);
		Assert.Equal(new string[] { "入口" }, zzzLostVoidAppSettingViewModel.ChosenConfig.RegionTypePriority);
		Assert.Equal("输入非法 不存在区域", zzzLostVoidAppSettingViewModel.Error);
	}

	[Fact]
	public void ProductionBackendPersistsRenamesAndDeletesRealChallengeFiles()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-lost-void-settings", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "lost_void_challenge"));
		File.WriteAllText(Path.Combine(text, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
		File.WriteAllText(Path.Combine(text, "config", "lost_void_challenge", "默认.sample.yml"), "store_gold: true\nperiod_buff_no: 第二个\nfuture_nested:\n  child: keep-me\n");
		ZzzRuntimeManager zzzRuntimeManager = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
		ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
		ZzzBattleAssistantRuntimeSource zzzBattleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
		ZzzLogFanOutLoggerProvider zzzLogFanOutLoggerProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(text), eventBus);
		try
		{
			ZzzAppBackend zzzAppBackend = new ZzzAppBackend(zzzRuntimeManager, eventBus, zzzBattleAssistantRuntimeSource, zzzLogFanOutLoggerProvider, new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
			IZzzLostVoidSettingsBackend zzzLostVoidSettingsBackend = zzzAppBackend;
			ZzzLostVoidChallengeConfigDto value = zzzLostVoidSettingsBackend.GetLostVoidChallengeConfig("默认").Value;
			Assert.True(value.IsSample);
			ZzzLostVoidChallengeConfigDto value2 = zzzLostVoidSettingsBackend.CopyLostVoidChallengeConfigDraft("默认").Value;
			Assert.False(value2.Exists);
			Assert.Equal("默认_copy", value2.ModuleName);
			ZzzBackendResult<ZzzLostVoidChallengeConfigDto> zzzBackendResult = zzzLostVoidSettingsBackend.SaveLostVoidChallengeConfig(new ZzzSaveLostVoidChallengeConfigRequest(null, value2 with
			{
				StoreGold = false
			}));
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.True(File.Exists(Path.Combine(text, "config", "lost_void_challenge", "默认_copy.yml")));
			Assert.False(LostVoidChallengeConfig.Load(new OneDragonEnvironment(text), "默认_copy").StoreGold);
			Assert.Contains("child: keep-me", File.ReadAllText(Path.Combine(text, "config", "lost_void_challenge", "默认_copy.yml")), StringComparison.Ordinal);
			ZzzBackendResult<ZzzLostVoidChallengeConfigDto> zzzBackendResult2 = zzzLostVoidSettingsBackend.SaveLostVoidChallengeConfig(new ZzzSaveLostVoidChallengeConfigRequest("默认_copy", zzzBackendResult.Value with
			{
				ModuleName = "自定义-01"
			}));
			Assert.True(zzzBackendResult2.Success, zzzBackendResult2.Error);
			Assert.False(File.Exists(Path.Combine(text, "config", "lost_void_challenge", "默认_copy.yml")));
			Assert.True(File.Exists(Path.Combine(text, "config", "lost_void_challenge", "自定义-01.yml")));
			Assert.Contains("child: keep-me", File.ReadAllText(Path.Combine(text, "config", "lost_void_challenge", "自定义-01.yml")), StringComparison.Ordinal);
			Assert.True(zzzLostVoidSettingsBackend.DeleteLostVoidChallengeConfig("自定义-01").Success);
			Assert.False(File.Exists(Path.Combine(text, "config", "lost_void_challenge", "自定义-01.yml")));
			Assert.False(zzzLostVoidSettingsBackend.DeleteLostVoidChallengeConfig("默认").Success);
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

}
