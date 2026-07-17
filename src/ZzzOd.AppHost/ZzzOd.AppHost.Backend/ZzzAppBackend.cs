using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Operation;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using ZzzOd.GameLogic.Application;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Application.Devtools.LargeMapRecorder;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Backend;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 业务门面实现。
/// </summary>
public sealed class ZzzAppBackend : IZzzAppBackend, IZzzIntelBoardProgressBackend, IZzzRedemptionCodeBackend, IZzzWitheredDomainSettingsBackend, IZzzLostVoidSettingsBackend, IZzzWorldPatrolSettingsBackend
{
	private sealed class WorldPatrolLargeMapRecorderSession : IDisposable
	{
		public int InstanceIndex { get; }

		public WorldPatrolArea Area { get; }

		public bool HasPersistedMap { get; set; }

		public LargeMapSnapshot? LastLargeMap { get; set; }

		public LargeMapSnapshot? LargeMap { get; set; }

		public Mat? MiniMap1Rgb { get; set; }

		public Mat? MiniMap2Rgb { get; set; }

		public MiniMapSnapshot? MiniMap { get; set; }

		public OneDragon.Core.Abstractions.Geometry.Point? LastPosition { get; set; }

		public OneDragon.Core.Abstractions.Geometry.Point? CurrentPosition { get; set; }

		public MatchResult? PositionMatch { get; set; }

		public int OverlapMode { get; set; } = 1;

		public int HighlightedIconIndex { get; set; } = -1;

		public string Status { get; set; } = string.Empty;

		public WorldPatrolLargeMapRecorderSession(int instanceIndex, WorldPatrolArea area)
		{
			InstanceIndex = instanceIndex;
			Area = area;
		}

		public void ReplaceMiniMaps(Mat miniMap1Rgb, Mat miniMap2Rgb, MiniMapSnapshot miniMap)
		{
			MiniMap1Rgb?.Dispose();
			MiniMap2Rgb?.Dispose();
			MiniMap?.Dispose();
			MiniMap1Rgb = miniMap1Rgb;
			MiniMap2Rgb = miniMap2Rgb;
			MiniMap = miniMap;
		}

		public void ClearMapState()
		{
			LastLargeMap?.Dispose();
			LargeMap?.Dispose();
			MiniMap1Rgb?.Dispose();
			MiniMap2Rgb?.Dispose();
			MiniMap?.Dispose();
			LastLargeMap = null;
			LargeMap = null;
			MiniMap1Rgb = null;
			MiniMap2Rgb = null;
			MiniMap = null;
			LastPosition = null;
			CurrentPosition = null;
			PositionMatch = null;
			OverlapMode = 1;
			HighlightedIconIndex = -1;
		}

		public void Dispose()
		{
			ClearMapState();
		}
	}

	private const string DefaultGroupId = "default";

	private readonly Lock _lock = new Lock();

	private readonly ZzzRuntimeManager _runtime;

	private readonly ZzzBackendEventBus _eventBus;

	private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

	private readonly ZzzLogFanOutLoggerProvider _logProvider;

	private readonly ZzzConfigScopeService _configScopes;

	private readonly ZzzHostMode _mode;

	private readonly ZzzApiOptions _apiOptions;

	private readonly ILogger<ZzzAppBackend> _logger;

	private Task<OperationResult>? _currentTask;

	private string? _currentAppId;

	private string? _currentAppName;

	private int? _currentInstanceIndex;

	private string? _currentGroupId;

	private DateTimeOffset? _startedAt;

	private DateTimeOffset? _finishedAt;

	private ZzzRunState _terminalState = ZzzRunState.Idle;

	private string? _lastStatus;

	private string? _lastError;

	private ZzzWindowStatusDto? _lastWindowStatus;

	private readonly SemaphoreSlim _worldPatrolLargeMapRecorderGate = new SemaphoreSlim(1, 1);

	private WorldPatrolLargeMapRecorderSession? _worldPatrolLargeMapRecorderSession;

	/// <summary>
	/// 初始化业务门面。
	/// </summary>
	/// <param name="runtime">运行时管理器。</param>
	/// <param name="eventBus">事件总线。</param>
	/// <param name="battleAssistantRuntimeSource">战斗助手进程内事件源。</param>
	/// <param name="logProvider">日志广播 provider。</param>
	/// <param name="mode">宿主模式。</param>
	/// <param name="apiOptions">API 配置。</param>
	/// <param name="logger">日志。</param>
	public ZzzAppBackend(ZzzRuntimeManager runtime, ZzzBackendEventBus eventBus, ZzzBattleAssistantRuntimeSource battleAssistantRuntimeSource, ZzzLogFanOutLoggerProvider logProvider, ZzzHostModeOptions mode, ZzzApiOptions apiOptions, ILogger<ZzzAppBackend> logger)
	{
		_runtime = runtime;
		_eventBus = eventBus;
		_battleAssistantRuntimeSource = battleAssistantRuntimeSource;
		_logProvider = logProvider;
		_configScopes = new ZzzConfigScopeService(runtime.RunRoot);
		_mode = mode.Mode;
		_apiOptions = apiOptions;
		_logger = logger;
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzHealthDto> GetHealth()
	{
		ZContext zContext = _runtime.TryGetContext();
		string version = typeof(ZzzAppBackend).Assembly.GetName().Version?.ToString() ?? "0.0.0";
		return ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(_mode, version, _runtime.RunRoot, _apiOptions.Enabled, zContext?.ReadyForApplication ?? false, _runtime.ActiveInstanceIndex));
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> GetInstances()
	{
		return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(_runtime.ListInstances());
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzInstanceDto> GetCurrentInstance()
	{
		ZzzInstanceDto zzzInstanceDto = _runtime.ListInstances().FirstOrDefault((ZzzInstanceDto instance) => instance.Active);
		return ((object)zzzInstanceDto == null) ? ZzzBackendResult<ZzzInstanceDto>.Fail(ZzzBackendErrorCode.NotReady, "当前实例不可用。") : ZzzBackendResult<ZzzInstanceDto>.Ok(zzzInstanceDto);
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int instanceIndex)
	{
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult = _runtime.ActivateInstance(instanceIndex);
		if (zzzBackendResult.Success)
		{
			ZzzInstanceDto zzzInstanceDto = zzzBackendResult.Value?.FirstOrDefault((ZzzInstanceDto instance) => instance.Active);
			if ((object)zzzInstanceDto != null)
			{
				_eventBus.Publish("instance.activeChanged", zzzInstanceDto);
			}
			_eventBus.Publish("instance.changed", zzzBackendResult.Value);
		}
		return zzzBackendResult;
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> CreateInstance()
	{
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = _runtime.CreateInstance();
		PublishInstanceChanged(result);
		return result;
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request)
	{
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = _runtime.UpdateInstance(request);
		PublishInstanceChanged(result);
		return result;
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int instanceIndex)
	{
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result = _runtime.DeleteInstance(instanceIndex);
		PublishInstanceChanged(result);
		return result;
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzRunStatusDto> LoginInstance(int instanceIndex)
	{
		if (_runtime.IsRunActive)
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能修改实例。");
		}
		if (_runtime.ListInstances().All((ZzzInstanceDto instance) => instance.Index != instanceIndex))
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {instanceIndex:00}");
		}
		return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "当前未配置登录操作。");
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetApps()
	{
		try
		{
			ZContext context = _runtime.EnsureContext();
			IReadOnlySet<string> defaultApps = context.RunContext.DefaultGroupApps.ToHashSet<string>(StringComparer.Ordinal);
			ZzzAppDto[] value = context.ApplicationFactoryRegistry.RegisteredAppIds.Select(delegate(string appId)
			{
				IApplicationFactory applicationFactory = context.ApplicationFactoryRegistry.CreateFactory(appId);
				IReadOnlyList<string> scopesForApp = _configScopes.GetScopesForApp(appId);
				return new ZzzAppDto(appId, applicationFactory.AppName, defaultApps.Contains(appId), applicationFactory.NeedNotify, RunAvailable: true, SupportsGroup: true, scopesForApp);
			}).OrderBy<ZzzAppDto, string>((ZzzAppDto app) => app.AppId, StringComparer.Ordinal).ToArray();
			return ZzzBackendResult<IReadOnlyList<ZzzAppDto>>.Ok(value);
		}
		catch (Exception exception)
		{
			return FailApps(exception);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetStandaloneApps()
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			List<ZzzAppDto> list = new List<ZzzAppDto>();
			foreach (string defaultGroupApp in zContext.RunContext.DefaultGroupApps)
			{
				if (!string.Equals(defaultGroupApp, "one_dragon", StringComparison.Ordinal))
				{
					IApplicationFactory applicationFactory = zContext.ApplicationFactoryRegistry.CreateFactory(defaultGroupApp);
					list.Add(new ZzzAppDto(defaultGroupApp, applicationFactory.AppName, DefaultGroup: true, applicationFactory.NeedNotify, RunAvailable: true, SupportsGroup: true, _configScopes.GetScopesForApp(defaultGroupApp)));
				}
			}
			return ZzzBackendResult<IReadOnlyList<ZzzAppDto>>.Ok(list);
		}
		catch (Exception exception)
		{
			return FailApps(exception);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> GetOneDragonApps(int? instanceIndex = null)
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			int value = instanceIndex ?? _runtime.ActiveInstanceIndex;
			IReadOnlyList<string> registeredAppIds = zContext.RunContext.DefaultGroupApps.Where((string appId) => !string.Equals(appId, "one_dragon", StringComparison.Ordinal)).ToArray();
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = _configScopes.Read("one-dragon-group", value, "default");
			if (!zzzBackendResult.Success || (object)zzzBackendResult.Value == null)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(zzzBackendResult.ErrorCode ?? ZzzBackendErrorCode.NotReady, zzzBackendResult.Error ?? "一条龙应用组不可用。");
			}
			object value2;
			List<OneDragonApplicationConfigItem> savedApps = ((zzzBackendResult.Value.Values.TryGetValue("app_list", out value2) && value2 is List<OneDragonApplicationConfigItem> source) ? source.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList() : new List<OneDragonApplicationConfigItem>());
			ZzzOneDragonAppMergeResult zzzOneDragonAppMergeResult = ZzzOneDragonAppListMerger.Merge(savedApps, registeredAppIds);
			if (zzzOneDragonAppMergeResult.Changed)
			{
				ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = _configScopes.Save(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = zzzOneDragonAppMergeResult.AllApps }, value, "default"));
				if (!zzzBackendResult2.Success)
				{
					return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(zzzBackendResult2.ErrorCode ?? ZzzBackendErrorCode.NotReady, zzzBackendResult2.Error ?? "一条龙应用组保存失败。");
				}
			}
			List<ZzzOneDragonAppDto> list = new List<ZzzOneDragonAppDto>();
			foreach (OneDragonApplicationConfigItem visibleApp in zzzOneDragonAppMergeResult.VisibleApps)
			{
				IApplicationFactory applicationFactory = zContext.ApplicationFactoryRegistry.CreateFactory(visibleApp.AppId);
				ZApplicationRunRecord zApplicationRunRecord = zContext.RunContext.GetRunRecord(visibleApp.AppId, value) as ZApplicationRunRecord;
				list.Add(new ZzzOneDragonAppDto(visibleApp.AppId, applicationFactory.AppName, visibleApp.Enabled, applicationFactory.NeedNotify, zContext.RunContext.NotifyAppMap.ContainsKey(visibleApp.AppId), ZzzAppSettingProviderRegistry.TryGetImplemented(visibleApp.AppId, out ZzzAppSettingProviderDescriptor _), RunAvailable: true, zApplicationRunRecord?.RunTime, zApplicationRunRecord?.RunStatusUnderNow));
			}
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(list);
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> SaveOneDragonApps(ZzzSaveOneDragonAppsRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			int value = request.InstanceIndex ?? _runtime.ActiveInstanceIndex;
			IReadOnlyList<string> readOnlyList = zContext.RunContext.DefaultGroupApps.Where((string appId) => !string.Equals(appId, "one_dragon", StringComparison.Ordinal)).ToArray();
			HashSet<string> registeredAppIds = readOnlyList.ToHashSet<string>(StringComparer.Ordinal);
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = _configScopes.Read("one-dragon-group", value, "default");
			if (!zzzBackendResult.Success || (object)zzzBackendResult.Value == null)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(zzzBackendResult.ErrorCode ?? ZzzBackendErrorCode.NotReady, zzzBackendResult.Error ?? "一条龙应用组不可用。");
			}
			object value2;
			List<OneDragonApplicationConfigItem> savedApps = ((zzzBackendResult.Value.Values.TryGetValue("app_list", out value2) && value2 is List<OneDragonApplicationConfigItem> source) ? source.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList() : new List<OneDragonApplicationConfigItem>());
			IReadOnlyList<OneDragonApplicationConfigItem> value3;
			try
			{
				ZzzOneDragonAppMergeResult zzzOneDragonAppMergeResult = ZzzOneDragonAppListMerger.Merge(savedApps, readOnlyList);
				value3 = ZzzOneDragonAppListMerger.ApplyVisibleOrder(zzzOneDragonAppMergeResult.AllApps, registeredAppIds, request.Apps);
			}
			catch (ArgumentException ex)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(ZzzBackendErrorCode.Validation, ex.Message);
			}
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = _configScopes.Save(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = value3 }, value, "default"));
			return (ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>)(zzzBackendResult2.Success ? ((object)GetOneDragonApps(value)) : ((object)ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(zzzBackendResult2.ErrorCode ?? ZzzBackendErrorCode.NotReady, zzzBackendResult2.Error ?? "一条龙应用组保存失败。")));
		}
		catch (Exception ex2)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Fail(ZzzBackendErrorCode.NotReady, ex2.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzChargePlanCatalogDto> GetChargePlanCatalog()
	{
		try
		{
			ZContext context = _runtime.EnsureContext();
			ZzzChargePlanCategoryDto[] categories = context.CompendiumService.GetChargePlanCategoryList().Select(delegate(ConfigItem category)
			{
				string categoryName = category.Value?.ToString() ?? string.Empty;
				ZzzChargePlanMissionTypeDto[] missionTypes = context.CompendiumService.GetChargePlanMissionTypeList(categoryName).Select(delegate(ConfigItem missionType)
				{
					string text = missionType.Value?.ToString() ?? string.Empty;
					ZzzChargePlanMissionDto[] missions = (from mission in context.CompendiumService.GetChargePlanMissionList(categoryName, text)
						select new ZzzChargePlanMissionDto(mission.Label, mission.Value?.ToString() ?? string.Empty)).ToArray();
					return new ZzzChargePlanMissionTypeDto(missionType.Label, text, missions);
				}).ToArray();
				return new ZzzChargePlanCategoryDto(category.Label, categoryName, missionTypes);
			}).ToArray();
			ZzzChargePlanTeamDto[] teams = context.TeamConfig.TeamList.Select((PredefinedTeamInfo team) => new ZzzChargePlanTeamDto(team.Idx, team.Name)).ToArray();
			IReadOnlyList<string> autoBattleConfigs = (from item in new AutoBattleConfigProvider(new OneDragonEnvironment(_runtime.RunRoot)).GetAutoBattleOpConfigList("auto_battle")
				select item.Value?.ToString() ?? item.Label).ToArray();
			return ZzzBackendResult<ZzzChargePlanCatalogDto>.Ok(new ZzzChargePlanCatalogDto(categories, teams, autoBattleConfigs));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzChargePlanCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> ResetShiyuDefenseRunRecord(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			ShiyuDefenseRunRecord shiyuDefenseRunRecord = (ShiyuDefenseRunRecord)zContext.RunContext.GetRunRecord("shiyu_defense", instanceIndex);
			shiyuDefenseRunRecord.ResetRecord();
			return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Ok(new ZzzShiyuDefenseRunRecordDto(instanceIndex, shiyuDefenseRunRecord.CriticalHistory.ToArray()));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto> GetWitheredDomainSettingsCatalog(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			WitheredDomainChallengeConfigStore witheredDomainChallengeConfigStore = new WitheredDomainChallengeConfigStore(environment);
			CompendiumService compendiumService = new CompendiumService(environment);
			IReadOnlyList<string> hollowZeroMissionNameList = compendiumService.GetHollowZeroMissionNameList();
			IReadOnlyList<string> autoBattleConfigs = (from item in new AutoBattleConfigProvider(environment).GetAutoBattleOpConfigList("auto_battle")
				select item.Value?.ToString() ?? item.Label).ToArray();
			List<ZzzWitheredDomainOptionDto> list = new List<ZzzWitheredDomainOptionDto>();
			list.AddRange(from type in Enum.GetValues<AgentTypeEnum>()
				where type != AgentTypeEnum.UNKNOWN
				select new ZzzWitheredDomainOptionDto(type.GetStringValue(), type.GetStringValue()));
			list.AddRange(AgentEnum.Values.Select((AgentEnum agent) => new ZzzWitheredDomainOptionDto(agent.Value.AgentName, agent.Value.AgentId)));
			ZzzWitheredDomainOptionDto[] agentOptions = list.ToArray();
			ZzzWitheredDomainOptionDto[] pathFindingOptions = WitheredDomainPathFinding.Options.Select((ConfigItem item) => new ZzzWitheredDomainOptionDto(item.Label, item.Value?.ToString() ?? string.Empty)).ToArray();
			WitheredDomainRunRecord record = LoadWitheredDomainRunRecord(environment, instanceIndex);
			return ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto>.Ok(new ZzzWitheredDomainSettingsCatalogDto(hollowZeroMissionNameList, witheredDomainChallengeConfigStore.GetAll().Select(ToDto).ToArray(), autoBattleConfigs, agentOptions, pathFindingOptions, witheredDomainChallengeConfigStore.GetDefaultGoInOneStep(), witheredDomainChallengeConfigStore.GetDefaultWaypoint(), witheredDomainChallengeConfigStore.GetDefaultAvoid(), ToDto(instanceIndex, record), witheredDomainChallengeConfigStore.GetNewModuleName()));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto> SaveWitheredDomainChallengeConfig(ZzzSaveWitheredDomainChallengeConfigRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			WitheredDomainChallengeConfigStore witheredDomainChallengeConfigStore = new WitheredDomainChallengeConfigStore(new OneDragonEnvironment(_runtime.RunRoot));
			WitheredDomainChallengeValidationResult witheredDomainChallengeValidationResult = witheredDomainChallengeConfigStore.ValidateResoniumText(request.ResoniumPriorityText);
			WitheredDomainChallengeValidationResult witheredDomainChallengeValidationResult2 = witheredDomainChallengeConfigStore.ValidateEntryText(request.GoInOneStepText);
			WitheredDomainChallengeValidationResult witheredDomainChallengeValidationResult3 = witheredDomainChallengeConfigStore.ValidateEntryText(request.WaypointText);
			WitheredDomainChallengeValidationResult witheredDomainChallengeValidationResult4 = witheredDomainChallengeConfigStore.ValidateEntryText(request.AvoidText);
			List<string> list = request.TargetAgents.Take(3).ToList();
			while (list.Count < 3)
			{
				list.Add(null);
			}
			WitheredDomainChallengeConfig config = new WitheredDomainChallengeConfig
			{
				AutoBattle = request.AutoBattle,
				ResoniumPriority = witheredDomainChallengeValidationResult.Values.ToList(),
				EventPriority = (from item in request.EventPriorityText.Split('\n')
					select item.Trim()).ToList(),
				TargetAgents = list,
				PathFinding = request.PathFinding,
				GoInOneStep = witheredDomainChallengeValidationResult2.Values.ToList(),
				Waypoint = witheredDomainChallengeValidationResult3.Values.ToList(),
				Avoid = witheredDomainChallengeValidationResult4.Values.ToList(),
				BuyOnlyPriority = request.BuyOnlyPriority
			};
			WitheredDomainChallengeConfigEntry entry = witheredDomainChallengeConfigStore.Save(request.OriginalModuleName, request.ModuleName, config);
			string text = string.Join("; ", new string[4] { witheredDomainChallengeValidationResult.Error, witheredDomainChallengeValidationResult2.Error, witheredDomainChallengeValidationResult3.Error, witheredDomainChallengeValidationResult4.Error }.Where((string value) => !string.IsNullOrWhiteSpace(value)));
			return ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto>.Ok(ToDto(entry)with
			{
				ValidationError = (string.IsNullOrWhiteSpace(text) ? null : text)
			});
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto>.Fail(ZzzBackendErrorCode.Validation, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzWitheredDomainChallengeConfigDto>> DeleteWitheredDomainChallengeConfig(string moduleName)
	{
		try
		{
			WitheredDomainChallengeConfigStore witheredDomainChallengeConfigStore = new WitheredDomainChallengeConfigStore(new OneDragonEnvironment(_runtime.RunRoot));
			witheredDomainChallengeConfigStore.Delete(moduleName);
			return ZzzBackendResult<IReadOnlyList<ZzzWitheredDomainChallengeConfigDto>>.Ok(witheredDomainChallengeConfigStore.GetAll().Select(ToDto).ToArray());
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzWitheredDomainChallengeConfigDto>>.Fail(ZzzBackendErrorCode.Validation, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWitheredDomainRunRecordDto> ResetWitheredDomainRunRecord(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<ZzzWitheredDomainRunRecordDto>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			WitheredDomainRunRecord witheredDomainRunRecord = LoadWitheredDomainRunRecord(environment, instanceIndex);
			witheredDomainRunRecord.ResetForWeekly();
			return ZzzBackendResult<ZzzWitheredDomainRunRecordDto>.Ok(ToDto(instanceIndex, witheredDomainRunRecord));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWitheredDomainRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	private static WitheredDomainRunRecord LoadWitheredDomainRunRecord(OneDragonEnvironment environment, int instanceIndex)
	{
		WitheredDomainConfig config = WitheredDomainConfig.Load(environment, instanceIndex, "default");
		GameAccountConfig current = new YamlConfig<GameAccountConfig>(environment, "game_account", null, instanceIndex).Current;
		return WitheredDomainRunRecord.Load(environment, config, instanceIndex, current.GameRefreshHourOffset);
	}

	private static ZzzWitheredDomainRunRecordDto ToDto(int instanceIndex, WitheredDomainRunRecord record)
	{
		return new ZzzWitheredDomainRunRecordDto(instanceIndex, record.WeeklyRunTimes, record.DailyRunTimes, record.NoEvalPoint, record.PeriodRewardComplete);
	}

	private static ZzzWitheredDomainChallengeConfigDto ToDto(WitheredDomainChallengeConfigEntry entry)
	{
		return new ZzzWitheredDomainChallengeConfigDto(entry.ModuleName, entry.IsSample, entry.Config.AutoBattle, entry.Config.ResoniumPriority.ToArray(), entry.Config.EventPriority.ToArray(), entry.Config.TargetAgents.ToArray(), entry.Config.PathFinding, entry.Config.GoInOneStep.ToArray(), entry.Config.Waypoint.ToArray(), entry.Config.Avoid.ToArray(), entry.Config.BuyOnlyPriority);
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> GetBattleAssistantConfigCatalog()
	{
		try
		{
			AutoBattleConfigProvider provider = new AutoBattleConfigProvider(new OneDragonEnvironment(_runtime.RunRoot));
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(CreateBattleAssistantCatalog(provider));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> DeleteBattleAssistantConfig(ZzzDeleteBattleAssistantConfigRequest request)
	{
		try
		{
			AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(_runtime.RunRoot));
			string subDir = ((request.Kind == ZzzBattleAssistantConfigKind.AutoBattle) ? "auto_battle" : "dodge");
			autoBattleConfigProvider.DeleteAutoBattleOpConfig(subDir, request.Name);
			ZzzBattleAssistantConfigCatalogDto zzzBattleAssistantConfigCatalogDto = CreateBattleAssistantCatalog(autoBattleConfigProvider);
			_eventBus.Publish("battleAssistant.configCatalogChanged", zzzBattleAssistantConfigCatalogDto);
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(zzzBattleAssistantConfigCatalogDto);
		}
		catch (ArgumentException ex)
		{
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Fail(ZzzBackendErrorCode.Validation, ex.Message);
		}
		catch (Exception ex2)
		{
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex2.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<bool> ResetIntelBoardProgress(int? instanceIndex = null)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			int num = instanceIndex ?? _runtime.ActiveInstanceIndex;
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			IntelBoardConfig config = IntelBoardConfig.Load(environment, num, "one_dragon");
			IntelBoardRunRecord intelBoardRunRecord = IntelBoardRunRecord.Load(environment, num, config);
			intelBoardRunRecord.ResetRecord();
			_eventBus.Publish("intelBoard.progressReset", num);
			return ZzzBackendResult<bool>.Ok(value: true);
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> GetRedemptionCodes()
	{
		try
		{
			RedemptionCodeConfig config = CreateRedemptionCodeConfig();
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Ok(BuildRedemptionCodeRows(config));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> AddRedemptionCode(string code, int endDate)
	{
		try
		{
			RedemptionCodeConfig redemptionCodeConfig = CreateRedemptionCodeConfig();
			string text = code.Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return RedemptionCodeValidation("兑换码不能为空。");
			}
			if (redemptionCodeConfig.CodesDict.ContainsKey(text))
			{
				return RedemptionCodeValidation("兑换码已存在");
			}
			redemptionCodeConfig.AddCode(text, endDate);
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Ok(BuildRedemptionCodeRows(redemptionCodeConfig));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> UpdateRedemptionCode(string oldCode, string newCode, int endDate)
	{
		try
		{
			RedemptionCodeConfig redemptionCodeConfig = CreateRedemptionCodeConfig();
			string text = oldCode.Trim();
			string text2 = newCode.Trim();
			if (!redemptionCodeConfig.UserCodesDict.ContainsKey(text))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotFound, "用户兑换码不存在 " + text);
			}
			if (string.IsNullOrWhiteSpace(text2))
			{
				return RedemptionCodeValidation("兑换码不能为空。");
			}
			if (!string.Equals(text, text2, StringComparison.Ordinal) && redemptionCodeConfig.CodesDict.ContainsKey(text2))
			{
				return RedemptionCodeValidation("兑换码已存在");
			}
			redemptionCodeConfig.UpdateCode(text, text2, endDate);
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Ok(BuildRedemptionCodeRows(redemptionCodeConfig));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> DeleteRedemptionCode(string code)
	{
		try
		{
			RedemptionCodeConfig redemptionCodeConfig = CreateRedemptionCodeConfig();
			string text = code.Trim();
			if (!redemptionCodeConfig.UserCodesDict.ContainsKey(text))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotFound, "用户兑换码不存在 " + text);
			}
			redemptionCodeConfig.DeleteCode(text);
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Ok(BuildRedemptionCodeRows(redemptionCodeConfig));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	private RedemptionCodeConfig CreateRedemptionCodeConfig()
	{
		return new RedemptionCodeConfig(new OneDragonEnvironment(_runtime.RunRoot));
	}

	private static IReadOnlyList<ZzzRedemptionCodeDto> BuildRedemptionCodeRows(RedemptionCodeConfig config)
	{
		IReadOnlyDictionary<string, int> sampleCodesDict = config.SampleCodesDict;
		IReadOnlyDictionary<string, int> userCodesDict = config.UserCodesDict;
		List<ZzzRedemptionCodeDto> list = new List<ZzzRedemptionCodeDto>();
		foreach (var (text2, endDate) in sampleCodesDict)
		{
			if (!userCodesDict.ContainsKey(text2))
			{
				list.Add(new ZzzRedemptionCodeDto(text2, endDate, ReadOnly: true));
			}
		}
		list.AddRange(userCodesDict.Select((KeyValuePair<string, int> item) => new ZzzRedemptionCodeDto(item.Key, item.Value, ReadOnly: false)));
		return list;
	}

	private static ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> RedemptionCodeValidation(string message)
	{
		return ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>>.Fail(ZzzBackendErrorCode.Validation, message);
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolCatalogDto> GetWorldPatrolCatalog(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Ok(BuildWorldPatrolCatalog(instanceIndex));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolCatalogDto> SaveWorldPatrolRouteList(ZzzSaveWorldPatrolRouteListRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (!TryValidateWorldPatrolRouteList(request.Name, request.ListType, out string error))
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.Validation, error);
		}
		try
		{
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			return worldPatrolService.SaveWorldPatrolRouteList(new WorldPatrolRouteList
			{
				Name = request.Name.Trim(),
				ListType = request.ListType,
				RouteItems = request.RouteItems.ToList()
			}) ? ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Ok(BuildWorldPatrolCatalog(request.InstanceIndex)) : ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, "保存路线列表失败");
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolCatalogDto> DeleteWorldPatrolRouteList(int instanceIndex, string name)
	{
		if (!TryValidateWorldPatrolRouteList(name, "whitelist", out string error))
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.Validation, error);
		}
		try
		{
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			WorldPatrolRouteList worldPatrolRouteList = worldPatrolService.GetWorldPatrolRouteLists().FirstOrDefault((WorldPatrolRouteList item) => string.Equals(item.Name, name.Trim(), StringComparison.Ordinal));
			if (worldPatrolRouteList == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotFound, "路线列表不存在 " + name.Trim());
			}
			return worldPatrolService.DeleteWorldPatrolRouteList(worldPatrolRouteList) ? ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Ok(BuildWorldPatrolCatalog(instanceIndex)) : ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, "删除路线列表失败");
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolRunRecordDto> ResetWorldPatrolRunRecord(int instanceIndex)
	{
		if (instanceIndex < 0)
		{
			return ZzzBackendResult<ZzzWorldPatrolRunRecordDto>.Fail(ZzzBackendErrorCode.Validation, "实例编号不能小于 0。");
		}
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			WorldPatrolRunRecord worldPatrolRunRecord = WorldPatrolRunRecord.Load(environment, instanceIndex);
			worldPatrolRunRecord.ResetRecord();
			return ZzzBackendResult<ZzzWorldPatrolRunRecordDto>.Ok(ToWorldPatrolRunRecordDto(instanceIndex, worldPatrolRunRecord));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolCatalogDto> SaveWorldPatrolRoute(ZzzSaveWorldPatrolRouteRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (!TryBuildWorldPatrolRoute(request, out WorldPatrolRoute route, out string error))
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.Validation, error);
		}
		try
		{
			WorldPatrolRoute worldPatrolRoute = route;
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			worldPatrolService.LoadData();
			if (!string.IsNullOrWhiteSpace(request.OriginalFullId))
			{
				WorldPatrolRoute worldPatrolRoute2 = worldPatrolService.GetWorldPatrolRoutes().FirstOrDefault((WorldPatrolRoute item) => string.Equals(item.FullId, request.OriginalFullId, StringComparison.Ordinal));
				if (worldPatrolRoute2 == null)
				{
					return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotFound, "路线不存在 " + request.OriginalFullId);
				}
				worldPatrolRoute.TpArea = worldPatrolRoute2.TpArea;
				worldPatrolRoute.TpAreaId = worldPatrolRoute2.TpAreaId;
				worldPatrolRoute.Idx = worldPatrolRoute2.Idx;
			}
			else
			{
				WorldPatrolArea worldPatrolArea = worldPatrolService.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, request.AreaId, StringComparison.Ordinal));
				if (worldPatrolArea == null)
				{
					return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotFound, "区域不存在 " + request.AreaId);
				}
				WorldPatrolLargeMapIcon worldPatrolLargeMapIcon = worldPatrolService.GetLargeMapByAreaFullId(worldPatrolArea.FullId)?.IconList.FirstOrDefault((WorldPatrolLargeMapIcon icon) => string.Equals(icon.TemplateId, "map_icon_01", StringComparison.Ordinal) && string.Equals(icon.IconName, request.TransportPoint, StringComparison.Ordinal));
				if (worldPatrolLargeMapIcon == null)
				{
					return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.Validation, "新增路线必须选择大地图中真实存在的传送点。");
				}
				worldPatrolRoute.TpArea = worldPatrolArea;
				worldPatrolRoute.TpAreaId = worldPatrolArea.FullId;
				worldPatrolRoute.Idx = ((request.Index > 0) ? request.Index : worldPatrolService.GetNextRouteIdx(worldPatrolArea));
			}
			return worldPatrolService.SaveWorldPatrolRoute(worldPatrolRoute) ? ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Ok(BuildWorldPatrolCatalog(request.InstanceIndex)) : ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, "保存路线失败");
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolCatalogDto> DeleteWorldPatrolRoute(int instanceIndex, string fullId)
	{
		try
		{
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			worldPatrolService.LoadData();
			WorldPatrolRoute worldPatrolRoute = worldPatrolService.GetWorldPatrolRoutes().FirstOrDefault((WorldPatrolRoute item) => string.Equals(item.FullId, fullId, StringComparison.Ordinal));
			if (worldPatrolRoute == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotFound, "路线不存在 " + fullId);
			}
			return worldPatrolService.DeleteWorldPatrolRoute(worldPatrolRoute) ? ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Ok(BuildWorldPatrolCatalog(instanceIndex)) : ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, "删除路线失败");
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> CaptureWorldPatrolRoutePosition(ZzzCaptureWorldPatrolRoutePositionRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			worldPatrolService.LoadData();
			WorldPatrolArea worldPatrolArea = worldPatrolService.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, request.AreaId, StringComparison.Ordinal));
			if (worldPatrolArea == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotFound, "区域不存在 " + request.AreaId);
			}
			WorldPatrolLargeMap largeMapByAreaFullId = worldPatrolService.GetLargeMapByAreaFullId(worldPatrolArea.FullId);
			if (largeMapByAreaFullId?.RoadMask == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotReady, "当前区域缺少真实道路地图数据。");
			}
			WorldPatrolLargeMapIcon worldPatrolLargeMapIcon = largeMapByAreaFullId.IconList.FirstOrDefault((WorldPatrolLargeMapIcon icon) => string.Equals(icon.TemplateId, "map_icon_01", StringComparison.Ordinal) && string.Equals(icon.IconName, request.TransportPoint, StringComparison.Ordinal));
			if (worldPatrolLargeMapIcon == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.Validation, "当前路线缺少真实传送点。");
			}
			WorldPatrolPoint worldPatrolPoint = worldPatrolLargeMapIcon.TransportPosition;
			foreach (ZzzWorldPatrolOperationDto operation in request.Operations)
			{
				if (!string.Equals(operation.OpType, "move", StringComparison.Ordinal) || operation.Data.Count < 2 || !int.TryParse(operation.Data[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || !int.TryParse(operation.Data[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
				{
					return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.Validation, "路线包含无法用于定位的操作。");
				}
				worldPatrolPoint = new WorldPatrolPoint(result, result2);
			}
			using Mat screen = zContext.Backend.Capture();
			WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = worldPatrolService.CutMiniMap(zContext, screen);
			try
			{
				if (worldPatrolMiniMapSnapshot.RoadMask == null || worldPatrolMiniMapSnapshot.Rgb == null)
				{
					return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotReady, "真实游戏截图中未能提取小地图。");
				}
				OneDragon.Core.Abstractions.Geometry.Rect largeMapRect = new OneDragon.Core.Abstractions.Geometry.Rect(worldPatrolPoint.X - worldPatrolMiniMapSnapshot.RoadMask.Cols * 2, worldPatrolPoint.Y - worldPatrolMiniMapSnapshot.RoadMask.Rows * 2, worldPatrolPoint.X + worldPatrolMiniMapSnapshot.RoadMask.Cols * 2, worldPatrolPoint.Y + worldPatrolMiniMapSnapshot.RoadMask.Rows * 2);
				WorldPatrolPoint? worldPatrolPoint2 = worldPatrolService.CalculateCurrentPosition(zContext, largeMapByAreaFullId, worldPatrolMiniMapSnapshot, largeMapRect);
				if (!worldPatrolPoint2.HasValue)
				{
					return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotReady, "当前游戏截图定位失败。");
				}
				using MiniMapSnapshot miniMapSnapshot = CreateLargeMapRecorderMiniMapSnapshot(zContext, worldPatrolMiniMapSnapshot, 0.7);
				ZzzWorldPatrolRecorderImageDto miniMapRoad = null;
				if ((object)miniMapSnapshot != null)
				{
					using Mat bgr = RenderMiniMapDisplay(zContext.TemplateLoader, miniMapSnapshot);
					miniMapRoad = EncodeBgrImage(bgr);
				}
				ZzzWorldPatrolOperationDto element = new ZzzWorldPatrolOperationDto("move", new string[2]
				{
					worldPatrolPoint2.Value.X.ToString(CultureInfo.InvariantCulture),
					worldPatrolPoint2.Value.Y.ToString(CultureInfo.InvariantCulture)
				});
				using Mat bgr2 = RenderWorldPatrolRouteMap(zContext.TemplateLoader, largeMapByAreaFullId, worldPatrolLargeMapIcon, request.Operations.Append(element));
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Ok(new ZzzWorldPatrolRoutePositionDto(worldPatrolPoint2.Value.X, worldPatrolPoint2.Value.Y)
				{
					MiniMapRoad = miniMapRoad,
					RouteMap = EncodeBgrImage(bgr2)
				});
			}
			finally
			{
				worldPatrolMiniMapSnapshot.RoadMask?.Dispose();
				worldPatrolMiniMapSnapshot.Rgb?.Dispose();
			}
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolRouteVisualDto> RenderWorldPatrolRouteRecorder(ZzzWorldPatrolRouteVisualRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			worldPatrolService.LoadData();
			if (!TryResolveWorldPatrolRouteVisual(worldPatrolService, request.AreaId, request.TransportPoint, out WorldPatrolLargeMap largeMap, out WorldPatrolLargeMapIcon transportPoint, out ZzzBackendErrorCode errorCode, out string error))
			{
				return ZzzBackendResult<ZzzWorldPatrolRouteVisualDto>.Fail(errorCode, error);
			}
			using TemplateLoader templateLoader = new TemplateLoader(new OneDragonEnvironment(_runtime.RunRoot));
			using Mat bgr = RenderWorldPatrolRouteMap(templateLoader, largeMap, transportPoint, request.Operations);
			return ZzzBackendResult<ZzzWorldPatrolRouteVisualDto>.Ok(new ZzzWorldPatrolRouteVisualDto(EncodeBgrImage(bgr)));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolRouteVisualDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> ConvertWorldPatrolRouteRecorderClick(ZzzWorldPatrolRouteMapClickRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (request.ViewportWidth <= 0.0 || request.ViewportHeight <= 0.0)
		{
			return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.Validation, "大地图显示区域尺寸必须大于 0。");
		}
		try
		{
			WorldPatrolService worldPatrolService = CreateWorldPatrolService();
			worldPatrolService.LoadData();
			if (!TryResolveWorldPatrolRouteVisual(worldPatrolService, request.AreaId, request.TransportPoint, out WorldPatrolLargeMap largeMap, out WorldPatrolLargeMapIcon _, out ZzzBackendErrorCode errorCode, out string error))
			{
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(errorCode, error);
			}
			double num = Math.Min(request.ViewportWidth / (double)largeMap.RoadMask.Cols, request.ViewportHeight / (double)largeMap.RoadMask.Rows);
			double num2 = (double)largeMap.RoadMask.Cols * num;
			double num3 = (double)largeMap.RoadMask.Rows * num;
			double num4 = (request.ViewportWidth - num2) / 2.0;
			double num5 = (request.ViewportHeight - num3) / 2.0;
			double num6 = (request.ClickX - num4) / num;
			double num7 = (request.ClickY - num5) / num;
			if (num6 < 0.0 || num7 < 0.0 || num6 >= (double)largeMap.RoadMask.Cols || num7 >= (double)largeMap.RoadMask.Rows)
			{
				return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.Validation, "点击位置不在大地图图像范围内。");
			}
			return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Ok(new ZzzWorldPatrolRoutePositionDto((int)num6, (int)num7));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWorldPatrolRoutePositionDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public async Task<ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>> DebugWorldPatrolRouteAsync(ZzzDebugWorldPatrolRouteRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			WorldPatrolService service = CreateWorldPatrolService();
			service.LoadData();
			WorldPatrolRoute route = service.GetWorldPatrolRoutes().FirstOrDefault((WorldPatrolRoute item) => string.Equals(item.FullId, request.FullId, StringComparison.Ordinal));
			if (route == null)
			{
				return ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>.Fail(ZzzBackendErrorCode.NotFound, "路线不存在 " + request.FullId);
			}
			if (request.StartIndex < 0 || request.StartIndex > route.OpList.Count)
			{
				return ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>.Fail(ZzzBackendErrorCode.Validation, "调试起始下标超出路线操作范围。");
			}
			ZContext context = _runtime.EnsureContext();
			using (_lock.EnterScope())
			{
				if (_runtime.IsRunActive || !context.RunContext.StartRunning())
				{
					return ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>.Fail(ZzzBackendErrorCode.Conflict, "已有运行中的应用。");
				}
			}
			WorldPatrolRunRoute operation = new WorldPatrolRunRoute(context, route, WorldPatrolConfig.Load(context.Environment, request.InstanceIndex, request.GroupId), request.StartIndex);
			PublishRunEvents(new ZzzRunStatusDto(ZzzRunState.Running, "world_patrol", "锄大地路线调试", request.InstanceIndex, request.GroupId, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
			OperationResult result;
			try
			{
				result = await operation.ExecuteAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				await context.RunContext.StopRunningAsync(TimeSpan.FromSeconds(1L)).ConfigureAwait(continueOnCapturedContext: false);
			}
			ZzzAppBackend zzzAppBackend = this;
			int state = (result.IsSuccess ? 5 : 6);
			int? instanceIndex = request.InstanceIndex;
			string groupId = request.GroupId;
			string finishedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
			string status = result.Status;
			zzzAppBackend.PublishRunEvents(new ZzzRunStatusDto((ZzzRunState)state, "world_patrol", "锄大地路线调试", instanceIndex, groupId, null, finishedAt, null, status));
			return ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>.Ok(new ZzzWorldPatrolRouteDebugDto(result.IsSuccess, result.Status ?? string.Empty, operation.CurrentIdx));
		}
		catch (Exception ex)
		{
			Exception exception = ex;
			return ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>.Fail(ZzzBackendErrorCode.NotReady, exception.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> LoadWorldPatrolLargeMapRecorder(int instanceIndex, string areaId)
	{
		return WithWorldPatrolLargeMapRecorderGate(delegate
		{
			if (!TryValidateLargeMapRecorderInstance(instanceIndex, out string error))
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.Conflict, error);
			}
			if (!string.IsNullOrWhiteSpace(areaId))
			{
				try
				{
					WorldPatrolService worldPatrolService = CreateWorldPatrolService();
					worldPatrolService.LoadData();
					WorldPatrolArea worldPatrolArea = worldPatrolService.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, areaId.Trim(), StringComparison.Ordinal));
					if (worldPatrolArea == null)
					{
						return LargeMapRecorderFailure(ZzzBackendErrorCode.NotFound, "区域不存在 " + areaId.Trim());
					}
					WorldPatrolLargeMapRecorderSession worldPatrolLargeMapRecorderSession = new WorldPatrolLargeMapRecorderSession(instanceIndex, worldPatrolArea);
					WorldPatrolLargeMap largeMapByAreaFullId = worldPatrolService.GetLargeMapByAreaFullId(worldPatrolArea.FullId);
					if (largeMapByAreaFullId?.RoadMask != null)
					{
						worldPatrolLargeMapRecorderSession.LargeMap = new LargeMapSnapshot(worldPatrolArea.FullId, largeMapByAreaFullId.RoadMask.Clone(), largeMapByAreaFullId.IconList.Select(ToLargeMapIcon).ToArray(), new OneDragon.Core.Abstractions.Geometry.Point(largeMapByAreaFullId.RoadMask.Cols / 2, largeMapByAreaFullId.RoadMask.Rows / 2));
						worldPatrolLargeMapRecorderSession.CurrentPosition = worldPatrolLargeMapRecorderSession.LargeMap.PositionAfterMerge;
						worldPatrolLargeMapRecorderSession.HasPersistedMap = true;
					}
					ReplaceWorldPatrolLargeMapRecorderSession(worldPatrolLargeMapRecorderSession);
					worldPatrolLargeMapRecorderSession.Status = ((largeMapByAreaFullId?.RoadMask == null) ? "未有地图数据 新建" : "加载成功");
					return LargeMapRecorderSuccess(worldPatrolLargeMapRecorderSession);
				}
				catch (Exception ex)
				{
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, ex.Message);
				}
			}
			return LargeMapRecorderFailure(ZzzBackendErrorCode.Validation, "区域不能为空。");
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SaveWorldPatrolLargeMapRecorder(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			if ((object)session.LargeMap != null)
			{
				try
				{
					WorldPatrolService worldPatrolService = CreateWorldPatrolService();
					worldPatrolService.LoadData();
					WorldPatrolArea worldPatrolArea = worldPatrolService.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, session.Area.FullId, StringComparison.Ordinal));
					if (worldPatrolArea != null)
					{
						using WorldPatrolLargeMap largeMap = new WorldPatrolLargeMap(worldPatrolArea.FullId, WorldPatrolPaths.RoadMaskPath(new OneDragonEnvironment(_runtime.RunRoot), worldPatrolArea), session.LargeMap.IconList.Select(ToWorldPatrolLargeMapIcon).ToArray(), session.LargeMap.RoadMask.Clone());
						if (!worldPatrolService.SaveWorldPatrolLargeMap(worldPatrolArea, largeMap))
						{
							return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "保存区域地图失败。");
						}
						session.HasPersistedMap = true;
						session.Status = "保存区域地图成功 " + worldPatrolArea.FullId;
						return LargeMapRecorderSuccess(session);
					}
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotFound, "区域不存在 " + session.Area.FullId);
				}
				catch (Exception ex)
				{
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, ex.Message);
				}
			}
			return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前没有可保存的大地图数据。");
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> DeleteWorldPatrolLargeMapRecorder(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			try
			{
				WorldPatrolService worldPatrolService = CreateWorldPatrolService();
				worldPatrolService.LoadData();
				WorldPatrolArea worldPatrolArea = worldPatrolService.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, session.Area.FullId, StringComparison.Ordinal));
				if (worldPatrolArea == null)
				{
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotFound, "区域不存在 " + session.Area.FullId);
				}
				bool flag = worldPatrolService.DeleteWorldPatrolLargeMap(worldPatrolArea);
				if (!flag && session.HasPersistedMap)
				{
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotFound, "当前区域没有可删除的大地图数据。");
				}
				session.Dispose();
				_worldPatrolLargeMapRecorderSession = null;
				return ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>.Ok(EmptyLargeMapRecorderState(instanceIndex, flag ? ("删除区域地图成功 " + worldPatrolArea.FullId) : "未有落盘地图，已清空当前录制状态"));
			}
			catch (Exception ex)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, ex.Message);
			}
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> CancelWorldPatrolLargeMapRecorder(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderGate(delegate
		{
			WorldPatrolLargeMapRecorderSession worldPatrolLargeMapRecorderSession = _worldPatrolLargeMapRecorderSession;
			if (worldPatrolLargeMapRecorderSession == null || worldPatrolLargeMapRecorderSession.InstanceIndex != instanceIndex)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前没有已加载的大地图录制会话。");
			}
			worldPatrolLargeMapRecorderSession.Dispose();
			_worldPatrolLargeMapRecorderSession = null;
			return ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>.Ok(EmptyLargeMapRecorderState(instanceIndex, "已取消"));
		});
	}

	/// <inheritdoc />
	public async Task<ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>> CaptureWorldPatrolLargeMapRecorderAsync(int instanceIndex, double iconThreshold)
	{
		await _worldPatrolLargeMapRecorderGate.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			if (!TryGetWorldPatrolLargeMapRecorderSession(instanceIndex, out WorldPatrolLargeMapRecorderSession session, out string error))
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, error);
			}
			if ((iconThreshold < 0.1 || iconThreshold > 1.0) ? true : false)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.Validation, "图标匹配阈值必须在 0.1 到 1.0 之间。");
			}
			ZContext context = _runtime.EnsureContext();
			ZPcController controller2 = default(ZPcController);
			int num;
			if (context.InstanceIndex == instanceIndex)
			{
				ControllerBase controller = context.Controller;
				controller2 = controller as ZPcController;
				num = ((controller2 == null) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			if (num != 0)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "控制器未初始化。");
			}
			if (!context.ReadyForApplication || !((ControllerBase)(object)controller2).IsGameWindowReady)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "游戏窗口未就绪。");
			}
			using Mat screen1 = ((ControllerBase)(object)controller2).Screenshot(false).Screen;
			if (screen1 == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "第一次真实游戏截图失败。");
			}
			WorldPatrolMiniMapSnapshot cut1 = context.WorldPatrolService.CutMiniMap(context, screen1);
			using MiniMapSnapshot snapshot1 = CreateLargeMapRecorderMiniMapSnapshot(context, cut1, iconThreshold);
			if ((object)snapshot1 == null || cut1.Rgb == null)
			{
				DisposeWorldPatrolMiniMapSnapshot(cut1);
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "第一次截图未能提取小地图。");
			}
			Mat miniMap1Rgb = cut1.Rgb.Clone();
			DisposeWorldPatrolMiniMapSnapshot(cut1);
			controller2.TurnByAngleDiff(180f);
			await Task.Delay(TimeSpan.FromSeconds(2L)).ConfigureAwait(continueOnCapturedContext: false);
			using Mat screen2 = ((ControllerBase)(object)controller2).Screenshot(false).Screen;
			if (screen2 == null)
			{
				miniMap1Rgb.Dispose();
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "第二次真实游戏截图失败。");
			}
			WorldPatrolMiniMapSnapshot cut2 = context.WorldPatrolService.CutMiniMap(context, screen2);
			using MiniMapSnapshot snapshot2 = CreateLargeMapRecorderMiniMapSnapshot(context, cut2, iconThreshold);
			if ((object)snapshot2 == null || cut2.Rgb == null)
			{
				miniMap1Rgb.Dispose();
				DisposeWorldPatrolMiniMapSnapshot(cut2);
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "第二次截图未能提取小地图。");
			}
			Mat miniMap2Rgb = cut2.Rgb.Clone();
			DisposeWorldPatrolMiniMapSnapshot(cut2);
			MiniMapSnapshot merged = LargeMapRecorderUtils.MergeMiniMap(snapshot1, snapshot2);
			session.ReplaceMiniMaps(miniMap1Rgb, miniMap2Rgb, merged);
			session.PositionMatch = null;
			session.Status = "[截图] 计算小地图道路 完成";
			return LargeMapRecorderSuccess(session);
		}
		catch (Exception ex)
		{
			Exception exception = ex;
			return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, exception.Message);
		}
		finally
		{
			_worldPatrolLargeMapRecorderGate.Release();
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> CalculateWorldPatrolLargeMapRecorderPosition(int instanceIndex, bool useIcon)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			if ((object)session.LargeMap == null || (object)session.MiniMap == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前未有地图或小地图快照。");
			}
			if (!session.CurrentPosition.HasValue)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "未有上次坐标，请点击大地图选择一个坐标。");
			}
			MatchResult matchResult = LargeMapRecorderUtils.CalculatePosition(session.LargeMap, session.MiniMap, session.CurrentPosition.Value, useIcon);
			if (matchResult == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前游戏截图定位失败。");
			}
			session.PositionMatch = matchResult;
			session.Status = $"[计算坐标] 完成 当前坐标 {matchResult.Center}";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> ToggleWorldPatrolLargeMapRecorderOverlap(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			session.OverlapMode = (session.OverlapMode + 1) % 2;
			session.Status = "[重叠] 更改重叠显示方式";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> MergeWorldPatrolLargeMapRecorder(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			if ((object)session.MiniMap == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前没有可合并的小地图快照。");
			}
			session.LastLargeMap?.Dispose();
			session.LastLargeMap = session.LargeMap?.DeepClone();
			session.LastPosition = session.CurrentPosition;
			LargeMapSnapshot largeMapSnapshot = LargeMapRecorderUtils.MergeLargeMap(session.LargeMap, session.MiniMap, session.PositionMatch);
			session.LargeMap?.Dispose();
			session.LargeMap = (string.Equals(largeMapSnapshot.AreaFullId, session.Area.FullId, StringComparison.Ordinal) ? largeMapSnapshot : CopyLargeMapSnapshotWithArea(largeMapSnapshot, session.Area.FullId));
			session.CurrentPosition = session.LargeMap.PositionAfterMerge;
			session.PositionMatch = null;
			session.Status = "[合并到大地图] 完成";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> UndoWorldPatrolLargeMapRecorder(int instanceIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			if ((object)session.LastLargeMap == null)
			{
				session.LargeMap?.Dispose();
				session.LargeMap = null;
				session.CurrentPosition = null;
				session.LastPosition = null;
				session.PositionMatch = null;
				session.HighlightedIconIndex = -1;
				session.Status = "[回退] 恢复上一步大地图，只能恢复一次";
				return LargeMapRecorderSuccess(session);
			}
			session.LargeMap?.Dispose();
			session.LargeMap = session.LastLargeMap;
			session.LastLargeMap = null;
			session.CurrentPosition = session.LastPosition;
			session.LastPosition = null;
			session.PositionMatch = null;
			session.Status = "[回退] 恢复上一步大地图，只能恢复一次";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> ScaleWorldPatrolLargeMapRecorder(int instanceIndex, int percent)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			if ((object)session.LargeMap == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前没有可缩放的大地图数据。");
			}
			if (percent > 0)
			{
				using Mat mat = new Mat();
				double num = (double)percent / 100.0;
				Cv2.Resize(session.LargeMap.RoadMask, mat, default(Size), num, num);
				LargeMapSnapshot largeMapSnapshot = new LargeMapSnapshot(session.Area.FullId, mat.Clone(), session.LargeMap.IconList.Select((LargeMapIcon icon) => icon with { }).ToArray(), session.LargeMap.PositionAfterMerge);
				using (largeMapSnapshot)
				{
					LargeMapSnapshot largeMap = ExpandLargeMapRecorderEdges(largeMapSnapshot, 210, 210);
					session.LargeMap.Dispose();
					session.LargeMap = largeMap;
				}
				session.PositionMatch = null;
				session.Status = $"缩放已应用 {percent}%";
				return LargeMapRecorderSuccess(session);
			}
			return LargeMapRecorderFailure(ZzzBackendErrorCode.Validation, "缩放百分比必须大于 0。");
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> MoveWorldPatrolLargeMapRecorder(int instanceIndex, int deltaX, int deltaY)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(deltaX, deltaY);
			if (session.PositionMatch != null)
			{
				session.PositionMatch.AddOffset(point);
			}
			else if (session.CurrentPosition.HasValue)
			{
				session.CurrentPosition += point;
			}
			session.Status = $"坐标已移动 ({deltaX}, {deltaY})";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SetWorldPatrolLargeMapRecorderPosition(int instanceIndex, int x, int y)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			session.CurrentPosition = new OneDragon.Core.Abstractions.Geometry.Point(x, y);
			session.PositionMatch = null;
			session.Status = "[位置] 更新为点击位置";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> UpdateWorldPatrolLargeMapRecorderIcons(int instanceIndex, IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> icons)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			ArgumentNullException.ThrowIfNull(icons, "icons");
			if ((object)session.LargeMap == null)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, "当前没有可编辑的大地图数据。");
			}
			LargeMapIcon[] iconList = icons.Select((ZzzWorldPatrolLargeMapIconDto icon) => new LargeMapIcon(icon.IconName, icon.TemplateId, new OneDragon.Core.Abstractions.Geometry.Point(icon.LargeMapPosition.X, icon.LargeMapPosition.Y), new OneDragon.Core.Abstractions.Geometry.Point(icon.TeleportPosition.X, icon.TeleportPosition.Y))).ToArray();
			LargeMapSnapshot largeMap = new LargeMapSnapshot(session.Area.FullId, session.LargeMap.RoadMask.Clone(), iconList, session.LargeMap.PositionAfterMerge);
			session.LargeMap.Dispose();
			session.LargeMap = largeMap;
			session.Status = "图标列表已更新";
			return LargeMapRecorderSuccess(session);
		});
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SelectWorldPatrolLargeMapRecorderIcon(int instanceIndex, int iconIndex)
	{
		return WithWorldPatrolLargeMapRecorderSession(instanceIndex, delegate(WorldPatrolLargeMapRecorderSession session)
		{
			int num = session.LargeMap?.IconList.Count ?? 0;
			if (iconIndex < -1 || iconIndex >= num)
			{
				return LargeMapRecorderFailure(ZzzBackendErrorCode.Validation, "图标下标超出范围。");
			}
			session.HighlightedIconIndex = iconIndex;
			session.Status = ((iconIndex < 0) ? "已清除图标高亮" : $"已选择图标 {iconIndex + 1}");
			return LargeMapRecorderSuccess(session);
		});
	}

	private ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> WithWorldPatrolLargeMapRecorderSession(int instanceIndex, Func<WorldPatrolLargeMapRecorderSession, ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>> action)
	{
		return WithWorldPatrolLargeMapRecorderGate(delegate
		{
			if (TryGetWorldPatrolLargeMapRecorderSession(instanceIndex, out WorldPatrolLargeMapRecorderSession session, out string error))
			{
				try
				{
					return action(session);
				}
				catch (Exception ex)
				{
					return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, ex.Message);
				}
			}
			return LargeMapRecorderFailure(ZzzBackendErrorCode.NotReady, error);
		});
	}

	private ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> WithWorldPatrolLargeMapRecorderGate(Func<ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>> action)
	{
		_worldPatrolLargeMapRecorderGate.Wait();
		try
		{
			return action();
		}
		finally
		{
			_worldPatrolLargeMapRecorderGate.Release();
		}
	}

	private bool TryGetWorldPatrolLargeMapRecorderSession(int instanceIndex, out WorldPatrolLargeMapRecorderSession? session, out string error)
	{
		session = _worldPatrolLargeMapRecorderSession;
		if (session == null)
		{
			error = "当前没有已加载的大地图录制会话。";
			return false;
		}
		if (session.InstanceIndex != instanceIndex)
		{
			error = "实例已切换，请重新加载大地图录制区域。";
			return false;
		}
		if (!TryValidateLargeMapRecorderInstance(instanceIndex, out error))
		{
			return false;
		}
		error = string.Empty;
		return true;
	}

	private bool TryValidateLargeMapRecorderInstance(int instanceIndex, out string error)
	{
		if (instanceIndex < 0)
		{
			error = "实例编号不能小于 0。";
			return false;
		}
		if (_runtime.ActiveInstanceIndex != instanceIndex)
		{
			error = $"当前活动实例为 {_runtime.ActiveInstanceIndex:00}，请重新打开对应实例的大地图录制页面。";
			return false;
		}
		error = string.Empty;
		return true;
	}

	private void ReplaceWorldPatrolLargeMapRecorderSession(WorldPatrolLargeMapRecorderSession session)
	{
		_worldPatrolLargeMapRecorderSession?.Dispose();
		_worldPatrolLargeMapRecorderSession = session;
	}

	private ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> LargeMapRecorderSuccess(WorldPatrolLargeMapRecorderSession session)
	{
		return ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>.Ok(BuildLargeMapRecorderState(session));
	}

	private static ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> LargeMapRecorderFailure(ZzzBackendErrorCode errorCode, string error)
	{
		return ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>.Fail(errorCode, error);
	}

	private ZzzWorldPatrolLargeMapRecorderStateDto BuildLargeMapRecorderState(WorldPatrolLargeMapRecorderSession session)
	{
		using TemplateLoader templateLoader = new TemplateLoader(new OneDragonEnvironment(_runtime.RunRoot));
		ZzzWorldPatrolRecorderImageDto miniMap = EncodeRgbImage(session.MiniMap1Rgb);
		ZzzWorldPatrolRecorderImageDto miniMap2 = EncodeRgbImage(session.MiniMap2Rgb);
		ZzzWorldPatrolRecorderImageDto miniMapMerged = null;
		if ((object)session.MiniMap != null)
		{
			using Mat bgr = RenderMiniMapDisplay(templateLoader, session.MiniMap);
			miniMapMerged = EncodeBgrImage(bgr);
		}
		ZzzWorldPatrolRecorderImageDto largeMap = null;
		if ((object)session.LargeMap != null)
		{
			using Mat bgr2 = RenderLargeMapDisplay(templateLoader, session);
			largeMap = EncodeBgrImage(bgr2);
		}
		return new ZzzWorldPatrolLargeMapRecorderStateDto(session.InstanceIndex, session.Area.FullId, IsLoaded: true, (object)session.LargeMap != null, session.OverlapMode, ToPositionDto(session.CurrentPosition), (session.PositionMatch == null) ? null : new ZzzWorldPatrolRoutePositionDto(session.PositionMatch.Center.X, session.PositionMatch.Center.Y), miniMap, miniMap2, miniMapMerged, largeMap, session.LargeMap?.IconList.Select(ToLargeMapIconDto).ToArray() ?? Array.Empty<ZzzWorldPatrolLargeMapIconDto>(), session.HighlightedIconIndex, session.Status);
	}

	private static ZzzWorldPatrolLargeMapRecorderStateDto EmptyLargeMapRecorderState(int instanceIndex, string status)
	{
		return new ZzzWorldPatrolLargeMapRecorderStateDto(instanceIndex, null, IsLoaded: false, HasLargeMap: false, 1, null, null, null, null, null, null, Array.Empty<ZzzWorldPatrolLargeMapIconDto>(), -1, status);
	}

	private static ZzzWorldPatrolRoutePositionDto? ToPositionDto(OneDragon.Core.Abstractions.Geometry.Point? point)
	{
		return (!point.HasValue) ? null : new ZzzWorldPatrolRoutePositionDto(point.Value.X, point.Value.Y);
	}

	private static LargeMapIcon ToLargeMapIcon(WorldPatrolLargeMapIcon icon)
	{
		return new LargeMapIcon(icon.IconName, icon.TemplateId, new OneDragon.Core.Abstractions.Geometry.Point(icon.LargeMapPosition.X, icon.LargeMapPosition.Y), new OneDragon.Core.Abstractions.Geometry.Point(icon.TransportPosition.X, icon.TransportPosition.Y));
	}

	private static WorldPatrolLargeMapIcon ToWorldPatrolLargeMapIcon(LargeMapIcon icon)
	{
		return WorldPatrolLargeMapIcon.Create(icon.IconName, icon.TemplateId, new WorldPatrolPoint(icon.LargeMapPosition.X, icon.LargeMapPosition.Y), (!icon.TeleportPosition.HasValue) ? ((WorldPatrolPoint?)null) : new WorldPatrolPoint?(new WorldPatrolPoint(icon.TeleportPosition.Value.X, icon.TeleportPosition.Value.Y)));
	}

	private static ZzzWorldPatrolLargeMapIconDto ToLargeMapIconDto(LargeMapIcon icon)
	{
		OneDragon.Core.Abstractions.Geometry.Point point = icon.TeleportPosition ?? icon.LargeMapPosition;
		return new ZzzWorldPatrolLargeMapIconDto(icon.IconName, icon.TemplateId, new ZzzWorldPatrolRoutePositionDto(icon.LargeMapPosition.X, icon.LargeMapPosition.Y), new ZzzWorldPatrolRoutePositionDto(point.X, point.Y));
	}

	private static MiniMapSnapshot? CreateLargeMapRecorderMiniMapSnapshot(ZContext context, WorldPatrolMiniMapSnapshot cut, double iconThreshold)
	{
		if (cut.Rgb == null || cut.RoadMask == null)
		{
			return null;
		}
		return LargeMapRecorderUtils.CreateMiniMapSnapshot(context.TemplateMatcher, cut.Rgb, cut.RoadMask.Clone(), iconThreshold);
	}

	private static void DisposeWorldPatrolMiniMapSnapshot(WorldPatrolMiniMapSnapshot snapshot)
	{
		snapshot.RoadMask?.Dispose();
		snapshot.Rgb?.Dispose();
	}

	private static LargeMapSnapshot CopyLargeMapSnapshotWithArea(LargeMapSnapshot source, string areaId)
	{
		LargeMapSnapshot result = new LargeMapSnapshot(areaId, source.RoadMask.Clone(), source.IconList.Select((LargeMapIcon icon) => icon with { }).ToArray(), source.PositionAfterMerge);
		source.Dispose();
		return result;
	}

	private static LargeMapSnapshot ExpandLargeMapRecorderEdges(LargeMapSnapshot largeMap, int maskHeight, int maskWidth)
	{
		int num = Math.Max(1, maskHeight / 2);
		int num2 = Math.Max(1, maskWidth / 2);
		int num3 = ((CountNonZero(largeMap.RoadMask, new OpenCvSharp.Rect(0, 0, largeMap.RoadMask.Cols, num)) > 0) ? maskHeight : 0);
		int num4 = ((CountNonZero(largeMap.RoadMask, new OpenCvSharp.Rect(0, largeMap.RoadMask.Rows - num, largeMap.RoadMask.Cols, num)) > 0) ? maskHeight : 0);
		int num5 = ((CountNonZero(largeMap.RoadMask, new OpenCvSharp.Rect(0, 0, num2, largeMap.RoadMask.Rows)) > 0) ? maskWidth : 0);
		int num6 = ((CountNonZero(largeMap.RoadMask, new OpenCvSharp.Rect(largeMap.RoadMask.Cols - num2, 0, num2, largeMap.RoadMask.Rows)) > 0) ? maskWidth : 0);
		if (num3 == 0 && num4 == 0 && num5 == 0 && num6 == 0)
		{
			return largeMap.DeepClone();
		}
		Mat mat = new Mat(largeMap.RoadMask.Rows + num3 + num4, largeMap.RoadMask.Cols + num5 + num6, MatType.CV_8UC1, Scalar.Black);
		using (Mat m = new Mat(mat, new OpenCvSharp.Rect(num5, num3, largeMap.RoadMask.Cols, largeMap.RoadMask.Rows)))
		{
			largeMap.RoadMask.CopyTo(m);
		}
		OneDragon.Core.Abstractions.Geometry.Point offset = new OneDragon.Core.Abstractions.Geometry.Point(num5, num3);
		LargeMapIcon[] iconList = largeMap.IconList.Select((LargeMapIcon icon) => icon with
		{
			LargeMapPosition = icon.LargeMapPosition + offset,
			TeleportPosition = ((!icon.TeleportPosition.HasValue) ? ((OneDragon.Core.Abstractions.Geometry.Point?)null) : new OneDragon.Core.Abstractions.Geometry.Point?(icon.TeleportPosition.Value + offset))
		}).ToArray();
		return new LargeMapSnapshot(largeMap.AreaFullId, mat, iconList, largeMap.PositionAfterMerge + offset);
	}

	private static int CountNonZero(Mat source, OpenCvSharp.Rect area)
	{
		int num = Math.Clamp(area.X, 0, source.Cols);
		int num2 = Math.Clamp(area.Y, 0, source.Rows);
		int num3 = Math.Clamp(area.Width, 0, source.Cols - num);
		int num4 = Math.Clamp(area.Height, 0, source.Rows - num2);
		if (num3 == 0 || num4 == 0)
		{
			return 0;
		}
		using Mat mat = new Mat(source, new OpenCvSharp.Rect(num, num2, num3, num4));
		return Cv2.CountNonZero(mat);
	}

	private static ZzzWorldPatrolRecorderImageDto? EncodeRgbImage(Mat? rgb)
	{
		if (rgb == null)
		{
			return null;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(rgb, mat, ColorConversionCodes.BGR2RGB);
		return EncodeBgrImage(mat);
	}

	private static ZzzWorldPatrolRecorderImageDto EncodeBgrImage(Mat bgr)
	{
		Cv2.ImEncode(".png", bgr, out byte[] buf);
		return new ZzzWorldPatrolRecorderImageDto("image/png", buf);
	}

	private static Mat RenderMiniMapDisplay(TemplateLoader templateLoader, MiniMapSnapshot miniMap)
	{
		return LargeMapRecorderUtils.GetMiniMapDisplay(templateLoader, miniMap);
	}

	private static bool TryResolveWorldPatrolRouteVisual(WorldPatrolService service, string areaId, string transportPointName, out WorldPatrolLargeMap? largeMap, out WorldPatrolLargeMapIcon? transportPoint, out ZzzBackendErrorCode errorCode, out string error)
	{
		largeMap = null;
		transportPoint = null;
		WorldPatrolArea worldPatrolArea = service.AreaList.FirstOrDefault((WorldPatrolArea item) => string.Equals(item.FullId, areaId, StringComparison.Ordinal));
		if (worldPatrolArea == null)
		{
			errorCode = ZzzBackendErrorCode.NotFound;
			error = "区域不存在 " + areaId;
			return false;
		}
		largeMap = service.GetLargeMapByAreaFullId(worldPatrolArea.FullId);
		if (largeMap?.RoadMask == null)
		{
			errorCode = ZzzBackendErrorCode.NotReady;
			error = "当前区域缺少真实道路地图数据。";
			return false;
		}
		transportPoint = largeMap.IconList.FirstOrDefault((WorldPatrolLargeMapIcon icon) => string.Equals(icon.TemplateId, "map_icon_01", StringComparison.Ordinal) && string.Equals(icon.IconName, transportPointName, StringComparison.Ordinal));
		if (transportPoint == null)
		{
			errorCode = ZzzBackendErrorCode.Validation;
			error = "当前路线缺少真实传送点。";
			return false;
		}
		errorCode = ZzzBackendErrorCode.Unknown;
		error = string.Empty;
		return true;
	}

	private static Mat RenderWorldPatrolRouteMap(TemplateLoader templateLoader, WorldPatrolLargeMap largeMap, WorldPatrolLargeMapIcon transportPoint, IEnumerable<ZzzWorldPatrolOperationDto> operations)
	{
		Mat mat = new Mat();
		Cv2.CvtColor(largeMap.RoadMask, mat, ColorConversionCodes.GRAY2BGR);
		DrawMapIcons(templateLoader, mat, largeMap.IconList.Select((WorldPatrolLargeMapIcon icon) => (TemplateId: icon.TemplateId, new OneDragon.Core.Abstractions.Geometry.Point(icon.LargeMapPosition.X, icon.LargeMapPosition.Y))));
		OpenCvSharp.Point point = new OpenCvSharp.Point(transportPoint.LargeMapPosition.X, transportPoint.LargeMapPosition.Y);
		Cv2.Circle(mat, point, 20, new Scalar(0.0, 255.0, 255.0), 4);
		Cv2.Circle(mat, point, 15, new Scalar(255.0, 255.0, 0.0), 2);
		int num = 1;
		List<OpenCvSharp.Point> list = new List<OpenCvSharp.Point>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = point;
		List<OpenCvSharp.Point> list2 = list;
		foreach (ZzzWorldPatrolOperationDto operation in operations)
		{
			if (string.Equals(operation.OpType, "move", StringComparison.Ordinal) && operation.Data.Count >= 2 && int.TryParse(operation.Data[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && int.TryParse(operation.Data[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
			{
				list2.Add(new OpenCvSharp.Point(result, result2));
			}
		}
		for (int num2 = 1; num2 < list2.Count; num2++)
		{
			OpenCvSharp.Point center = list2[num2];
			Cv2.Circle(mat, center, 8, new Scalar(0.0, 255.0, 0.0), 2);
			Cv2.PutText(mat, num2.ToString(CultureInfo.InvariantCulture), new OpenCvSharp.Point(center.X - 5, center.Y - 10), HersheyFonts.HersheySimplex, 1.0, new Scalar(0.0, 0.0, 255.0), 2);
		}
		for (int num3 = 0; num3 + 1 < list2.Count; num3++)
		{
			Cv2.Line(mat, list2[num3], list2[num3 + 1], new Scalar(0.0, 255.0, 0.0), 2);
		}
		return mat;
	}

	private static Mat RenderLargeMapDisplay(TemplateLoader templateLoader, WorldPatrolLargeMapRecorderSession session)
	{
		LargeMapSnapshot largeMap = session.LargeMap;
		Mat largeMapDisplay = LargeMapRecorderUtils.GetLargeMapDisplay(templateLoader, largeMap);
		if (session.OverlapMode == 1 && session.PositionMatch != null && (object)session.MiniMap != null)
		{
			using Mat source = RenderMiniMapDisplay(templateLoader, session.MiniMap);
			using Mat mask = LargeMapRecorderUtils.GetMiniMapCircleMask(session.MiniMap.RoadMask.Rows);
			CopyImageWithMask(largeMapDisplay, source, mask, session.PositionMatch.X, session.PositionMatch.Y);
		}
		else if (session.CurrentPosition.HasValue)
		{
			Cv2.Circle(largeMapDisplay, new OpenCvSharp.Point(session.CurrentPosition.Value.X, session.CurrentPosition.Value.Y), 2, new Scalar(0.0, 0.0, 255.0), -1);
		}
		if (session.HighlightedIconIndex >= 0 && session.HighlightedIconIndex < largeMap.IconList.Count)
		{
			OneDragon.Core.Abstractions.Geometry.Point largeMapPosition = largeMap.IconList[session.HighlightedIconIndex].LargeMapPosition;
			Cv2.Circle(largeMapDisplay, new OpenCvSharp.Point(largeMapPosition.X, largeMapPosition.Y), 15, new Scalar(0.0, 0.0, 255.0), 3);
		}
		return largeMapDisplay;
	}

	private static void DrawMapIcons(TemplateLoader templateLoader, Mat destination, IEnumerable<(string TemplateId, OneDragon.Core.Abstractions.Geometry.Point Position)> icons)
	{
		foreach (var icon in icons)
		{
			string item = icon.TemplateId;
			OneDragon.Core.Abstractions.Geometry.Point item2 = icon.Position;
			TemplateInfo template = templateLoader.GetTemplate("map", item);
			if (template?.Raw == null)
			{
				continue;
			}
			Mat raw = template.Raw;
			using Mat mat = new Mat();
			Mat mat2 = raw;
			if (raw.Channels() == 4)
			{
				Cv2.CvtColor(raw, mat, ColorConversionCodes.BGRA2BGR);
				mat2 = mat;
			}
			else if (raw.Channels() == 1)
			{
				Cv2.CvtColor(raw, mat, ColorConversionCodes.GRAY2BGR);
				mat2 = mat;
			}
			int x = item2.X - mat2.Cols / 2;
			int y = item2.Y - mat2.Rows / 2;
			if (template.Mask == null)
			{
				using Mat mask = new Mat(mat2.Rows, mat2.Cols, MatType.CV_8UC1, Scalar.White);
				CopyImageWithMask(destination, mat2, mask, x, y);
			}
			else
			{
				CopyImageWithMask(destination, mat2, template.Mask, x, y);
			}
		}
	}

	private static void CopyImageWithMask(Mat destination, Mat source, Mat mask, int x, int y)
	{
		int num = Math.Max(0, x);
		int num2 = Math.Max(0, y);
		int num3 = Math.Max(0, -x);
		int num4 = Math.Max(0, -y);
		int val = Math.Min(source.Cols - num3, destination.Cols - num);
		int val2 = Math.Min(source.Rows - num4, destination.Rows - num2);
		val = Math.Min(val, mask.Cols - num3);
		val2 = Math.Min(val2, mask.Rows - num4);
		if (val <= 0 || val2 <= 0)
		{
			return;
		}
		OpenCvSharp.Rect roi = new OpenCvSharp.Rect(num3, num4, val, val2);
		OpenCvSharp.Rect roi2 = new OpenCvSharp.Rect(num, num2, val, val2);
		using Mat mat = new Mat(source, roi);
		using Mat mat2 = new Mat(mask, roi);
		using Mat m = new Mat(destination, roi2);
		mat.CopyTo(m, mat2);
	}

	private WorldPatrolService CreateWorldPatrolService()
	{
		return new WorldPatrolService(new OneDragonEnvironment(_runtime.RunRoot));
	}

	private ZzzWorldPatrolCatalogDto BuildWorldPatrolCatalog(int instanceIndex)
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
		WorldPatrolService service = new WorldPatrolService(environment);
		service.LoadData();
		IReadOnlyList<WorldPatrolRoute> worldPatrolRoutes = service.GetWorldPatrolRoutes();
		IReadOnlyList<string> autoBattleConfigs = (from item in new AutoBattleConfigProvider(environment).GetAutoBattleOpConfigList("auto_battle")
			select item.Value?.ToString() ?? item.Label).ToArray();
		WorldPatrolRunRecord record = WorldPatrolRunRecord.Load(environment, instanceIndex);
		return new ZzzWorldPatrolCatalogDto(service.EntryList.Select((WorldPatrolEntry entry) => new ZzzWorldPatrolEntryDto(entry.EntryId, entry.EntryName)).ToArray(), service.AreaList.Select((WorldPatrolArea area) => new ZzzWorldPatrolAreaDto(area.Entry.EntryId, area.FullId, area.FullName)).ToArray(), worldPatrolRoutes.Select((WorldPatrolRoute route) => new ZzzWorldPatrolRouteDto(route.FullId, route.TpArea.Entry.EntryId, route.TpArea.FullId, route.TpArea.FullName, route.Idx, route.TpName, route.OpList.Select((WorldPatrolRouteOperation operation) => new ZzzWorldPatrolOperationDto(operation.OpType, operation.Data.ToArray())).ToArray(), ToWorldPatrolRoutePositionDto(service.GetRouteLastPos(route)))).ToArray(), (from list in service.GetWorldPatrolRouteLists()
			select new ZzzWorldPatrolRouteListDto(list.Name, list.ListType, list.RouteItems.ToArray())).ToArray(), service.LargeMapList.SelectMany((WorldPatrolLargeMap map) => from icon in map.IconList
			where string.Equals(icon.TemplateId, "map_icon_01", StringComparison.Ordinal)
			select new ZzzWorldPatrolTransportPointDto(map.AreaFullId, icon.IconName, new ZzzWorldPatrolRoutePositionDto(icon.TransportPosition.X, icon.TransportPosition.Y))).ToArray(), autoBattleConfigs, ToWorldPatrolRunRecordDto(instanceIndex, record));
	}

	private static ZzzWorldPatrolRoutePositionDto? ToWorldPatrolRoutePositionDto(WorldPatrolPoint? position)
	{
		return (!position.HasValue) ? null : new ZzzWorldPatrolRoutePositionDto(position.Value.X, position.Value.Y);
	}

	private static bool TryBuildWorldPatrolRoute(ZzzSaveWorldPatrolRouteRequest request, out WorldPatrolRoute? route, out string error)
	{
		route = null;
		if (request.InstanceIndex < 0 || string.IsNullOrWhiteSpace(request.AreaId) || string.IsNullOrWhiteSpace(request.TransportPoint))
		{
			error = "路线实例、区域和传送点不能为空。";
			return false;
		}
		List<WorldPatrolRouteOperation> list = new List<WorldPatrolRouteOperation>();
		foreach (ZzzWorldPatrolOperationDto operation in request.Operations)
		{
			if (!string.Equals(operation.OpType, "move", StringComparison.Ordinal) || operation.Data.Count < 2 || !double.TryParse(operation.Data[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.TryParse(operation.Data[1], NumberStyles.Float, CultureInfo.InvariantCulture, out result))
			{
				error = "路线操作只支持包含两个数字坐标的 move。";
				return false;
			}
			list.Add(new WorldPatrolRouteOperation
			{
				OpType = "move",
				Data = operation.Data.Take(2).ToList()
			});
		}
		route = new WorldPatrolRoute
		{
			TpAreaId = request.AreaId,
			TpName = request.TransportPoint.Trim(),
			Idx = request.Index,
			OpList = list
		};
		error = string.Empty;
		return true;
	}

	private static ZzzWorldPatrolRunRecordDto ToWorldPatrolRunRecordDto(int instanceIndex, WorldPatrolRunRecord record)
	{
		return new ZzzWorldPatrolRunRecordDto(instanceIndex, record.Finished.ToArray(), record.CompletedRounds, record.RoutesPerRound);
	}

	private static bool TryValidateWorldPatrolRouteList(string name, string listType, out string error)
	{
		string text = name.Trim();
		if (string.IsNullOrWhiteSpace(text) || !string.Equals(Path.GetFileName(text), text, StringComparison.Ordinal) || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			error = "路线列表名称无效。";
			return false;
		}
		if ((!(listType == "whitelist") && !(listType == "blacklist")) || 1 == 0)
		{
			error = "路线列表类型无效。";
			return false;
		}
		error = string.Empty;
		return true;
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzBattleAssistantRuntimeDto> GetBattleAssistantRuntime()
	{
		try
		{
			ZContext zContext = _runtime.TryGetContext();
			if (zContext == null)
			{
				return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Fail(ZzzBackendErrorCode.NotReady, "运行上下文未初始化。");
			}
			_battleAssistantRuntimeSource.Attach(zContext);
			AutoBattleOperator autoOp = zContext.AutoBattleContext.AutoOp;
			if (autoOp == null)
			{
				return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Ok(EmptyBattleAssistantRuntime());
			}
			AutoBattleOperatorRuntimeSnapshot runtimeSnapshot = autoOp.GetRuntimeSnapshot();
			if (!runtimeSnapshot.IsRunning)
			{
				return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Ok(EmptyBattleAssistantRuntime());
			}
			DateTimeOffset now = DateTimeOffset.UtcNow;
			IReadOnlyDictionary<string, StateRecorderSnapshot> recorderSnapshots = zContext.AutoBattleContext.StateRecordService.GetSnapshot();
			ZzzBattleAssistantStateDto[] states = (from stateName in runtimeSnapshot.UsageStates
				select recorderSnapshots.TryGetValue(stateName, out StateRecorderSnapshot value) ? value : null into recorder
				where (object)recorder != null && recorder.LastRecordTime != -1.0
				where recorder.LastRecordTime != 0.0 || (!recorder.StateName.StartsWith("前台-", StringComparison.Ordinal) && !recorder.StateName.StartsWith("后台-", StringComparison.Ordinal))
				select new ZzzBattleAssistantStateDto(recorder.StateName, recorder.LastRecordTime, GetTriggerSeconds(now, recorder), recorder.LastValue, recorder.Revision)).ToArray();
			double? executionDurationSeconds = ((!runtimeSnapshot.ExecutionStartedAtUtc.HasValue) ? ((double?)null) : new double?(Math.Max(0.0, (now - runtimeSnapshot.ExecutionStartedAtUtc.Value).TotalSeconds)));
			return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Ok(new ZzzBattleAssistantRuntimeDto(IsRunning: true, runtimeSnapshot.TriggerDisplay, runtimeSnapshot.ExpressionDisplay, executionDurationSeconds, states));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLifeOnLineRunRecordDto> GetLifeOnLineRunRecord(int? instanceIndex = null)
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			int num = instanceIndex ?? _runtime.ActiveInstanceIndex;
			if (!(zContext.RunContext.GetRunRecord("life_on_line", num) is LifeOnLineRunRecord lifeOnLineRunRecord))
			{
				return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, "生命热线运行记录不可用。");
			}
			lifeOnLineRunRecord.CheckAndUpdateStatus();
			return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Ok(new ZzzLifeOnLineRunRecordDto(num, lifeOnLineRunRecord.DailyRunTimes));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidSettingsCatalogDto> GetLostVoidSettingsCatalog(int instanceIndex)
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			LostVoidRunRecord lostVoidRunRecord = GetLostVoidRunRecord(zContext, instanceIndex);
			lostVoidRunRecord.CheckAndUpdateStatus();
			return ZzzBackendResult<ZzzLostVoidSettingsCatalogDto>.Ok(new ZzzLostVoidSettingsCatalogDto(zContext.CompendiumService.GetLostVoidMissionNameList(), LostVoidChallengeConfig.GetAllModuleNames(zContext.Environment), ToLostVoidRunRecordDto(instanceIndex, lostVoidRunRecord)));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidSettingsCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidRunRecordDto> ResetLostVoidRunRecord(int instanceIndex)
	{
		try
		{
			LostVoidRunRecord lostVoidRunRecord = GetLostVoidRunRecord(_runtime.EnsureContext(), instanceIndex);
			lostVoidRunRecord.ResetRecord();
			lostVoidRunRecord.ResetForWeekly();
			return ZzzBackendResult<ZzzLostVoidRunRecordDto>.Ok(ToLostVoidRunRecordDto(instanceIndex, lostVoidRunRecord));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidRunRecordDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidChallengeCatalogDto> GetLostVoidChallengeCatalog(int instanceIndex)
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			zContext.LostVoid.LoadInvestigationStrategy();
			AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(_runtime.RunRoot));
			ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = _configScopes.Read("team", instanceIndex, "default");
			if (!zzzBackendResult.Success || (object)zzzBackendResult.Value == null || !zzzBackendResult.Value.Values.TryGetValue("team_list", out object value) || !(value is List<PredefinedTeamInfo> source))
			{
				throw new InvalidOperationException(zzzBackendResult.Error ?? "预备编队配置缺少 team_list。");
			}
			OneDragonEnvironment environment = zContext.Environment;
			return ZzzBackendResult<ZzzLostVoidChallengeCatalogDto>.Ok(new ZzzLostVoidChallengeCatalogDto((from name in LostVoidChallengeConfig.GetAllModuleNames(environment)
				select new ZzzLostVoidChallengeSummaryDto(name, LostVoidChallengeConfig.IsSample(environment, name))).ToArray(), source.Select((PredefinedTeamInfo team) => new ZzzLostVoidTeamDto(team.Idx, team.Name)).ToArray(), (from item in autoBattleConfigProvider.GetAutoBattleOpConfigList("auto_battle")
				select Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? item.Label).ToArray(), new ZzzLostVoidOptionDto[1]
			{
				new ZzzLostVoidOptionDto("代理人", "unknown")
			}.Concat(AgentEnum.Values.Select((AgentEnum agent) => new ZzzLostVoidOptionDto(agent.Value.AgentName, agent.Value.AgentId))).ToArray(), (from strategy in zContext.LostVoid.InvestigationStrategyList
				select strategy.StrategyName into name
				where !string.IsNullOrWhiteSpace(name)
				select name).ToArray()));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeCatalogDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> GetLostVoidChallengeConfig(string moduleName)
	{
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			LostVoidChallengeConfig config = LostVoidChallengeConfig.Load(environment, moduleName);
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(ToLostVoidChallengeConfigDto(moduleName, LostVoidChallengeConfig.IsSample(environment, moduleName), exists: true, config));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CreateLostVoidChallengeConfigDraft()
	{
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			string newModuleName = LostVoidChallengeConfig.GetNewModuleName(environment);
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(ToLostVoidChallengeConfigDto(newModuleName, isSample: false, exists: false, new LostVoidChallengeConfig()));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CopyLostVoidChallengeConfigDraft(string moduleName)
	{
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			LostVoidChallengeConfig config = LostVoidChallengeConfig.Load(environment, moduleName);
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(ToLostVoidChallengeConfigDto(moduleName + "_copy", isSample: false, exists: false, config));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidChallengeConfigDto> SaveLostVoidChallengeConfig(ZzzSaveLostVoidChallengeConfigRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			ZzzLostVoidChallengeConfigDto config = request.Config;
			LostVoidChallengeConfig config2 = FromLostVoidChallengeConfigDto(config);
			LostVoidChallengeConfig.Save(environment, config.ModuleName, config2);
			if (!string.IsNullOrWhiteSpace(request.OriginalModuleName) && !string.Equals(request.OriginalModuleName, config.ModuleName, StringComparison.Ordinal) && !LostVoidChallengeConfig.IsSample(environment, request.OriginalModuleName))
			{
				LostVoidChallengeConfig.Delete(environment, request.OriginalModuleName);
			}
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Ok(ToLostVoidChallengeConfigDto(config.ModuleName, isSample: false, exists: true, config2));
		}
		catch (ArgumentException ex)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Fail(ZzzBackendErrorCode.Validation, ex.Message);
		}
		catch (Exception ex2)
		{
			return ZzzBackendResult<ZzzLostVoidChallengeConfigDto>.Fail(ZzzBackendErrorCode.NotReady, ex2.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<bool> DeleteLostVoidChallengeConfig(string moduleName)
	{
		try
		{
			OneDragonEnvironment environment = new OneDragonEnvironment(_runtime.RunRoot);
			if (LostVoidChallengeConfig.IsSample(environment, moduleName))
			{
				return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.Validation, "默认配置不能删除。");
			}
			return LostVoidChallengeConfig.Delete(environment, moduleName) ? ZzzBackendResult<bool>.Ok(value: true) : ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.NotFound, "挑战配置不存在。");
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<bool>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzLostVoidPriorityParseDto> ParseLostVoidPriority(ZzzLostVoidPriorityKind kind, string text)
	{
		try
		{
			ZContext zContext = _runtime.EnsureContext();
			List<string> items;
			string errorMessage;
			if (kind == ZzzLostVoidPriorityKind.RegionTypePriority)
			{
				(items, errorMessage) = zContext.LostVoid.CheckRegionTypePriorityInput(text);
			}
			else
			{
				(items, errorMessage) = zContext.LostVoid.CheckArtifactPriorityInput(text);
			}
			return ZzzBackendResult<ZzzLostVoidPriorityParseDto>.Ok(new ZzzLostVoidPriorityParseDto(items, errorMessage));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzLostVoidPriorityParseDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	private static LostVoidRunRecord GetLostVoidRunRecord(ZContext context, int instanceIndex)
	{
		return (context.RunContext.GetRunRecord("lost_void", instanceIndex) as LostVoidRunRecord) ?? throw new InvalidOperationException("迷失之地运行记录不可用。");
	}

	private static ZzzLostVoidRunRecordDto ToLostVoidRunRecordDto(int instanceIndex, LostVoidRunRecord record)
	{
		return new ZzzLostVoidRunRecordDto(instanceIndex, record.DailyRunTimes, record.WeeklyRunTimes, record.BountyCommissionComplete, record.EvalPointComplete, record.PeriodRewardComplete);
	}

	private static ZzzLostVoidChallengeConfigDto ToLostVoidChallengeConfigDto(string moduleName, bool isSample, bool exists, LostVoidChallengeConfig config)
	{
		return new ZzzLostVoidChallengeConfigDto(moduleName, isSample, exists, config.PredefinedTeamIdx, config.ChooseTeamByPriority, config.ManuallyChooseAgent, config.TeamInfo.ToArray(), config.AutoBattle, config.ChaseNewMode, config.InvestigationStrategy, config.PeriodBuffNo, config.StoreGold, config.StoreBlood, config.StoreBloodMin, config.ArtifactPriorityNew, config.BuyOnlyPriority1, config.BuyOnlyPriority2, config.ArtifactPriority.ToArray(), config.ArtifactPriority2.ToArray(), config.RegionTypePriority.ToArray());
	}

	private static LostVoidChallengeConfig FromLostVoidChallengeConfigDto(ZzzLostVoidChallengeConfigDto dto)
	{
		return new LostVoidChallengeConfig
		{
			PredefinedTeamIdx = dto.PredefinedTeamIndex,
			ChooseTeamByPriority = dto.ChooseTeamByPriority,
			ManuallyChooseAgent = dto.ManuallyChooseAgent,
			TeamInfo = dto.TeamInfo.ToList(),
			AutoBattle = dto.AutoBattle,
			ChaseNewMode = dto.ChaseNewMode,
			InvestigationStrategy = dto.InvestigationStrategy,
			PeriodBuffNo = dto.PeriodBuffNo,
			StoreGold = dto.StoreGold,
			StoreBlood = dto.StoreBlood,
			StoreBloodMin = dto.StoreBloodMin,
			ArtifactPriorityNew = dto.ArtifactPriorityNew,
			BuyOnlyPriority1 = dto.BuyOnlyPriority1,
			BuyOnlyPriority2 = dto.BuyOnlyPriority2,
			ArtifactPriority = dto.ArtifactPriority.ToList(),
			ArtifactPriority2 = dto.ArtifactPriority2.ToList(),
			RegionTypePriority = dto.RegionTypePriority.ToList()
		};
	}

	/// <inheritdoc />
	public void SubscribeBattleAssistantOperationLoaded(Action callback)
	{
		ArgumentNullException.ThrowIfNull(callback, "callback");
		ZContext zContext = _runtime.TryGetContext();
		if (zContext != null)
		{
			_battleAssistantRuntimeSource.Attach(zContext);
		}
		_battleAssistantRuntimeSource.OperationLoaded += callback;
	}

	/// <inheritdoc />
	public void UnsubscribeBattleAssistantOperationLoaded(Action callback)
	{
		ArgumentNullException.ThrowIfNull(callback, "callback");
		_battleAssistantRuntimeSource.OperationLoaded -= callback;
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>> GetRecentLogs(int limit = 200)
	{
		return ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>>.Ok(_logProvider.GetRecent(limit));
	}

	private static ZzzBattleAssistantConfigCatalogDto CreateBattleAssistantCatalog(AutoBattleConfigProvider provider)
	{
		return new ZzzBattleAssistantConfigCatalogDto((from item in provider.GetAutoBattleOpConfigList("auto_battle")
			select Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? item.Label).ToArray(), (from item in provider.GetAutoBattleOpConfigList("dodge")
			select Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? item.Label).ToArray());
	}

	/// <inheritdoc />
	public ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>> GetConfigScopes()
	{
		return ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>>.Ok(_configScopes.GetDescriptors());
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex = null, string? groupId = null)
	{
		return _configScopes.Read(scope, instanceIndex ?? _runtime.ActiveInstanceIndex, groupId);
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfigScope(ZzzSaveConfigScopeRequest request)
	{
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = _configScopes.Save(request with
		{
			InstanceIndex = (request.InstanceIndex ?? _runtime.ActiveInstanceIndex)
		});
		if (zzzBackendResult.Success)
		{
			ApplyConfigRuntimeEffects(request, zzzBackendResult.Value);
			_eventBus.Publish("config.changed", zzzBackendResult.Value);
		}
		return zzzBackendResult;
	}

	private void ApplyConfigRuntimeEffects(ZzzSaveConfigScopeRequest request, ZzzConfigScopeValuesDto saved)
	{
		ZContext zContext = _runtime.TryGetContext();
		if (zContext != null && (!saved.Descriptor.InstanceBound || saved.InstanceIndex == zContext.InstanceIndex))
		{
			zContext.ReloadInstanceConfig();
			if (string.Equals(request.Scope, "model", StringComparison.Ordinal) && request.Values.ContainsKey("ocr_use_gpu"))
			{
				zContext.OcrService.Matcher.UpdateUseGpu(zContext.ModelConfig.OcrUseGpu);
			}
			bool flag = string.Equals(request.Scope, "instance", StringComparison.Ordinal) && request.Values.Keys.Any((string key) => string.Equals(key, "game_region", StringComparison.Ordinal) || string.Equals(key, "use_custom_win_title", StringComparison.Ordinal) || string.Equals(key, "custom_win_title", StringComparison.Ordinal));
			bool flag2 = string.Equals(request.Scope, "env", StringComparison.Ordinal) && request.Values.ContainsKey("screenshot_method");
			if (flag || flag2)
			{
				zContext.InitController();
			}
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzWindowStatusDto> GetWindow()
	{
		try
		{
			WindowStatus windowStatus = _runtime.EnsureContext().Backend.CheckWindow();
			ZzzWindowStatusDto zzzWindowStatusDto = new ZzzWindowStatusDto(windowStatus.WinTitle, windowStatus.IsWinValid, windowStatus.IsWinActive, windowStatus.IsWinScale);
			PublishWindowChangedIfNeeded(zzzWindowStatusDto);
			return ZzzBackendResult<ZzzWindowStatusDto>.Ok(zzzWindowStatusDto);
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzWindowStatusDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzScreenshotDto> GetScreenshot()
	{
		try
		{
			using Mat mat = _runtime.EnsureContext().Backend.Capture();
			Cv2.ImEncode(".png", mat, out byte[] buf);
			return ZzzBackendResult<ZzzScreenshotDto>.Ok(new ZzzScreenshotDto("image/png", buf));
		}
		catch (Exception ex)
		{
			return ZzzBackendResult<ZzzScreenshotDto>.Fail(ZzzBackendErrorCode.NotReady, ex.Message);
		}
	}

	/// <inheritdoc />
	public Task<ZzzBackendResult<ZzzRunStatusDto>> StartRunAsync(ZzzStartRunRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		if (string.IsNullOrWhiteSpace(request.AppId))
		{
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotFound, "缺少 appId。"));
		}
		try
		{
			using (_lock.EnterScope())
			{
				ZzzRunStatusDto currentRunCore = GetCurrentRunCore();
				ZzzRunState state = currentRunCore.State;
				if ((uint)(state - 1) <= 3u)
				{
					return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.Conflict, "已有运行中的应用。"));
				}
				ZContext zContext = _runtime.EnsureContext();
				_battleAssistantRuntimeSource.Attach(zContext);
				if (!zContext.RunContext.IsAppRegistered(request.AppId))
				{
					return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotFound, "应用未注册 " + request.AppId));
				}
				int num = request.InstanceIndex ?? _runtime.ActiveInstanceIndex;
				string text = (string.IsNullOrWhiteSpace(request.GroupId) ? "default" : request.GroupId);
				string applicationName = zContext.RunContext.GetApplicationName(request.AppId);
				_currentAppId = request.AppId;
				_currentAppName = applicationName;
				_currentInstanceIndex = num;
				_currentGroupId = text;
				_startedAt = DateTimeOffset.UtcNow;
				_finishedAt = null;
				_terminalState = ZzzRunState.Starting;
				_lastStatus = null;
				_lastError = null;
				_currentTask = zContext.RunContext.RunApplicationAsync(request.AppId, num, text);
				if (_currentTask.IsFaulted)
				{
					return Task.FromResult(CompleteStartFailure((Exception)(((object)_currentTask.Exception) ?? ((object)new InvalidOperationException("应用启动失败。")))));
				}
				ObserveRunAsync(_currentTask);
				ZzzRunStatusDto currentRunCore2 = GetCurrentRunCore();
				PublishRunEvents(currentRunCore2);
				_logger.LogInformation("启动应用 {AppId} 实例 {InstanceIndex}", request.AppId, num);
				return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(currentRunCore2));
			}
		}
		catch (Exception exception)
		{
			return Task.FromResult(CompleteStartFailure(request, exception));
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzRunStatusDto> PauseRun()
	{
		using (_lock.EnterScope())
		{
			ZContext zContext = _runtime.TryGetContext();
			if (zContext == null)
			{
				return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "运行上下文未初始化。");
			}
			if (!zContext.RunContext.IsContextRunning)
			{
				return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.Conflict, "当前没有运行中的应用。");
			}
			zContext.RunContext.SwitchContextPauseAndRun();
			ZzzRunStatusDto currentRunCore = GetCurrentRunCore();
			PublishRunEvents(currentRunCore);
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(currentRunCore);
		}
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzRunStatusDto> ResumeRun()
	{
		using (_lock.EnterScope())
		{
			ZContext zContext = _runtime.TryGetContext();
			if (zContext == null)
			{
				return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "运行上下文未初始化。");
			}
			if (!zContext.RunContext.IsContextPause)
			{
				return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.Conflict, "当前没有暂停中的应用。");
			}
			zContext.RunContext.SwitchContextPauseAndRun();
			ZzzRunStatusDto currentRunCore = GetCurrentRunCore();
			PublishRunEvents(currentRunCore);
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(currentRunCore);
		}
	}

	/// <inheritdoc />
	public async Task<ZzzBackendResult<ZzzRunStatusDto>> StopRunAsync()
	{
		ZContext context = _runtime.TryGetContext();
		if (context == null)
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "运行上下文未初始化。");
		}
		if (context.RunContext.IsContextStop)
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.Conflict, "当前没有运行中的应用。");
		}
		using (_lock.EnterScope())
		{
			_terminalState = ZzzRunState.Stopping;
			PublishRunEvents(GetCurrentRunCore());
		}
		await context.RunContext.StopRunningAsync(TimeSpan.FromSeconds(3L)).ConfigureAwait(continueOnCapturedContext: false);
		ZzzRunStatusDto status;
		using (_lock.EnterScope())
		{
			_terminalState = ZzzRunState.Cancelled;
			_finishedAt = DateTimeOffset.UtcNow;
			_lastStatus = "人工结束";
			status = GetCurrentRunCore();
		}
		PublishRunEvents(status);
		return ZzzBackendResult<ZzzRunStatusDto>.Ok(status);
	}

	/// <inheritdoc />
	public ZzzBackendResult<ZzzRunStatusDto> GetCurrentRun()
	{
		using (_lock.EnterScope())
		{
			if (_runtime.TryGetContext() == null)
			{
				return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "运行上下文未初始化。");
			}
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(GetCurrentRunCore());
		}
	}

	/// <inheritdoc />
	public ChannelReader<ZzzBackendEvent> SubscribeEvents()
	{
		return _eventBus.Subscribe();
	}

	/// <inheritdoc />
	public void UnsubscribeEvents(ChannelReader<ZzzBackendEvent> reader)
	{
		_eventBus.Unsubscribe(reader);
	}

	private async Task ObserveRunAsync(Task<OperationResult> task)
	{
		try
		{
			OperationResult result = await task.ConfigureAwait(continueOnCapturedContext: false);
			ZzzRunStatusDto status;
			using (_lock.EnterScope())
			{
				_terminalState = (result.IsSuccess ? ZzzRunState.Succeeded : ZzzRunState.Failed);
				_lastStatus = result.Status;
				_finishedAt = DateTimeOffset.UtcNow;
				status = GetCurrentRunCore();
			}
			PublishRunEvents(status);
		}
		catch (OperationCanceledException)
		{
			ZzzRunStatusDto status2;
			using (_lock.EnterScope())
			{
				_terminalState = ZzzRunState.Cancelled;
				_lastStatus = "人工结束";
				_finishedAt = DateTimeOffset.UtcNow;
				status2 = GetCurrentRunCore();
			}
			PublishRunEvents(status2);
		}
		catch (Exception ex2)
		{
			Exception exception = ex2;
			ZzzRunStatusDto status3;
			using (_lock.EnterScope())
			{
				_terminalState = ZzzRunState.Failed;
				_lastStatus = "执行异常";
				_lastError = exception.Message;
				_finishedAt = DateTimeOffset.UtcNow;
				status3 = GetCurrentRunCore();
			}
			_eventBus.Publish("error.raised", new { exception.Message });
			PublishRunEvents(status3);
		}
	}

	private ZzzBackendResult<ZzzRunStatusDto> CompleteStartFailure(ZzzStartRunRequest request, Exception exception)
	{
		using (_lock.EnterScope())
		{
			_currentAppId = request.AppId;
			_currentAppName = null;
			_currentInstanceIndex = request.InstanceIndex ?? _runtime.ActiveInstanceIndex;
			_currentGroupId = (string.IsNullOrWhiteSpace(request.GroupId) ? "default" : request.GroupId);
			_startedAt = DateTimeOffset.UtcNow;
			return CompleteStartFailureCore(exception);
		}
	}

	private ZzzBackendResult<ZzzRunStatusDto> CompleteStartFailure(Exception exception)
	{
		using (_lock.EnterScope())
		{
			return CompleteStartFailureCore(exception);
		}
	}

	private ZzzBackendResult<ZzzRunStatusDto> CompleteStartFailureCore(Exception exception)
	{
		string message = exception.GetBaseException().Message;
		_currentTask = null;
		_terminalState = ZzzRunState.Failed;
		_lastStatus = "启动异常";
		_lastError = message;
		_finishedAt = DateTimeOffset.UtcNow;
		ZzzRunStatusDto currentRunCore = GetCurrentRunCore();
		_eventBus.Publish("error.raised", new
		{
			Error = message,
			AppId = _currentAppId
		});
		PublishRunEvents(currentRunCore);
		_logger.LogError(exception, "启动应用 {AppId} 时发生异常", _currentAppId);
		return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, message);
	}

	private void PublishRunEvents(ZzzRunStatusDto status)
	{
		_eventBus.Publish("run.stateChanged", status);
		_eventBus.Publish("run.progress", status);
	}

	private static ZzzBattleAssistantRuntimeDto EmptyBattleAssistantRuntime()
	{
		return new ZzzBattleAssistantRuntimeDto(IsRunning: false, null, null, null, Array.Empty<ZzzBattleAssistantStateDto>());
	}

	private static double GetTriggerSeconds(DateTimeOffset now, StateRecorderSnapshot recorder)
	{
		if (recorder.LastRecordTime == 0.0 || !recorder.LastRecordTimestampUtc.HasValue)
		{
			return 999.0;
		}
		return Math.Clamp((now - recorder.LastRecordTimestampUtc.Value).TotalSeconds, 0.0, 999.0);
	}

	private void PublishInstanceChanged(ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> result)
	{
		if (result.Success && result.Value != null)
		{
			ZzzInstanceDto zzzInstanceDto = result.Value.FirstOrDefault((ZzzInstanceDto instance) => instance.Active);
			if ((object)zzzInstanceDto != null)
			{
				_eventBus.Publish("instance.activeChanged", zzzInstanceDto);
			}
			_eventBus.Publish("instance.changed", result.Value);
		}
	}

	private void PublishWindowChangedIfNeeded(ZzzWindowStatusDto status)
	{
		if (!(status == _lastWindowStatus))
		{
			_lastWindowStatus = status;
			_eventBus.Publish("window.changed", status);
		}
	}

	private ZzzRunStatusDto GetCurrentRunCore()
	{
		ZzzRunState zzzRunState = ZzzRunStateMapper.Map(_runtime.TryGetContext()?.RunContext.State, _terminalState);
		if (_currentTask == null && zzzRunState == ZzzRunState.Idle)
		{
			return new ZzzRunStatusDto(ZzzRunState.Idle);
		}
		DateTimeOffset? finishedAt = _finishedAt;
		DateTimeOffset? dateTimeOffset;
		if (finishedAt.HasValue)
		{
			dateTimeOffset = finishedAt;
		}
		else
		{
			bool flag = (uint)(zzzRunState - 1) <= 3u;
			dateTimeOffset = (flag ? new DateTimeOffset?(DateTimeOffset.UtcNow) : ((DateTimeOffset?)null));
		}
		DateTimeOffset? endedAt = dateTimeOffset;
		return new ZzzRunStatusDto(zzzRunState, _currentAppId, _currentAppName, _currentInstanceIndex, _currentGroupId, FormatTimestamp(_startedAt), FormatTimestamp(_finishedAt), GetDurationSeconds(_startedAt, endedAt), _lastStatus, _lastError);
	}

	private static string? FormatTimestamp(DateTimeOffset? timestamp)
	{
		return timestamp?.ToString("O", CultureInfo.InvariantCulture);
	}

	private static double? GetDurationSeconds(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
	{
		if (!startedAt.HasValue || !endedAt.HasValue)
		{
			return null;
		}
		return Math.Round((endedAt.Value - startedAt.Value).TotalSeconds, 3);
	}

	private static ZzzBackendResult<IReadOnlyList<ZzzAppDto>> FailApps(Exception exception)
	{
		return ZzzBackendResult<IReadOnlyList<ZzzAppDto>>.Fail(ZzzBackendErrorCode.NotReady, exception.Message);
	}
}
