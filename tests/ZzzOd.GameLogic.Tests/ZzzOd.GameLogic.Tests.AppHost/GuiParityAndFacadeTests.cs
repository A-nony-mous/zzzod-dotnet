using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Devtools;
using ZzzOd.AppHost.Overlay;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Config;
using ZzzOd.Gui;
using ZzzOd.Gui.Controls;
using ZzzOd.Gui.PageModels.Accounts;
using ZzzOd.Gui.Views.FrontierPages.Accounts;
using ZzzOd.Gui.PageModels.Devtools;
using ZzzOd.Gui.Views.FrontierPages.GameAssistant;
using ZzzOd.Gui.PageModels.OneDragon;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Services.Config;
using ZzzOd.Gui.Services.Dialogs;
using ZzzOd.Gui.Services.Home;
using ZzzOd.Gui.Services.LauncherMedia;
using ZzzOd.Gui.Services.Notices;
using ZzzOd.Gui.Services.RunIntent;
using ZzzOd.Gui.Shell;
using ZzzOd.Gui.Views.FrontierPages.OneDragon;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// Avalonia parity 和共享业务门面测试。
/// </summary>
public sealed class GuiParityAndFacadeTests
{
	private sealed class AvaloniaTestThread
	{
		private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

		private readonly ManualResetEventSlim _ready = new ManualResetEventSlim();

		private readonly Thread _thread;

		private Exception? _startupException;

		private int _threadId;

		public AvaloniaTestThread()
		{
			_thread = new Thread(Run)
			{
				IsBackground = true,
				Name = "ZzzOd Avalonia Test Thread"
			};
			_thread.SetApartmentState(ApartmentState.STA);
			_thread.Start();
		}

		public void EnsureStarted()
		{
			_ready.Wait();
			if (_startupException != null)
			{
				ExceptionDispatchInfo.Capture(_startupException).Throw();
			}
		}

		public void Invoke(Action action)
		{
			EnsureStarted();
			if (Environment.CurrentManagedThreadId == _threadId)
			{
				action();
				return;
			}
			Exception exception = null;
			ManualResetEventSlim done = new ManualResetEventSlim();
			try
			{
				_queue.Add(delegate
				{
					try
					{
						action();
					}
					catch (Exception ex)
					{
						exception = ex;
					}
					finally
					{
						done.Set();
					}
				});
				done.Wait();
				if (exception != null)
				{
					ExceptionDispatchInfo.Capture(exception).Throw();
				}
			}
			finally
			{
				if (done != null)
				{
					((IDisposable)done).Dispose();
				}
			}
		}

		private void Run()
		{
			_threadId = Environment.CurrentManagedThreadId;
			try
			{
				AppBuilder.Configure<Avalonia.Application>().UsePlatformDetect().SetupWithoutStarting();
			}
			catch (Exception startupException)
			{
				_startupException = startupException;
			}
			finally
			{
				_ready.Set();
			}
			if (_startupException != null)
			{
				return;
			}
			foreach (Action item in _queue.GetConsumingEnumerable())
			{
				item();
			}
		}
	}

	private sealed class LifecycleControl : Control, IZzzPageLifecycle
	{
		public int Left { get; private set; }

		public int Shown { get; private set; }

		public int Hidden { get; private set; }

		public int Disposed { get; private set; }

		public void OnPageLeave()
		{
			Left++;
		}

		public void OnPageShown()
		{
			Shown++;
		}

		public void OnPageHidden()
		{
			Hidden++;
		}

		public void DisposePage()
		{
			Disposed++;
		}
	}

	private sealed class BackendHarness : IDisposable
	{
		public string RunRoot { get; }

		public ZzzRuntimeManager Runtime { get; }

		public ZzzBattleAssistantRuntimeSource BattleAssistantRuntimeSource { get; }

		public ZzzLogFanOutLoggerProvider LogProvider { get; }

		public ZzzAppBackend Backend { get; }

		private BackendHarness(string runRoot, ZzzRuntimeManager runtime, ZzzBattleAssistantRuntimeSource battleAssistantRuntimeSource, ZzzLogFanOutLoggerProvider logProvider, ZzzAppBackend backend)
		{
			RunRoot = runRoot;
			Runtime = runtime;
			BattleAssistantRuntimeSource = battleAssistantRuntimeSource;
			LogProvider = logProvider;
			Backend = backend;
		}

		public static BackendHarness Create()
		{
			string text = Path.Combine(Path.GetTempPath(), "zzzod-gui-parity-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(text, "config", "00"));
			File.WriteAllText(Path.Combine(text, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
			ZzzRuntimeManager runtime = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			ZzzBattleAssistantRuntimeSource battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			ZzzLogFanOutLoggerProvider logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(text), eventBus);
			ZzzAppBackend backend = new ZzzAppBackend(runtime, eventBus, battleAssistantRuntimeSource, logProvider, new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
			return new BackendHarness(text, runtime, battleAssistantRuntimeSource, logProvider, backend);
		}

		public void Dispose()
		{
			Runtime.Dispose();
			BattleAssistantRuntimeSource.Dispose();
			LogProvider.Dispose();
			if (Directory.Exists(RunRoot))
			{
				Directory.Delete(RunRoot, recursive: true);
			}
		}
	}

	private sealed class ImmediateUiDispatcher : IZzzUiDispatcher
	{
		public void Post(Action action)
		{
			action();
		}
	}

	private sealed class FakeOverlayService : IZzzOverlayService
	{
		private bool _enabled;

		public ZzzOverlayStatusDto GetStatus()
		{
			return new ZzzOverlayStatusDto(_enabled, null, 0);
		}

		public void SetEnabled(bool enabled)
		{
			_enabled = enabled;
		}

		public ZzzOverlayFrameDto? GetLastFrame()
		{
			return null;
		}

		public void SubmitPerformanceSample(ZzzOverlayPerformanceSampleDto sample)
		{
		}

		public IReadOnlyList<ZzzOverlayPerformanceSampleDto> GetPerformanceSamples()
		{
			return Array.Empty<ZzzOverlayPerformanceSampleDto>();
		}
	}

	private sealed class FakeBackend : IZzzAppBackend
	{
		private readonly ZzzBackendEventBus _eventBus = new ZzzBackendEventBus();

		private readonly bool _contextReady;

		private readonly bool _hasOneDragonApp;

		private readonly bool _windowValid;

		private readonly string _runRoot;

		private readonly IReadOnlyList<ZzzAppDto>? _apps;

		private List<ZzzInstanceDto> _instances;

		private readonly Dictionary<string, Dictionary<string, object?>> _configScopes = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);

		private ZzzRunStatusDto _run = new ZzzRunStatusDto(ZzzRunState.Idle);

		public ZzzStartRunRequest? LastStartRequest { get; private set; }

		public ZzzBattleAssistantConfigCatalogDto BattleAssistantCatalog { get; set; } = new ZzzBattleAssistantConfigCatalogDto(Array.Empty<string>(), Array.Empty<string>());

		public ZzzBattleAssistantRuntimeDto BattleAssistantRuntime { get; set; } = new ZzzBattleAssistantRuntimeDto(IsRunning: false, null, null, null, Array.Empty<ZzzBattleAssistantStateDto>());

		public ZzzChargePlanCatalogDto ChargePlanCatalog { get; set; } = new ZzzChargePlanCatalogDto(Array.Empty<ZzzChargePlanCategoryDto>(), Array.Empty<ZzzChargePlanTeamDto>(), Array.Empty<string>());

		public int BattleAssistantOperationLoadedSubscriberCount { get; private set; }

		public int EventSubscriberCount { get; private set; }

		private event Action? BattleAssistantOperationLoaded;

		public FakeBackend(bool contextReady = true, bool hasOneDragonApp = true, bool windowValid = true, string? runRoot = null, IReadOnlyList<ZzzAppDto>? apps = null, IReadOnlyList<ZzzInstanceDto>? instances = null, ZzzRunStatusDto? run = null)
		{
			_contextReady = contextReady;
			_hasOneDragonApp = hasOneDragonApp;
			_windowValid = windowValid;
			_runRoot = runRoot ?? Path.Combine(Path.GetTempPath(), "zzzod-fake-backend", Guid.NewGuid().ToString("N"));
			_apps = apps;
			object obj = instances?.ToList();
			if (obj == null)
			{
				int num = 1;
				obj = new List<ZzzInstanceDto>(num);
				CollectionsMarshal.SetCount((List<ZzzInstanceDto>)obj, num);
				CollectionsMarshal.AsSpan((List<ZzzInstanceDto>?)obj)[0] = new ZzzInstanceDto(0, "00", Active: true, "config/00");
			}
			_instances = (List<ZzzInstanceDto>)obj;
			_run = run ?? new ZzzRunStatusDto(ZzzRunState.Idle);
		}

		public ZzzBackendResult<ZzzHealthDto> GetHealth()
		{
			return ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(ZzzHostMode.Gui, "test", _runRoot, ApiEnabled: true, _contextReady, CurrentInstanceIndex()));
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> GetInstances()
		{
			return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(_instances);
		}

		public ZzzBackendResult<ZzzInstanceDto> GetCurrentInstance()
		{
			ZzzInstanceDto zzzInstanceDto = _instances.FirstOrDefault((ZzzInstanceDto instance) => instance.Active);
			return ((object)zzzInstanceDto != null) ? ZzzBackendResult<ZzzInstanceDto>.Ok(zzzInstanceDto) : ZzzBackendResult<ZzzInstanceDto>.Fail(ZzzBackendErrorCode.NotReady, "当前实例不可用。");
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int instanceIndex)
		{
			if (IsRunActive(_run.State))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能切换实例。");
			}
			if (_instances.All((ZzzInstanceDto instance) => instance.Index != instanceIndex))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {instanceIndex:00}");
			}
			_instances = _instances.Select((ZzzInstanceDto instance) => instance with
			{
				Active = (instance.Index == instanceIndex)
			}).ToList();
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> CreateInstance()
		{
			if (IsRunActive(_run.State))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能切换实例。");
			}
			int index;
			for (index = 0; _instances.Any((ZzzInstanceDto instance) => instance.Index == index); index++)
			{
			}
			_instances.Add(new ZzzInstanceDto(index, index.ToString("00"), Active: false, $"config/{index:00}"));
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request)
		{
			if (IsRunActive(_run.State))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能切换实例。");
			}
			int num = _instances.FindIndex((ZzzInstanceDto instance) => instance.Index == request.Index);
			if (num < 0)
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.NotFound, $"实例不存在 {request.Index:00}");
			}
			ZzzInstanceDto zzzInstanceDto = _instances[num];
			_instances[num] = zzzInstanceDto with
			{
				Name = (request.Name ?? zzzInstanceDto.Name),
				ActiveInOneDragon = (request.ActiveInOneDragon ?? zzzInstanceDto.ActiveInOneDragon)
			};
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int instanceIndex)
		{
			if (IsRunActive(_run.State))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "运行中不能切换实例。");
			}
			if (_instances.Count <= 1 || _instances.Any((ZzzInstanceDto instance) => instance.Index == instanceIndex && instance.Active))
			{
				return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Fail(ZzzBackendErrorCode.Conflict, "当前实例不能删除。");
			}
			_instances.RemoveAll((ZzzInstanceDto instance) => instance.Index == instanceIndex);
			return GetInstances();
		}

		public ZzzBackendResult<ZzzRunStatusDto> LoginInstance(int instanceIndex)
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "当前未配置登录操作。");
		}

		public ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetApps()
		{
			IReadOnlyList<ZzzAppDto> readOnlyList = _apps;
			if (readOnlyList == null)
			{
				if (!_hasOneDragonApp)
				{
					IReadOnlyList<ZzzAppDto> readOnlyList2 = Array.Empty<ZzzAppDto>();
					readOnlyList = readOnlyList2;
				}
				else
				{
					IReadOnlyList<ZzzAppDto> readOnlyList2 = new ZzzAppDto[] { new ZzzAppDto("one_dragon", "一条龙", DefaultGroup: true, NeedNotify: false) };
					readOnlyList = readOnlyList2;
				}
			}
			return ZzzBackendResult<IReadOnlyList<ZzzAppDto>>.Ok(readOnlyList);
		}

		public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> GetOneDragonApps(int? instanceIndex = null)
		{
			int value = instanceIndex ?? CurrentInstanceIndex();
			ZzzAppDto[] array = (GetApps().Value ?? Array.Empty<ZzzAppDto>()).Where((ZzzAppDto app) => app.DefaultGroup && app.AppId != "one_dragon").ToArray();
			Dictionary<string, ZzzAppDto> appMap = array.ToDictionary<ZzzAppDto, string>((ZzzAppDto app) => app.AppId, StringComparer.Ordinal);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = GetConfigScope("one-dragon-group", value, "one_dragon");
			ZzzConfigScopeValuesDto? value2 = configScope.Value;
			object value3;
			List<OneDragonApplicationConfigItem> list = (((object)value2 != null && value2.Values.TryGetValue("app_list", out value3) && value3 is List<OneDragonApplicationConfigItem> source) ? source.Select((OneDragonApplicationConfigItem item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList() : new List<OneDragonApplicationConfigItem>());
			HashSet<string> hashSet = list.Select((OneDragonApplicationConfigItem item) => item.AppId).ToHashSet<string>(StringComparer.Ordinal);
			ZzzAppDto[] array2 = array;
			foreach (ZzzAppDto zzzAppDto in array2)
			{
				if (hashSet.Add(zzzAppDto.AppId))
				{
					list.Add(new OneDragonApplicationConfigItem(zzzAppDto.AppId, enabled: false));
				}
			}
			SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = list }, value, "one_dragon"));
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(list.Where((OneDragonApplicationConfigItem item) => appMap.ContainsKey(item.AppId)).Select(delegate(OneDragonApplicationConfigItem item)
			{
				ZzzAppDto zzzAppDto2 = appMap[item.AppId];
				string appId = item.AppId;
				string name = zzzAppDto2.Name;
				bool enabled = item.Enabled;
				bool needNotify = zzzAppDto2.NeedNotify;
				bool needNotify2 = zzzAppDto2.NeedNotify;
				IReadOnlyList<string>? configScopes = zzzAppDto2.ConfigScopes;
				return new ZzzOneDragonAppDto(appId, name, enabled, needNotify, needNotify2, configScopes != null && configScopes.Count > 0, zzzAppDto2.RunAvailable, null, null);
			}).ToArray());
		}

		public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> SaveOneDragonApps(ZzzSaveOneDragonAppsRequest request)
		{
			int value = request.InstanceIndex ?? CurrentInstanceIndex();
			SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = request.Apps.Select((ZzzOneDragonAppUpdateDto item) => new OneDragonApplicationConfigItem(item.AppId, item.Enabled)).ToList() }, value, "one_dragon"));
			return GetOneDragonApps(value);
		}

		public ZzzBackendResult<ZzzChargePlanCatalogDto> GetChargePlanCatalog()
		{
			return ZzzBackendResult<ZzzChargePlanCatalogDto>.Ok(ChargePlanCatalog);
		}

		public ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> ResetShiyuDefenseRunRecord(int instanceIndex)
		{
			return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Ok(new ZzzShiyuDefenseRunRecordDto(instanceIndex, Array.Empty<int>()));
		}

		public ZzzBackendResult<ZzzLifeOnLineRunRecordDto> GetLifeOnLineRunRecord(int? instanceIndex = null)
		{
			return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Ok(new ZzzLifeOnLineRunRecordDto(instanceIndex ?? CurrentInstanceIndex(), 0));
		}

		public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> GetBattleAssistantConfigCatalog()
		{
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(BattleAssistantCatalog);
		}

		public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> DeleteBattleAssistantConfig(ZzzDeleteBattleAssistantConfigRequest request)
		{
			ZzzBattleAssistantConfigKind kind = request.Kind;
			if (1 == 0)
			{
			}
			ZzzBattleAssistantConfigCatalogDto battleAssistantCatalog = kind switch
			{
				ZzzBattleAssistantConfigKind.AutoBattle => BattleAssistantCatalog with
				{
					AutoBattle = BattleAssistantCatalog.AutoBattle.Where((string name) => !string.Equals(name, request.Name, StringComparison.Ordinal)).ToArray()
				}, 
				ZzzBattleAssistantConfigKind.Dodge => BattleAssistantCatalog with
				{
					Dodge = BattleAssistantCatalog.Dodge.Where((string name) => !string.Equals(name, request.Name, StringComparison.Ordinal)).ToArray()
				}, 
				_ => BattleAssistantCatalog, 
			};
			if (1 == 0)
			{
			}
			BattleAssistantCatalog = battleAssistantCatalog;
			return GetBattleAssistantConfigCatalog();
		}

		public ZzzBackendResult<ZzzBattleAssistantRuntimeDto> GetBattleAssistantRuntime()
		{
			return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Ok(BattleAssistantRuntime);
		}

		public void SubscribeBattleAssistantOperationLoaded(Action callback)
		{
			BattleAssistantOperationLoaded += callback;
			BattleAssistantOperationLoadedSubscriberCount++;
		}

		public void UnsubscribeBattleAssistantOperationLoaded(Action callback)
		{
			BattleAssistantOperationLoaded -= callback;
			BattleAssistantOperationLoadedSubscriberCount--;
		}

		public void PublishBattleAssistantOperationLoaded()
		{
			this.BattleAssistantOperationLoaded?.Invoke();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>> GetRecentLogs(int limit = 200)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>>.Ok(Array.Empty<ZzzLogEntryDto>());
		}

		public ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>> GetConfigScopes()
		{
			return ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>>.Ok(Array.Empty<ZzzConfigScopeDescriptorDto>());
		}

		public ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex = null, string? groupId = null)
		{
			string key = ScopeKey(scope, instanceIndex, groupId);
			_configScopes.TryGetValue(key, out Dictionary<string, object> value);
			ZzzConfigScopeDescriptorDto descriptor = new ZzzConfigScopeDescriptorDto(scope, scope, instanceIndex.HasValue, !string.IsNullOrWhiteSpace(groupId), Writable: true, Array.Empty<ZzzConfigSettingDescriptorDto>());
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(descriptor, instanceIndex, groupId, (value == null) ? new Dictionary<string, object>() : new Dictionary<string, object>(value, StringComparer.Ordinal)));
		}

		public ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfigScope(ZzzSaveConfigScopeRequest request)
		{
			string key = ScopeKey(request.Scope, request.InstanceIndex, request.GroupId);
			if (!_configScopes.TryGetValue(key, out Dictionary<string, object> value))
			{
				value = new Dictionary<string, object>(StringComparer.Ordinal);
				_configScopes[key] = value;
			}
			foreach (KeyValuePair<string, object> value4 in request.Values)
			{
				value4.Deconstruct(out var key2, out var value2);
				string key3 = key2;
				object value3 = value2;
				value[key3] = value3;
			}
			return GetConfigScope(request.Scope, request.InstanceIndex, request.GroupId);
		}

		public ZzzBackendResult<ZzzWindowStatusDto> GetWindow()
		{
			return ZzzBackendResult<ZzzWindowStatusDto>.Ok(new ZzzWindowStatusDto("window", _windowValid, IsWinActive: true, IsWinScale: false));
		}

		public ZzzBackendResult<ZzzScreenshotDto> GetScreenshot()
		{
			return ZzzBackendResult<ZzzScreenshotDto>.Ok(new ZzzScreenshotDto("image/png", new byte[1] { 1 }));
		}

		public Task<ZzzBackendResult<ZzzRunStatusDto>> StartRunAsync(ZzzStartRunRequest request)
		{
			LastStartRequest = request;
			_run = new ZzzRunStatusDto(ZzzRunState.Running, request.AppId, DisplayName(request.AppId), CurrentInstanceIndex(), request.GroupId ?? "default", null, null, 0.0, "已启动");
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(_run));
		}

		public ZzzBackendResult<ZzzRunStatusDto> PauseRun()
		{
			_run = _run with
			{
				State = ZzzRunState.Paused
			};
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(_run);
		}

		public ZzzBackendResult<ZzzRunStatusDto> ResumeRun()
		{
			_run = _run with
			{
				State = ZzzRunState.Running
			};
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(_run);
		}

		public Task<ZzzBackendResult<ZzzRunStatusDto>> StopRunAsync()
		{
			_run = new ZzzRunStatusDto(ZzzRunState.Cancelled);
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(_run));
		}

		public ZzzBackendResult<ZzzRunStatusDto> GetCurrentRun()
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(_run);
		}

		public ChannelReader<ZzzBackendEvent> SubscribeEvents()
		{
			EventSubscriberCount++;
			return _eventBus.Subscribe();
		}

		public void UnsubscribeEvents(ChannelReader<ZzzBackendEvent> reader)
		{
			_eventBus.Unsubscribe(reader);
			EventSubscriberCount--;
		}

		public void PublishEvent(string type, object data)
		{
			_eventBus.Publish(type, data);
		}

		private static string DisplayName(string appId)
		{
			if (1 == 0)
			{
			}
			string result = appId switch
			{
				"auto_battle" => "自动战斗", 
				"dodge_assistant" => "闪避助手", 
				"commission_assistant" => "委托助手", 
				"one_dragon" => "一条龙", 
				"charge_plan" => "体力刷本", 
				"coffee" => "咖啡店", 
				"predefined_team_checker" => "预备编队检查", 
				"mouse_sensitivity_checker" => "灵敏度校准", 
				"screenshot_helper" => "截图助手", 
				"operation_debug" => "指令调试", 
				_ => appId, 
			};
			if (1 == 0)
			{
			}
			return result;
		}

		private static string ScopeKey(string scope, int? instanceIndex, string? groupId)
		{
			return $"{scope}|{instanceIndex?.ToString() ?? string.Empty}|{groupId ?? string.Empty}";
		}

		private int CurrentInstanceIndex()
		{
			return _instances.FirstOrDefault((ZzzInstanceDto instance) => instance.Active)?.Index ?? 0;
		}

		private static bool IsRunActive(ZzzRunState state)
		{
			if ((uint)(state - 1) <= 3u)
			{
				return true;
			}
			return false;
		}
	}

	private static readonly Lazy<AvaloniaTestThread> AvaloniaThread = new Lazy<AvaloniaTestThread>(() => new AvaloniaTestThread());

	private static readonly byte[] OnePixelPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZKZkAAAAASUVORK5CYII=");

	/// <summary>
	/// 应用启动时应按 BaselineParity ThemeManager.load_from_config() 读取已保存主题色。
	/// </summary>
	[Fact]
	public void AppReadsPersistedPythonThemeColorAtStartup()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("custom");
		Assert.True(configScope.Success);
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object> { ["global_theme_color"] = "179,112,71" }));
		Assert.True(zzzBackendResult.Success);
		Assert.True(App.TryReadConfiguredAccentColor(backendHarness.Backend, out var color));
		Assert.Equal(Color.FromRgb(179, 112, 71), color);
	}

	/// <summary>
	/// 默认导航应符合 Fluent 产品范围。
	/// </summary>
	[Fact]
	public void NavigationRegistryMatchesFluentProductScope()
	{
		WithGuiEnvironment(null, null, delegate
		{
			ZzzNavigationRegistry registry = new ZzzNavigationRegistry();
			Assert.Equal(ZzzGuiParityRouteScope.ProductPrimaryNavigationKeys, (from entry in registry.Entries
				where entry.Placement == ZzzNavigationPlacement.Primary
				select entry.Key).ToArray());
			Assert.Equal(ZzzGuiParityRouteScope.ProductFooterNavigationKeys, (from entry in registry.Entries
				where entry.Placement == ZzzNavigationPlacement.Footer
				select entry.Key).ToArray());
			(string, string, string, ZzzNavigationPlacement)[] buffer = new (string, string, string, ZzzNavigationPlacement)[7];
			buffer[0] = ("home", "仪表盘", "\ue80f", ZzzNavigationPlacement.Primary);
			buffer[1] = ("game-assistant", "游戏助手", "\ue7fc", ZzzNavigationPlacement.Primary);
			buffer[2] = ("one-dragon", "一条龙", "\ue768", ZzzNavigationPlacement.Primary);
			buffer[3] = ("standalone", "应用运行", "\uecaa", ZzzNavigationPlacement.Primary);
			buffer[4] = ("devtools", "开发工具", "\uec7a", ZzzNavigationPlacement.Footer);
			buffer[5] = ("accounts", "账户管理", "\ue77b", ZzzNavigationPlacement.Footer);
			buffer[6] = ("settings", "设置", "\ue713", ZzzNavigationPlacement.Footer);
			Assert.Equal(buffer, registry.Entries.Select((ZzzNavigationEntry entry) => (Key: entry.Key, Text: entry.Text, IconGlyph: entry.IconGlyph, Placement: entry.Placement)).ToArray());
			Assert.All(registry.Entries, delegate(ZzzNavigationEntry entry)
			{
				Assert.False(string.IsNullOrWhiteSpace(entry.IconGlyph));
				Assert.False(string.IsNullOrWhiteSpace(entry.SelectedIconGlyph));
				Assert.False(string.IsNullOrWhiteSpace(entry.AccessibleName));
			});
			Assert.All(ZzzGuiParityRouteScope.ExcludedParityRouteKeys, delegate(string key)
			{
				Assert.DoesNotContain((IEnumerable<ZzzNavigationEntry>)registry.Entries, (Predicate<ZzzNavigationEntry>)((ZzzNavigationEntry entry) => entry.Key == key));
			});
		});
	}

	/// <summary>
	/// 开发工具是 BaselineParity 底部产品导航的一部分，开发模式不得改变产品分组。
	/// </summary>
	[Fact]
	public void NavigationRegistryValidationModeKeepsExcludedRoutesOut()
	{
		WithGuiEnvironment("1", null, delegate
		{
			ZzzNavigationRegistry zzzNavigationRegistry = new ZzzNavigationRegistry();
			Assert.Equal(ZzzGuiParityRouteScope.ProductNavigationKeys, zzzNavigationRegistry.Entries.Select((ZzzNavigationEntry entry) => entry.Key).ToArray());
			Assert.DoesNotContain((IEnumerable<ZzzNavigationEntry>)zzzNavigationRegistry.Entries, (Predicate<ZzzNavigationEntry>)delegate(ZzzNavigationEntry entry)
			{
				switch (entry.Key)
				{
				case "pip":
				case "diagnostics":
				case "like":
				case "code-sync":
					return true;
				default:
					return false;
				}
			});
		});
	}

	/// <summary>
	/// 已批准的截图对账路由不包含旧 change 排除项。
	/// </summary>
	[Fact]
	public void ApprovedParityScopeRejectsExcludedRouteKeys()
	{
		Assert.Equal(new string[21]
		{
			"home", "game-assistant-battle", "game-assistant-commission", "one-dragon-run", "one-dragon-charge-plan", "one-dragon-predefined-team", "one-dragon-sensitivity", "standalone-run", "accounts", "settings-game",
			"settings-overlay", "settings-resource-download", "settings-env", "settings-push", "settings-custom", "devtools-image-analysis", "devtools-template-helper", "devtools-screen-manage", "devtools-agent-template", "devtools-screenshot-helper",
			"devtools-operation-debug"
		}, ZzzGuiParityRouteScope.ApprovedParityRouteKeys);
		Assert.Equal(new string[6] { "游戏设置", "Overlay", "资源下载", "脚本环境", "通知设置", "自定义设置" }, ZzzGuiParityRouteScope.ApprovedSettingsTabs);
		Assert.Equal(new string[6] { "like", "code-sync", "pip", "settings-api", "settings-app-config", "diagnostics" }, ZzzGuiParityRouteScope.ExcludedParityRouteKeys);
		Assert.All(ZzzGuiParityRouteScope.ExcludedParityRouteKeys, delegate(string key)
		{
			Assert.DoesNotContain(key, (IEnumerable<string>)ZzzGuiParityRouteScope.ApprovedParityRouteKeys);
		});
	}

	/// <summary>
	/// 游戏助手 AXAML 容器保持 BaselineParity 的子页顺序和可选择行为。
	/// </summary>
	[Fact]
	public void GameAssistantContainerKeepsPythonPivotOrder()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzOd.Gui.Views.FrontierPages.GameAssistant.FrontierGameAssistantPage zzzGameAssistantPage = new ZzzOd.Gui.Views.FrontierPages.GameAssistant.FrontierGameAssistantPage(new FakeBackend(), new ZzzGuiRunIntentService());
			Assert.Equal("TabControl", zzzGameAssistantPage.NavigationTargetKind);
			Assert.Equal(new string[2] { "战斗助手", "委托助手" }, zzzGameAssistantPage.ItemHeaders);
			Assert.Equal("战斗助手", zzzGameAssistantPage.SelectedHeader);
			zzzGameAssistantPage.OnPageShown();
			Assert.True(zzzGameAssistantPage.ActiveChildIsShown);
			for (int index = 0; index < 8; index++)
			{
				Assert.True(zzzGameAssistantPage.SelectByHeader("委托助手"));
				Assert.Equal("委托助手", zzzGameAssistantPage.SelectedHeader);
				Assert.True(zzzGameAssistantPage.SelectByHeader("战斗助手"));
				Assert.Equal("战斗助手", zzzGameAssistantPage.SelectedHeader);
			}
			Assert.False(zzzGameAssistantPage.SelectByHeader("不存在"));
			Assert.False(zzzGameAssistantPage.CanGoBack);
			zzzGameAssistantPage.OnPageLeave();
			zzzGameAssistantPage.OnPageHidden();
			Assert.False(zzzGameAssistantPage.ActiveChildIsShown);
			zzzGameAssistantPage.OnPageShown();
			Assert.True(zzzGameAssistantPage.ActiveChildIsShown);
			zzzGameAssistantPage.DisposePage();
		});
	}

	/// <summary>
	/// 一条龙 AXAML 容器保持 BaselineParity 的子页顺序和可选择行为。
	/// </summary>
	[Fact]
	public void OneDragonContainerKeepsPythonPivotOrder()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FrontierOneDragonPage zzzOneDragonPage = new FrontierOneDragonPage(new FakeBackend(), new ZzzGuiRunIntentService());
			Assert.Equal("TabControl", zzzOneDragonPage.NavigationTargetKind);
			Assert.Equal(new string[4] { "一条龙运行", "体力计划", "预备编队", "灵敏度校准" }, zzzOneDragonPage.ItemHeaders);
			Assert.Equal("一条龙运行", zzzOneDragonPage.SelectedHeader);
			Assert.True(zzzOneDragonPage.SelectByHeader("体力计划"));
			Assert.Equal("体力计划", zzzOneDragonPage.SelectedHeader);
			Assert.True(zzzOneDragonPage.SelectByHeader("预备编队"));
			Assert.Equal("预备编队", zzzOneDragonPage.SelectedHeader);
			Assert.True(zzzOneDragonPage.SelectByHeader("灵敏度校准"));
			Assert.Equal("灵敏度校准", zzzOneDragonPage.SelectedHeader);
			Assert.False(zzzOneDragonPage.SelectByHeader("不存在"));
			Assert.False(zzzOneDragonPage.CanGoBack);
			zzzOneDragonPage.OnPageShown();
			Assert.True(zzzOneDragonPage.ActiveChildIsShown);
			zzzOneDragonPage.OnPageLeave();
			zzzOneDragonPage.OnPageHidden();
			Assert.False(zzzOneDragonPage.ActiveChildIsShown);
			zzzOneDragonPage.DisposePage();
		});
	}

	/// <summary>
	/// Shell 标题栏应读取真实 project.yml、当前实例和程序集版本。
	/// </summary>
	[Fact]
	public void ShellViewModelReadsRealProjectInstanceAndBuildMetadata()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		File.WriteAllText(Path.Combine(backendHarness.RunRoot, "config", "project.yml"), "project_name: ZenlessZoneZero-OneDragon\ngithub_homepage: https://github.com/OneDragon-Anything/ZenlessZoneZero-OneDragon\nscreen_standard_width: 1920\nscreen_standard_height: 1080");
		using ZzzShellViewModel zzzShellViewModel = new ZzzShellViewModel(backendHarness.Backend, new ImmediateUiDispatcher());
		Assert.Equal("绝区零 一条龙", zzzShellViewModel.ProjectName);
		Assert.False(string.IsNullOrWhiteSpace(zzzShellViewModel.ActiveInstanceName));
		Assert.Equal("绝区零 一条龙 " + zzzShellViewModel.ActiveInstanceName, zzzShellViewModel.WindowTitle);
		Assert.Equal(zzzShellViewModel.ActiveInstanceName + " × 绝区零 一条龙", zzzShellViewModel.FrontierWindowTitle);
		Assert.Equal("https://github.com/OneDragon-Anything/ZenlessZoneZero-OneDragon/issues", zzzShellViewModel.IssueUrl);
		Assert.True(zzzShellViewModel.HasLauncherVersion);
		Assert.True(zzzShellViewModel.HasCodeVersion);
		Assert.StartsWith("ⓘ 启动器版本 ", zzzShellViewModel.LauncherVersionText, StringComparison.Ordinal);
		Assert.StartsWith("ⓘ 代码版本 ", zzzShellViewModel.CodeVersionText, StringComparison.Ordinal);
		Assert.DoesNotContain("unknown", zzzShellViewModel.LauncherVersionText, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("unknown", zzzShellViewModel.CodeVersionText, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 激活实例事件应立即刷新标题栏实例名。
	/// </summary>
	[Fact]
	public void ShellViewModelRefreshesWhenActiveInstanceMetadataChanges()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		File.WriteAllText(Path.Combine(backendHarness.RunRoot, "config", "project.yml"), "project_name: ZenlessZoneZero-OneDragon\n");
		ZzzShellViewModel viewModel = new ZzzShellViewModel(backendHarness.Backend, new ImmediateUiDispatcher());
		try
		{
			ManualResetEventSlim changed = new ManualResetEventSlim();
			try
			{
				viewModel.PropertyChanged += delegate(object? _, PropertyChangedEventArgs args)
				{
					if (args.PropertyName == "ActiveInstanceName" && string.Equals(viewModel.ActiveInstanceName, "第二实例", StringComparison.Ordinal))
					{
						changed.Set();
					}
				};
				ZzzInstanceDto value = backendHarness.Backend.GetCurrentInstance().Value;
				ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult = backendHarness.Backend.UpdateInstance(new ZzzUpdateInstanceRequest(value.Index, "第二实例"));
				Assert.True(zzzBackendResult.Success);
				Assert.True(changed.Wait(TimeSpan.FromSeconds(2L)));
				Assert.Equal("绝区零 一条龙 第二实例", viewModel.WindowTitle);
				Assert.Equal("第二实例 × 绝区零 一条龙", viewModel.FrontierWindowTitle);
			}
			finally
			{
				if (changed != null)
				{
					((IDisposable)changed).Dispose();
				}
			}
		}
		finally
		{
			if (viewModel != null)
			{
				((IDisposable)viewModel).Dispose();
			}
		}
	}

	/// <summary>
	/// 开发工具页应暴露保留控件，并为文件类操作提供确定性的本地状态。
	/// </summary>
	[Fact]
	public void DevtoolsPagesExposeRetainedModelsAndLocalFileOperations()
	{
		EnsureAvaloniaServices();
		string runRoot = Path.Combine(Path.GetTempPath(), "zzzod-devtools-pages-tests", Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(runRoot);
			string imagePath = Path.Combine(runRoot, "sample.png");
			using (Mat img = new Mat(160, 200, MatType.CV_8UC3, new Scalar(20.0, 80.0, 160.0)))
			{
				Cv2.ImWrite(imagePath, img);
			}
			RunOnUiThread(delegate
			{
				FakeBackend backend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, runRoot);
				using ZzzRuntimeManager runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance);
				IZzzImageAnalysisService service = new ZzzImageAnalysisService(new ZzzRunRoot(runRoot), runtime);
				ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierImageAnalysisPage zzzImageAnalysisPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierImageAnalysisPage(backend, service);
				zzzImageAnalysisPage.OpenImageForTest(imagePath);
				zzzImageAnalysisPage.AddStepForTest("灰度化");
				zzzImageAnalysisPage.RunPipelineForTest();
				string path = zzzImageAnalysisPage.SaveAsPipelineForTest("测试流水线");
				Assert.Contains("灰度化", (IEnumerable<string>)zzzImageAnalysisPage.PipelineSteps);
				Assert.True(File.Exists(path));
				Assert.Contains("step: 灰度化", File.ReadAllText(path), StringComparison.Ordinal);
				ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierTemplateHelperPage zzzTemplateHelperAxamlPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierTemplateHelperPage(backend);
				zzzTemplateHelperAxamlPage.CreateTemplateForTest("battle", "avatar_test", "测试头像");
				zzzTemplateHelperAxamlPage.ChooseImageForTest(imagePath);
				zzzTemplateHelperAxamlPage.AddPointForTest(20, 40);
				zzzTemplateHelperAxamlPage.AddPointForTest(120, 140);
				string text = zzzTemplateHelperAxamlPage.SaveConfigForTest();
				string path2 = zzzTemplateHelperAxamlPage.SaveRawForTest();
				string path3 = zzzTemplateHelperAxamlPage.SaveMaskForTest();
				Assert.True(File.Exists(text));
				Assert.True(File.Exists(path2));
				Assert.True(File.Exists(path3));
				string[] buffer = new string[5];
				buffer[0] = "assets";
				buffer[1] = "template";
				buffer[2] = "battle";
				buffer[3] = "avatar_test";
				buffer[4] = "config.yml";
				Assert.EndsWith(Path.Combine(buffer), text, StringComparison.Ordinal);
				Assert.Contains("template_shape: rectangle", File.ReadAllText(text), StringComparison.Ordinal);
				zzzTemplateHelperAxamlPage.MovePointsForTest(3, -2);
				Assert.Equal<(int, int)>((23, 38), zzzTemplateHelperAxamlPage.Points[0]);
				zzzTemplateHelperAxamlPage.UndoForTest();
				Assert.Equal<(int, int)>((20, 40), zzzTemplateHelperAxamlPage.Points[0]);
				zzzTemplateHelperAxamlPage.RedoForTest();
				Assert.Equal<(int, int)>((23, 38), zzzTemplateHelperAxamlPage.Points[0]);
				zzzTemplateHelperAxamlPage.ClearPointsForTest();
				Assert.Empty(zzzTemplateHelperAxamlPage.Points);
				ZzzScreenManageService zzzScreenManageService = new ZzzScreenManageService(new ZzzRunRoot(runRoot), backend);
				zzzScreenManageService.SaveScreen(new ZzzScreenDocument(string.Empty, "battle_main", "战斗主画面", string.Empty, PcAlt: true, new ZzzScreenAreaDocument[] { new ZzzScreenAreaDocument("确认按钮", IdMark: true, 1, 2, 3, 4, "确认", 0.6, "battle", "button_ok", 0.8, new IReadOnlyList<int>[2]
				{
					new int[3] { 1, 2, 3 },
					new int[3] { 4, 5, 6 }
				}, new string[] { "next" }, "A") }));
				ZzzScreenDocument zzzScreenDocument = zzzScreenManageService.LoadScreen("战斗主画面");
				Assert.Equal("battle_main", zzzScreenDocument.ScreenId);
				Assert.Equal("确认按钮", Assert.Single(zzzScreenDocument.Areas).AreaName);
				string[] buffer2 = new string[5];
				buffer2[0] = runRoot;
				buffer2[1] = "assets";
				buffer2[2] = "game_data";
				buffer2[3] = "screen_info";
				buffer2[4] = "battle_main.yml";
				Assert.True(File.Exists(Path.Combine(buffer2)));
				ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierAgentTemplateGeneratorPage zzzAgentTemplateGeneratorPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierAgentTemplateGeneratorPage(backend);
				Assert.True(zzzAgentTemplateGeneratorPage.SetAgentIdForTest("anby"));
				Assert.Equal(6, zzzAgentTemplateGeneratorPage.Cards.Count);
			});
		}
		finally
		{
			if (Directory.Exists(runRoot))
			{
				Directory.Delete(runRoot, recursive: true);
			}
		}
	}

	/// <summary>
	/// 截图助手和指令调试应写入真实配置 scope，并使用保留的运行 app_id。
	/// </summary>
	[Fact]
	public void DevtoolsRunSettingsBindConfigScopesAndRunAppIds()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierScreenshotHelperPage zzzScreenshotHelperAxamlPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierScreenshotHelperPage(fakeBackend, new ZzzGuiRunIntentService());
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("screenshot-helper", new Dictionary<string, object>
			{
				["frequency_second"] = 0.25,
				["length_second"] = 2.5,
				["key_save"] = "F10",
				["dodge_detect"] = true
			}, 0, "one_dragon"));
			zzzScreenshotHelperAxamlPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			ZzzConfigScopeValuesDto value = fakeBackend.GetConfigScope("screenshot-helper", 0, "one_dragon").Value;
			Assert.Equal(0.25, value.Values["frequency_second"]);
			Assert.Equal(2.5, value.Values["length_second"]);
			Assert.Equal("F10", value.Values["key_save"]);
			Assert.True((bool)value.Values["dodge_detect"]);
			Assert.Equal("screenshot_helper", fakeBackend.LastStartRequest?.AppId);
			fakeBackend.StopRunAsync().GetAwaiter().GetResult();
			ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierOperationDebugPage zzzOperationDebugAxamlPage = new ZzzOd.Gui.Views.FrontierPages.DevTools.FrontierOperationDebugPage(fakeBackend, new ZzzGuiRunIntentService());
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("operation-debug", new Dictionary<string, object>
			{
				["operation_template"] = "battle/dodge",
				["repeat_enabled"] = true
			}, 0, "one_dragon"));
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("battle-assistant", new Dictionary<string, object> { ["control_method"] = "ds4" }, 0));
			zzzOperationDebugAxamlPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			ZzzConfigScopeValuesDto value2 = fakeBackend.GetConfigScope("operation-debug", 0, "one_dragon").Value;
			Assert.Equal("battle/dodge", value2.Values["operation_template"]);
			Assert.True((bool)value2.Values["repeat_enabled"]);
			Assert.Equal("ds4", fakeBackend.GetConfigScope("battle-assistant", 0).Value.Values["control_method"]);
			Assert.Equal("operation_debug", fakeBackend.LastStartRequest?.AppId);
		});
	}

	/// <summary>
	/// 页面自定义资源只保留媒体遮罩，标准控件颜色交给 FluentAvaloniaTheme。
	/// </summary>
	[Fact]
	public void FluentThemeResourcesLoadForLightAndDarkTheme()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ResourceDictionary resourceDictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://ZzzOd.Gui/Theme/ZzzFluentTheme.axaml"), new Uri("avares://ZzzOd.Gui/"));
			string[] array = new string[2] { "ZzzMediaOverlayBrush", "ZzzMediaOverlayStrongBrush" };
			foreach (string text in array)
			{
				Assert.True(resourceDictionary.TryGetResource(text, ThemeVariant.Default, out object value), "Missing resource " + text + ".");
				Assert.IsAssignableFrom<IBrush>(value);
			}
			FluentAvaloniaTheme fluentAvaloniaTheme = new FluentAvaloniaTheme
			{
				PreferSystemTheme = true,
				PreferUserAccentColor = true
			};
			ThemeVariant[] array2 = new ThemeVariant[3]
			{
				ThemeVariant.Light,
				ThemeVariant.Dark,
				FluentAvaloniaTheme.HighContrastTheme
			};
			foreach (ThemeVariant theme in array2)
			{
				Assert.True(fluentAvaloniaTheme.TryGetResource("TextFillColorPrimaryBrush", theme, out var value2));
				Assert.True(fluentAvaloniaTheme.TryGetResource("ControlFillColorDefaultBrush", theme, out var value3));
				Assert.IsAssignableFrom<IBrush>(value2);
				Assert.IsAssignableFrom<IBrush>(value3);
			}
		});
	}

	/// <summary>
	/// 运行面板应按 idle、running、paused 状态切换按钮文本。
	/// </summary>
	[Fact]
	public void RunPanelUpdatesPrimaryActionForStateTransitions()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			ZzzRunPanel zzzRunPanel = new ZzzRunPanel(fakeBackend, "one_dragon");
			Assert.Equal("one_dragon", zzzRunPanel.SelectedAppId);
			Assert.Equal("开始", zzzRunPanel.PrimaryActionText);
			zzzRunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("暂停", zzzRunPanel.PrimaryActionText);
			Assert.Equal("一条龙", zzzRunPanel.DisplayedApp);
			Assert.Equal("00", zzzRunPanel.DisplayedInstance);
			Assert.Equal("0", zzzRunPanel.DisplayedDuration);
			Assert.True(zzzRunPanel.StopActionEnabled);
			fakeBackend.PauseRun();
			zzzRunPanel.RefreshState();
			Assert.Equal("继续", zzzRunPanel.PrimaryActionText);
			fakeBackend.ResumeRun();
			zzzRunPanel.RefreshState();
			Assert.Equal("暂停", zzzRunPanel.PrimaryActionText);
			zzzRunPanel.InvokeStopActionAsync().GetAwaiter().GetResult();
			Assert.Equal("开始", zzzRunPanel.PrimaryActionText);
			Assert.False(zzzRunPanel.StopActionEnabled);
		});
	}

	/// <summary>
	/// 运行页登记真实目标，并在全局 F9/F10 到达后刷新同一运行状态。
	/// </summary>
	[Fact]
	public void RunPanelTracksTargetAndGlobalRunHotkeys()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("env", new Dictionary<string, object>
			{
				["key_start_running"] = "F9",
				["key_stop_running"] = "F10",
			}));
			ZzzGuiRunIntentService runIntent = new();
			ZzzRunPanel runPanel = new ZzzRunPanel(fakeBackend, "one_dragon", runIntent: runIntent, fixedGroupId: "one_dragon");
			runPanel.OnPageShown();

			Assert.Equal(new ZzzGuiRunTarget("one_dragon", "one_dragon", null), runIntent.CurrentRunTarget);
			fakeBackend.StartRunAsync(new ZzzStartRunRequest("one_dragon", GroupId: "one_dragon")).GetAwaiter().GetResult();
			runIntent.PublishGlobalInputPressed("f9");
			Assert.Equal("暂停", runPanel.PrimaryActionText);

			fakeBackend.PauseRun();
			runIntent.PublishGlobalInputPressed("f9");
			Assert.Equal("继续", runPanel.PrimaryActionText);

			fakeBackend.StopRunAsync().GetAwaiter().GetResult();
			runIntent.PublishGlobalInputPressed("f10");
			Assert.Equal("开始", runPanel.PrimaryActionText);

			runPanel.OnPageHidden();
			Assert.Null(runIntent.CurrentRunTarget);
			runPanel.DisposePage();
		});
	}

	/// <summary>
	/// frontier 运行页重新显示时恢复后端运行状态，并重新登记当前目标。
	/// </summary>
	[Fact]
	public void FrontierRunPanelRestoresStateAfterNavigation()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend(run: new ZzzRunStatusDto(
				ZzzRunState.Running,
				"one_dragon",
				"一条龙",
				0,
				"one_dragon"));
			ZzzGuiRunIntentService runIntent = new();
			FrontierOneDragonRunPage page = new(fakeBackend, runIntent);

			page.OnPageShown();
			Assert.Equal("暂停", page.RunPanel.PrimaryActionText);
			Assert.Equal("one_dragon", runIntent.CurrentRunTarget?.AppId);

			page.OnPageHidden();
			Assert.Null(runIntent.CurrentRunTarget);
			page.OnPageShown();
			Assert.Equal("暂停", page.RunPanel.PrimaryActionText);
			Assert.Equal("one_dragon", runIntent.CurrentRunTarget?.AppId);

			page.DisposePage();
		});
	}

	[Fact]
	public void RunPanelCommandsRemainEqualAndSeparatedAtNarrowWidth()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzRunPanel panel = new(new FakeBackend(), "one_dragon");
			panel.Measure(new Avalonia.Size(320, 560));
			panel.Arrange(new Avalonia.Rect(0, 0, 320, 560));
			Button primary = panel.FindControl<Button>("PrimaryButton")!;
			Button stop = panel.FindControl<Button>("StopButton")!;

			Assert.Contains("zzz-run-command", primary.Classes);
			Assert.Contains("zzz-run-command", stop.Classes);
			Assert.Equal(primary.Bounds.Width, stop.Bounds.Width, 3);
			panel.DisposePage();
		});
	}

	/// <summary>
	/// 战斗助手应保留 BaselineParity 控件和配置 key，并按模式选择运行 app_id。
	/// </summary>
	[Fact]
	public void BattleAssistantModelBindsPythonControlsAndSwitchesRunAppId()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			FrontierBattleAssistantPage zzzBattleAssistantPage = new FrontierBattleAssistantPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzBattleAssistantPage.OnPageShown();
			FrontierBattleAssistantSettings zzzBattleAssistantSettings = Assert.IsType<FrontierBattleAssistantSettings>(zzzBattleAssistantPage.LeftContent);
			ZzzGameAssistantPageModel pageModel = zzzBattleAssistantSettings.PageModel;
			Assert.Equal(new string[2] { "自动战斗", "闪避助手" }, pageModel.ModeHeaders);
			Assert.Equal("auto_battle", pageModel.SelectedAppId);
			Assert.Contains("GPU运算", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("截图间隔", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("操作方式", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("闪避方式", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Scope == "model" && binding.Key == "flash_classifier_gpu"));
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Scope == "battle-assistant" && binding.Key == "screenshot_interval"));
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Scope == "battle-assistant" && binding.Key == "control_method"));
			Assert.Equal("auto_battle", zzzBattleAssistantPage.RunPanel.SelectedAppId);
			Assert.True(zzzBattleAssistantPage.IsTaskDisplayVisible);
			zzzBattleAssistantPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("auto_battle", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("自动战斗", zzzBattleAssistantPage.RunPanel.DisplayedApp);
			zzzBattleAssistantPage.RunPanel.InvokeStopActionAsync().GetAwaiter().GetResult();
			Assert.True(zzzBattleAssistantSettings.SelectModeByHeader("闪避助手"));
			Assert.Equal("dodge_assistant", zzzBattleAssistantSettings.SelectedAppId);
			Assert.Equal("dodge_assistant", zzzBattleAssistantPage.RunPanel.SelectedAppId);
			Assert.False(zzzBattleAssistantPage.IsTaskDisplayVisible);
			Assert.DoesNotContain<string>("等待运行事件", EnumerateText(zzzBattleAssistantPage), StringComparer.Ordinal);
			zzzBattleAssistantPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("dodge_assistant", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("闪避助手", zzzBattleAssistantPage.RunPanel.DisplayedApp);
		});
	}

	/// <summary>
	/// 战斗助手列表只显示真实目录项，并按 BaselineParity 语义删除普通 yml 后刷新。
	/// </summary>
	[Fact]
	public void BattleAssistantUsesRealCatalogWithoutInjectingConfiguredDefaults()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("battle-assistant", new Dictionary<string, object>
			{
				["auto_battle_config"] = "全配队通用",
				["dodge_assistant_config"] = "闪避"
			}));
			FrontierBattleAssistantPage zzzBattleAssistantPage = new FrontierBattleAssistantPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzBattleAssistantPage.OnPageShown();
			FrontierBattleAssistantSettings zzzBattleAssistantSettings = Assert.IsType<FrontierBattleAssistantSettings>(zzzBattleAssistantPage.LeftContent);
			Assert.Empty(zzzBattleAssistantSettings.AutoBattleOptions);
			Assert.Empty(zzzBattleAssistantSettings.DodgeOptions);
			Assert.Null(zzzBattleAssistantSettings.SelectedAutoBattleConfig);
			Assert.Null(zzzBattleAssistantSettings.SelectedDodgeConfig);
			FakeBackend fakeBackend2 = new FakeBackend
			{
				BattleAssistantCatalog = new ZzzBattleAssistantConfigCatalogDto(new string[2] { "配置A", "配置B" }, new string[2] { "闪避A", "闪避B" })
			};
			fakeBackend2.SaveConfigScope(new ZzzSaveConfigScopeRequest("battle-assistant", new Dictionary<string, object>
			{
				["auto_battle_config"] = "配置A",
				["dodge_assistant_config"] = "闪避A"
			}));
			FrontierBattleAssistantPage zzzBattleAssistantPage2 = new FrontierBattleAssistantPage(fakeBackend2, new ZzzGuiRunIntentService());
			zzzBattleAssistantPage2.OnPageShown();
			FrontierBattleAssistantSettings zzzBattleAssistantSettings2 = Assert.IsType<FrontierBattleAssistantSettings>(zzzBattleAssistantPage2.LeftContent);
			Assert.Equal(new string[2] { "配置A", "配置B" }, zzzBattleAssistantSettings2.AutoBattleOptions);
			Assert.Equal(new string[2] { "闪避A", "闪避B" }, zzzBattleAssistantSettings2.DodgeOptions);
			Assert.Equal("配置A", zzzBattleAssistantSettings2.SelectedAutoBattleConfig);
			Assert.Equal("闪避A", zzzBattleAssistantSettings2.SelectedDodgeConfig);
			zzzBattleAssistantSettings2.DeleteSelectedAutoBattleConfig();
			zzzBattleAssistantSettings2.DeleteSelectedDodgeConfig();
			Assert.Equal(new string[] { "配置B" }, zzzBattleAssistantSettings2.AutoBattleOptions);
			Assert.Equal(new string[] { "闪避B" }, zzzBattleAssistantSettings2.DodgeOptions);
			Assert.Null(zzzBattleAssistantSettings2.SelectedAutoBattleConfig);
			Assert.Null(zzzBattleAssistantSettings2.SelectedDodgeConfig);
		});
	}

	/// <summary>
	/// 战斗助手按真实快照刷新任务和状态，并按 BaselineParity 规则调整过滤排序。
	/// </summary>
	[Fact]
	public void BattleAssistantRefreshesRuntimeSnapshotAndFiltersStateOrder()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			EnsureFluentTheme();
			FakeBackend fakeBackend = new FakeBackend
			{
				BattleAssistantRuntime = new ZzzBattleAssistantRuntimeDto(IsRunning: true, "闪避识别-红光", "红光 ← 前台-安比", 0.123456, new ZzzBattleAssistantStateDto[3]
				{
					new ZzzBattleAssistantStateDto("治疗", 10.0, 1.25, null, 1L),
					new ZzzBattleAssistantStateDto("闪避", 11.0, 0.05, 1, 2L),
					new ZzzBattleAssistantStateDto("攻击", 12.0, 0.1, 2, 3L)
				})
			};
			FrontierBattleAssistantPage zzzBattleAssistantPage = new FrontierBattleAssistantPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzBattleAssistantPage.OnPageShown();
			Assert.True(zzzBattleAssistantPage.IsRuntimeRefreshActive);
			Assert.Equal("闪避识别-红光", zzzBattleAssistantPage.TaskTriggerText);
			Assert.Equal("红光 ← 前台-安比", zzzBattleAssistantPage.TaskExpressionText);
			Assert.Equal("0.1235", zzzBattleAssistantPage.TaskDurationText);
			Assert.Equal(new string[3] { "攻击", "治疗", "闪避" }, zzzBattleAssistantPage.DisplayedStateRows.Select((ZzzBattleAssistantStateRowModel row) => row.StateName));
			Assert.Equal("0.0500", zzzBattleAssistantPage.DisplayedStateRows.Single((ZzzBattleAssistantStateRowModel row) => row.StateName == "闪避").TriggerSecondsText);
			Assert.Equal(string.Empty, zzzBattleAssistantPage.DisplayedStateRows.Single((ZzzBattleAssistantStateRowModel row) => row.StateName == "治疗").ValueText);
			zzzBattleAssistantPage.SetBattleStateFilter("闪");
			Assert.Equal(new string[3] { "闪避", "攻击", "治疗" }, zzzBattleAssistantPage.DisplayedStateRows.Select((ZzzBattleAssistantStateRowModel row) => row.StateName));
			FrontierBattleAssistantSettings zzzBattleAssistantSettings = Assert.IsType<FrontierBattleAssistantSettings>(zzzBattleAssistantPage.LeftContent);
			Assert.True(zzzBattleAssistantSettings.SelectModeByHeader("闪避助手"));
			Assert.False(zzzBattleAssistantPage.IsTaskDisplayVisible);
			Assert.True(zzzBattleAssistantPage.IsRuntimeRefreshActive);
			fakeBackend.BattleAssistantRuntime = new ZzzBattleAssistantRuntimeDto(IsRunning: false, null, null, null, Array.Empty<ZzzBattleAssistantStateDto>());
			zzzBattleAssistantPage.RefreshRuntimeState();
			Assert.Equal("/", zzzBattleAssistantPage.TaskTriggerText);
			Assert.Equal("/", zzzBattleAssistantPage.TaskExpressionText);
			Assert.Equal("/", zzzBattleAssistantPage.TaskDurationText);
			Assert.Empty(zzzBattleAssistantPage.DisplayedStateRows);
			Assert.False(zzzBattleAssistantPage.IsRuntimeRefreshActive);
		});
	}

	/// <summary>
	/// 战斗助手页面显示时订阅真实指令加载事件，隐藏和释放后取消订阅。
	/// </summary>
	[Fact]
	public void BattleAssistantRuntimeSubscriptionFollowsPageLifecycle()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			EnsureFluentTheme();
			FakeBackend fakeBackend = new FakeBackend();
			FrontierBattleAssistantPage zzzBattleAssistantPage = new FrontierBattleAssistantPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzBattleAssistantPage.OnPageShown();
			Assert.Equal(1, fakeBackend.BattleAssistantOperationLoadedSubscriberCount);
			Assert.False(zzzBattleAssistantPage.IsRuntimeRefreshActive);
			zzzBattleAssistantPage.OnPageHidden();
			Assert.Equal(0, fakeBackend.BattleAssistantOperationLoadedSubscriberCount);
			fakeBackend.BattleAssistantRuntime = new ZzzBattleAssistantRuntimeDto(IsRunning: true, "主循环", "/", 0.1, Array.Empty<ZzzBattleAssistantStateDto>());
			fakeBackend.PublishBattleAssistantOperationLoaded();
			Assert.False(zzzBattleAssistantPage.IsRuntimeRefreshActive);
			zzzBattleAssistantPage.OnPageShown();
			Assert.Equal(1, fakeBackend.BattleAssistantOperationLoadedSubscriberCount);
			Assert.True(zzzBattleAssistantPage.IsRuntimeRefreshActive);
			zzzBattleAssistantPage.DisposePage();
			Assert.Equal(0, fakeBackend.BattleAssistantOperationLoadedSubscriberCount);
		});
	}

	/// <summary>
	/// 生产门面从真实 run root 读取并删除战斗助手普通配置文件。
	/// </summary>
	[Fact]
	public void BattleAssistantCatalogFacadeUsesRealRunRootFiles()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		string text = Path.Combine(backendHarness.RunRoot, "config", "auto_battle");
		string text2 = Path.Combine(backendHarness.RunRoot, "config", "dodge");
		Directory.CreateDirectory(text);
		Directory.CreateDirectory(text2);
		string path = Path.Combine(text, "可删.yml");
		string path2 = Path.Combine(text, "样例.sample.yml");
		File.WriteAllText(path, "scenes: []");
		File.WriteAllText(path2, "scenes: []");
		File.WriteAllText(Path.Combine(text2, "智能格挡.merged.yml"), "scenes: []");
		ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> battleAssistantConfigCatalog = backendHarness.Backend.GetBattleAssistantConfigCatalog();
		Assert.True(battleAssistantConfigCatalog.Success, battleAssistantConfigCatalog.Error);
		Assert.Equal(new string[2] { "可删", "样例" }, battleAssistantConfigCatalog.Value.AutoBattle);
		Assert.Equal(new string[] { "智能格挡" }, battleAssistantConfigCatalog.Value.Dodge);
		ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> zzzBackendResult = backendHarness.Backend.DeleteBattleAssistantConfig(new ZzzDeleteBattleAssistantConfigRequest(ZzzBattleAssistantConfigKind.AutoBattle, "可删"));
		Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
		Assert.Equal(new string[] { "样例" }, zzzBackendResult.Value.AutoBattle);
		Assert.False(File.Exists(path));
		Assert.True(File.Exists(path2));
	}

	/// <summary>
	/// 委托助手应保留 BaselineParity 配置项，并固定运行 commission_assistant。
	/// </summary>
	[Fact]
	public void CommissionAssistantModelBindsPythonControlsAndRunPanel()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			FrontierCommissionAssistantPage zzzCommissionAssistantPage = new FrontierCommissionAssistantPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzCommissionAssistantPage.OnPageShown();
			FrontierCommissionAssistantSettings zzzCommissionAssistantSettings = Assert.IsType<FrontierCommissionAssistantSettings>(zzzCommissionAssistantPage.LeftContent);
			ZzzGameAssistantPageModel pageModel = zzzCommissionAssistantSettings.PageModel;
			Assert.Equal("game-assistant-commission", pageModel.Key);
			Assert.Equal(new string[] { "commission_assistant" }, pageModel.RunAppIds);
			Assert.Equal("commission_assistant", zzzCommissionAssistantPage.RunPanel.SelectedAppId);
			Assert.Contains("游戏在后台时暂停", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("对话选项优先级", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("无内容时等待时间", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("自动闪避开关", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.Contains("自动战斗开关", (IEnumerable<string>)pageModel.SettingLabels);
			Assert.All(pageModel.Bindings, delegate(ZzzGameAssistantBindingSpec binding)
			{
				Assert.Equal("commission-assistant", binding.Scope);
			});
			Assert.All(pageModel.Bindings, delegate(ZzzGameAssistantBindingSpec binding)
			{
				Assert.Equal("one_dragon", binding.GroupId);
			});
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Key == "pause_in_background"));
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Key == "dialog_click_interval"));
			Assert.Contains((IEnumerable<ZzzGameAssistantBindingSpec>)pageModel.Bindings, (Predicate<ZzzGameAssistantBindingSpec>)((ZzzGameAssistantBindingSpec binding) => binding.Key == "sleep_after_empty_screen"));
			zzzCommissionAssistantPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("commission_assistant", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("one_dragon", fakeBackend.LastStartRequest?.GroupId);
			Assert.Equal("委托助手", zzzCommissionAssistantPage.RunPanel.DisplayedApp);
			Assert.Equal("已启动", zzzCommissionAssistantPage.RunPanel.DisplayedLastStatus);
		});
	}

	/// <summary>
	/// 一条龙运行页应保存应用组设置，并用 one_dragon group 启动全量或单项运行。
	/// </summary>
	[Fact]
	public void OneDragonRunPageWritesAppGroupSettingsAndRunIntent()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, new ZzzAppDto[3]
			{
				new ZzzAppDto("coffee", "咖啡店", DefaultGroup: true, NeedNotify: true),
				new ZzzAppDto("charge_plan", "体力刷本", DefaultGroup: true, NeedNotify: true),
				new ZzzAppDto("predefined_team_checker", "预备编队检查", DefaultGroup: false, NeedNotify: false)
			});
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon", new Dictionary<string, object>
			{
				["instance_run"] = "仅运行当前",
				["after_done"] = "无"
			}));
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("notify", new Dictionary<string, object>
			{
				["enable_notify"] = true,
				["merge_error_immediate_notify"] = true,
				["applications"] = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal)
			}, 0));
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = new List<OneDragonApplicationConfigItem>
			{
				new OneDragonApplicationConfigItem("charge_plan", enabled: true),
				new OneDragonApplicationConfigItem("coffee", enabled: false)
			} }, 0, "one_dragon"));
			FrontierOneDragonRunPage zzzOneDragonRunPage = new FrontierOneDragonRunPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzOneDragonRunPage.OnPageShown();
			ZzzOneDragonRunSettings settings = zzzOneDragonRunPage.Settings;
			Assert.False(typeof(Control).IsAssignableFrom(typeof(ZzzOneDragonRunSettings)));
			Assert.Equal("one-dragon-run", settings.PageModel.Key);
			Assert.Equal(new string[3] { "one_dragon", "charge_plan", "coffee" }, settings.PageModel.AppIds);
			Assert.Contains("app_list", (IEnumerable<string>)settings.PageModel.ConfigKeys);
			Assert.Equal("one_dragon", zzzOneDragonRunPage.RunPanel.SelectedAppId);
			string[] buffer = new string[2];
			buffer[0] = "charge_plan";
			buffer[1] = "coffee";
			Assert.Equal(buffer, settings.AppRows.Select((ZzzOneDragonAppRowModel row) => row.AppId).ToArray());
			ZzzOneDragonAppRowModel coffeeRow = settings.AppRows.Single((ZzzOneDragonAppRowModel row) => row.AppId == "coffee");
			Assert.False(coffeeRow.Enabled);
			settings.SetAppEnabledForTest("coffee", enabled: true);
			Assert.Same(coffeeRow, settings.AppRows.Single((ZzzOneDragonAppRowModel row) => row.AppId == "coffee"));
			Assert.True(coffeeRow.Enabled);
			settings.MoveAppForTest("coffee", -1);
			settings.SetInstanceRunForTest("全部实例");
			settings.SetAfterDoneForTest("关机");
			settings.SetNotifyEnabledForTest(enabled: false);
			Assert.True(settings.TryGetAppNotifyModesForTest("coffee", out string lifecycle, out string detail));
			Assert.Equal("start_and_finish", lifecycle);
			Assert.Equal("all", detail);
			Assert.True(settings.SetAppNotifyModesForTest("coffee", "finish_only", "merge"));
			List<OneDragonApplicationConfigItem> list = Assert.IsType<List<OneDragonApplicationConfigItem>>(fakeBackend.GetConfigScope("one-dragon-group", 0, "one_dragon").Value.Values["app_list"]);
			string[] buffer2 = new string[2];
			buffer2[0] = "coffee";
			buffer2[1] = "charge_plan";
			Assert.Equal(buffer2, list.Select((OneDragonApplicationConfigItem app) => app.AppId).ToArray());
			Assert.All(list, delegate(OneDragonApplicationConfigItem app)
			{
				Assert.True(app.Enabled);
			});
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = fakeBackend.GetConfigScope("one-dragon");
			Assert.Equal("全部实例", configScope.Value.Values["instance_run"]);
			Assert.Equal("关机", configScope.Value.Values["after_done"]);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope2 = fakeBackend.GetConfigScope("notify", 0);
			Assert.False((bool)configScope2.Value.Values["enable_notify"]);
			Dictionary<string, NotifyApplicationSetting> dictionary = ZzzNotifySettingsReader.ReadApplications(configScope2.Value.Values);
			Assert.Equal("finish_only", dictionary["coffee"].Lifecycle);
			Assert.Equal("merge", dictionary["coffee"].Detail);
			zzzOneDragonRunPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("one_dragon", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("one_dragon", fakeBackend.LastStartRequest?.GroupId);
			zzzOneDragonRunPage.RunPanel.InvokeStopActionAsync().GetAwaiter().GetResult();
			settings.StartSingleAppAsync("coffee").GetAwaiter().GetResult();
			Assert.Equal("coffee", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("one_dragon", fakeBackend.LastStartRequest?.GroupId);
		});
	}

	/// <summary>
	/// 一条龙全局通知设置按钮应请求推入当前页栈的真实通知设置页。
	/// </summary>
	[Fact]
	public void OneDragonRunPageRequestsNotifySecondaryPage()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend backend = new FakeBackend();
			FrontierOneDragonRunPage zzzOneDragonRunPage = new FrontierOneDragonRunPage(backend, new ZzzGuiRunIntentService());
			Control requested = null;
			zzzOneDragonRunPage.SecondaryPageRequested += delegate(object? _, Control control)
			{
				requested = control;
			};
			zzzOneDragonRunPage.OnPageShown();
			Button button = zzzOneDragonRunPage.FindControl<Button>("GlobalNotifySettingsButton");
			button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Assert.IsType<FrontierNotifySettingsPage>(requested);
			zzzOneDragonRunPage.OnPageHidden();
			zzzOneDragonRunPage.DisposePage();
		});
	}

	/// <summary>
	/// 一条龙运行页应在运行和实例事件后刷新真实列表，并在隐藏时取消订阅。
	/// </summary>
	[Fact]
	public void OneDragonRunPageRefreshesFromBackendEventsAndUnsubscribes()
	{
		EnsureAvaloniaServices();
		FakeBackend backend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, new ZzzAppDto[2]
		{
			new ZzzAppDto("coffee", "咖啡店", DefaultGroup: true, NeedNotify: true),
			new ZzzAppDto("charge_plan", "体力刷本", DefaultGroup: true, NeedNotify: true)
		}, new ZzzInstanceDto[2]
		{
			new ZzzInstanceDto(0, "00", Active: true, "config/00"),
			new ZzzInstanceDto(1, "01", Active: false, "config/01")
		});
		backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = new List<OneDragonApplicationConfigItem>
		{
			new OneDragonApplicationConfigItem("coffee", enabled: false),
			new OneDragonApplicationConfigItem("charge_plan", enabled: true)
		} }, 0, "one_dragon"));
		backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = new List<OneDragonApplicationConfigItem>
		{
			new OneDragonApplicationConfigItem("charge_plan", enabled: false),
			new OneDragonApplicationConfigItem("coffee", enabled: true)
		} }, 1, "one_dragon"));
		FrontierOneDragonRunPage page = null;
		RunOnUiThread(delegate
		{
			page = new FrontierOneDragonRunPage(backend, new ZzzGuiRunIntentService());
			page.OnPageShown();
			Assert.False(page.Settings.AppRows.Single((ZzzOneDragonAppRowModel row) => row.AppId == "coffee").Enabled);
			Assert.True(backend.EventSubscriberCount > 0);
		});
		backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = new List<OneDragonApplicationConfigItem>
		{
			new OneDragonApplicationConfigItem("coffee", enabled: true),
			new OneDragonApplicationConfigItem("charge_plan", enabled: true)
		} }, 0, "one_dragon"));
		backend.PublishEvent("run.stateChanged", new ZzzRunStatusDto(ZzzRunState.Running));
		Assert.True(SpinWait.SpinUntil(delegate
		{
			bool refreshed = false;
			RunOnUiThread(delegate
			{
				Dispatcher.UIThread.RunJobs();
				refreshed = page.Settings.AppRows.Single((ZzzOneDragonAppRowModel row) => row.AppId == "coffee").Enabled;
			});
			return refreshed;
		}, TimeSpan.FromSeconds(3L)));
		backend.ActivateInstance(1);
		backend.PublishEvent("instance.activeChanged", backend.GetCurrentInstance().Value);
		Assert.True(SpinWait.SpinUntil(delegate
		{
			int? instanceIndex = null;
			RunOnUiThread(delegate
			{
				Dispatcher.UIThread.RunJobs();
				instanceIndex = page.Settings.InstanceIndex;
			});
			return instanceIndex == 1;
		}, TimeSpan.FromSeconds(3L)));
		RunOnUiThread(delegate
		{
			string[] buffer = new string[2];
			buffer[0] = "charge_plan";
			buffer[1] = "coffee";
			Assert.Equal(buffer, page.Settings.AppRows.Select((ZzzOneDragonAppRowModel row) => row.AppId).ToArray());
			page.OnPageHidden();
			page.DisposePage();
		});
		Assert.Equal(0, backend.EventSubscriberCount);
	}

	/// <summary>
	/// 体力计划页应读写真配置，并保存计划行内字段和列表操作。
	/// </summary>
	[Fact]
	public void ChargePlanPageWritesBindingsAndPlanList()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			fakeBackend.ChargePlanCatalog = new ZzzChargePlanCatalogDto(new ZzzChargePlanCategoryDto[3]
			{
				new ZzzChargePlanCategoryDto("实战模拟室", "实战模拟室", new ZzzChargePlanMissionTypeDto[] { new ZzzChargePlanMissionTypeDto("基础材料", "基础材料", new ZzzChargePlanMissionDto[2]
				{
					new ZzzChargePlanMissionDto("调查专项", "调查专项"),
					new ZzzChargePlanMissionDto("自定义关卡", "自定义关卡")
				}) }),
				new ZzzChargePlanCategoryDto("区域巡防", "区域巡防", new ZzzChargePlanMissionTypeDto[] { new ZzzChargePlanMissionTypeDto("自定义类型", "自定义类型", new ZzzChargePlanMissionDto[] { new ZzzChargePlanMissionDto("自定义关卡", "自定义关卡") }) }),
				new ZzzChargePlanCategoryDto("恶名狩猎 深度追猎", "恶名狩猎", new ZzzChargePlanMissionTypeDto[] { new ZzzChargePlanMissionTypeDto("特训目标", "特训目标", Array.Empty<ZzzChargePlanMissionDto>()) })
			}, new ZzzChargePlanTeamDto[2]
			{
				new ZzzChargePlanTeamDto(0, "编队1"),
				new ZzzChargePlanTeamDto(1, "编队2")
			}, new string[2] { "全配队通用", "手动配置" });
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("charge-plan", new Dictionary<string, object>
			{
				["loop"] = true,
				["skip_plan"] = false,
				["daily_reset_plan_times"] = true,
				["restore_charge"] = RestoreChargeMode.Both.DisplayName,
				["double_reward"] = true,
				["combat_simulation_double_reward_config"] = new ChargePlanItem
				{
					MissionTypeName = "基础材料",
					MissionName = "调查专项"
				},
				["plan_list"] = new List<ChargePlanItem>
				{
					new ChargePlanItem
					{
						CategoryName = "实战模拟室",
						MissionTypeName = "基础材料",
						MissionName = "调查专项",
						RunTimes = 1,
						PlanTimes = 2
					},
					new ChargePlanItem
					{
						CategoryName = "恶名狩猎",
						MissionTypeName = "特训目标",
						MissionName = "默认关卡",
						RunTimes = 3,
						PlanTimes = 3
					}
				}
			}, 0, ChargePlanConstants.DefaultGroupId));
			FrontierChargePlanPage zzzChargePlanPage = new FrontierChargePlanPage(fakeBackend);
			zzzChargePlanPage.OnPageShown();
			Assert.True(SpinWait.SpinUntil(() => zzzChargePlanPage.State.Plans.Count == 2, TimeSpan.FromSeconds(1)));
			Assert.Equal("one-dragon-charge-plan", zzzChargePlanPage.PageModel.Key);
			Assert.Contains("combat_simulation_double_reward_config", (IEnumerable<string>)zzzChargePlanPage.PageModel.ConfigKeys);
			Assert.Equal(2, zzzChargePlanPage.PageModel.ItemCount);
			Assert.Equal("实战模拟室", zzzChargePlanPage.State.Plans[0].CategoryName);
			zzzChargePlanPage.State.UpdatePlan(0, delegate(ChargePlanItem plan)
			{
				plan.CategoryName = "区域巡防";
				plan.MissionTypeName = "自定义类型";
				plan.MissionName = "自定义关卡";
				plan.CardNum = "2";
				plan.PredefinedTeamIndex = 1;
				plan.RunTimes = 2;
				plan.PlanTimes = 4;
				plan.AutoBattleConfig = "手动配置";
			});
			List<ChargePlanItem> list = Assert.IsType<List<ChargePlanItem>>(fakeBackend.GetConfigScope("charge-plan", 0, ChargePlanConstants.DefaultGroupId).Value.Values["plan_list"]);
			Assert.Equal("区域巡防", list[0].CategoryName);
			Assert.Equal("自定义类型", list[0].MissionTypeName);
			Assert.Equal("自定义关卡", list[0].MissionName);
			Assert.Equal("2", list[0].CardNum);
			Assert.Equal(1, list[0].PredefinedTeamIndex);
			Assert.Equal(2, list[0].RunTimes);
			Assert.Equal(4, list[0].PlanTimes);
			Assert.Equal("手动配置", list[0].AutoBattleConfig);
			zzzChargePlanPage.State.DeleteCompleted();
			Assert.Single(zzzChargePlanPage.State.Plans);
			zzzChargePlanPage.State.UndoBulkDelete();
			Assert.Equal(2, zzzChargePlanPage.State.Plans.Count);
			zzzChargePlanPage.State.MoveTop(1);
			Assert.Equal("恶名狩猎", zzzChargePlanPage.State.Plans[0].CategoryName);
			ZzzChargePlanRowModel zzzChargePlanRowModel = zzzChargePlanPage.State.CreateDialogRow();
			Assert.Equal(0, zzzChargePlanRowModel.Plan.PredefinedTeamIndex);
			zzzChargePlanPage.State.AddPlan(zzzChargePlanRowModel.Plan);
			Assert.Equal(3, zzzChargePlanPage.State.Plans.Count);
		});
	}

	/// <summary>
	/// 体力计划应保留真实空列表，批量删除后的撤销只恢复当前内存状态。
	/// </summary>
	[Fact]
	public void ChargePlanStateKeepsEmptyListAndPythonUndoSemantics()
	{
		FakeBackend fakeBackend = new FakeBackend();
		fakeBackend.ChargePlanCatalog = new ZzzChargePlanCatalogDto(Array.Empty<ZzzChargePlanCategoryDto>(), Array.Empty<ZzzChargePlanTeamDto>(), Array.Empty<string>());
		fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("charge-plan", new Dictionary<string, object> { ["plan_list"] = new List<ChargePlanItem>() }, 0, "one_dragon"));
		ZzzChargePlanState zzzChargePlanState = new ZzzChargePlanState(fakeBackend);
		zzzChargePlanState.Reload();
		Assert.Empty(zzzChargePlanState.Plans);
		fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("charge-plan", new Dictionary<string, object> { ["plan_list"] = new List<ChargePlanItem>
		{
			new ChargePlanItem
			{
				PlanId = "a",
				RunTimes = 0,
				PlanTimes = 1
			},
			new ChargePlanItem
			{
				PlanId = "b",
				RunTimes = 1,
				PlanTimes = 1
			}
		} }, 0, "one_dragon"));
		ZzzChargePlanState zzzChargePlanState2 = new ZzzChargePlanState(fakeBackend);
		zzzChargePlanState2.Reload();
		zzzChargePlanState2.MoveTo(1, 0);
		Assert.Equal<string[]>(new string[2] { "b", "a" }, zzzChargePlanState2.Plans.Select((ChargePlanItem plan) => plan.PlanId).ToArray());
		zzzChargePlanState2.DeleteAll();
		Assert.Empty(zzzChargePlanState2.Plans);
		zzzChargePlanState2.UndoBulkDelete();
		Assert.Equal(2, zzzChargePlanState2.Plans.Count);
		List<ChargePlanItem> collection = Assert.IsType<List<ChargePlanItem>>(fakeBackend.GetConfigScope("charge-plan", 0, "one_dragon").Value.Values["plan_list"]);
		Assert.Empty(collection);
	}

	/// <summary>
	/// 预备编队和灵敏度校准页应暴露 BaselineParity 对应运行入口和配置保存。
	/// </summary>
	[Fact]
	public void PredefinedTeamAndSensitivityPagesWriteConfigAndRunIntent()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend();
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			TeamConfig teamConfig = new TeamConfig();
			int num = 4;
			List<PredefinedTeamInfo> list = new List<PredefinedTeamInfo>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<PredefinedTeamInfo> span = CollectionsMarshal.AsSpan(list);
			ref PredefinedTeamInfo reference = ref span[0];
			int num2 = 3;
			List<string> list2 = new List<string>(num2);
			CollectionsMarshal.SetCount(list2, num2);
			Span<string> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = "anby";
			span2[1] = "nicole";
			span2[2] = "billy";
			reference = new PredefinedTeamInfo(0, "编队1", "全配队通用", list2);
			ref PredefinedTeamInfo reference2 = ref span[1];
			num2 = 3;
			List<string> list3 = new List<string>(num2);
			CollectionsMarshal.SetCount(list3, num2);
			Span<string> span3 = CollectionsMarshal.AsSpan(list3);
			span3[0] = "unknown";
			span3[1] = "unknown";
			span3[2] = "unknown";
			reference2 = new PredefinedTeamInfo(1, "编队2", "手动配置", list3);
			ref PredefinedTeamInfo reference3 = ref span[2];
			num2 = 3;
			List<string> list4 = new List<string>(num2);
			CollectionsMarshal.SetCount(list4, num2);
			Span<string> span4 = CollectionsMarshal.AsSpan(list4);
			span4[0] = "unknown";
			span4[1] = "unknown";
			span4[2] = "unknown";
			reference3 = new PredefinedTeamInfo(2, "编队3", "全配队通用", list4);
			ref PredefinedTeamInfo reference4 = ref span[3];
			num2 = 3;
			List<string> list5 = new List<string>(num2);
			CollectionsMarshal.SetCount(list5, num2);
			Span<string> span5 = CollectionsMarshal.AsSpan(list5);
			span5[0] = "unknown";
			span5[1] = "unknown";
			span5[2] = "unknown";
			reference4 = new PredefinedTeamInfo(3, "编队4", "全配队通用", list5);
			teamConfig.TeamList = list;
			dictionary["team_list"] = teamConfig.TeamList;
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("team", dictionary));
			FrontierPredefinedTeamPage zzzPredefinedTeamPage = new FrontierPredefinedTeamPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzPredefinedTeamPage.OnPageShown();
			Assert.Equal("predefined_team_checker", zzzPredefinedTeamPage.RunPanel.SelectedAppId);
			Assert.Equal(20, zzzPredefinedTeamPage.Teams.Count);
			Assert.Equal("编队1", zzzPredefinedTeamPage.Teams[0].Name);
			ZzzPredefinedTeamRowModel zzzPredefinedTeamRowModel = zzzPredefinedTeamPage.Teams[1];
			zzzPredefinedTeamRowModel.Name = "速刷队";
			zzzPredefinedTeamRowModel.SelectedAutoBattle = new ZzzPredefinedTeamOption("自定义战斗", "自定义战斗");
			zzzPredefinedTeamRowModel.SelectedAgent1 = new ZzzPredefinedTeamOption("艾莲", "ellen");
			zzzPredefinedTeamRowModel.SelectedAgent2 = new ZzzPredefinedTeamOption("丽娜", "rina");
			zzzPredefinedTeamRowModel.SelectedAgent3 = new ZzzPredefinedTeamOption("苍角", "soukaku");
			zzzPredefinedTeamPage.SaveTeam(zzzPredefinedTeamRowModel);
			List<PredefinedTeamInfo> list6 = Assert.IsType<List<PredefinedTeamInfo>>(fakeBackend.GetConfigScope("team").Value.Values["team_list"]);
			Assert.Equal(20, list6.Count);
			Assert.Equal("速刷队", list6[1].Name);
			Assert.Equal("自定义战斗", list6[1].AutoBattle);
			num = 3;
			List<string> list7 = new List<string>(num);
			CollectionsMarshal.SetCount(list7, num);
			Span<string> span6 = CollectionsMarshal.AsSpan(list7);
			span6[0] = "ellen";
			span6[1] = "rina";
			span6[2] = "soukaku";
			Assert.Equal<List<string>>(list7, list6[1].AgentIdList);
			zzzPredefinedTeamPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("predefined_team_checker", fakeBackend.LastStartRequest?.AppId);
			zzzPredefinedTeamPage.RunPanel.InvokeStopActionAsync().GetAwaiter().GetResult();
			FrontierMouseSensitivityCheckerPage zzzMouseSensitivityCheckerPage = new FrontierMouseSensitivityCheckerPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzMouseSensitivityCheckerPage.OnPageShown();
			Assert.Equal("one-dragon-sensitivity", zzzMouseSensitivityCheckerPage.PageModel.Key);
			Assert.Equal("mouse_sensitivity_checker", zzzMouseSensitivityCheckerPage.RunPanel.SelectedAppId);
			zzzMouseSensitivityCheckerPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("mouse_sensitivity_checker", fakeBackend.LastStartRequest?.AppId);
		});
	}

	/// <summary>
	/// 独立运行页应按默认组过滤显示并保留未注册配置项，保存选择后用 one_dragon group 启动。
	/// </summary>
	[Fact]
	public void StandaloneRunPageWritesListSelectionAndRunIntent()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, new ZzzAppDto[4]
			{
				new ZzzAppDto("coffee", "咖啡店", DefaultGroup: true, NeedNotify: true, RunAvailable: true, SupportsGroup: true, new string[] { "coffee" }),
				new ZzzAppDto("charge_plan", "体力刷本", DefaultGroup: true, NeedNotify: true, RunAvailable: true, SupportsGroup: true, new string[] { "charge-plan" }),
				new ZzzAppDto("one_dragon", "一条龙", DefaultGroup: false, NeedNotify: false),
				new ZzzAppDto("predefined_team_checker", "预备编队检查", DefaultGroup: false, NeedNotify: false)
			});
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("standalone-app", new Dictionary<string, object>
			{
				["app_list"] = new List<string> { "missing_app", "charge_plan" },
				["active_app_id"] = "missing_app"
			}));
			ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandaloneAppRunPage zzzStandaloneAppRunPage = new ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandaloneAppRunPage(fakeBackend, new ZzzGuiRunIntentService());
			zzzStandaloneAppRunPage.OnPageShown();
			Assert.Equal(new ReadOnlySpan<string>("charge_plan"), zzzStandaloneAppRunPage.AppRows.Select(row => row.AppId).ToArray());
			Assert.Equal("charge_plan", zzzStandaloneAppRunPage.SelectedAppId);
			Assert.Equal("charge_plan", zzzStandaloneAppRunPage.RunPanel.SelectedAppId);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = fakeBackend.GetConfigScope("standalone-app");
			int num = 2;
			List<string> list = new List<string>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<string> span = CollectionsMarshal.AsSpan(list);
			span[0] = "missing_app";
			span[1] = "charge_plan";
			Assert.Equal(list, Assert.IsType<List<string>>(configScope.Value.Values["app_list"]));
			Assert.Equal("charge_plan", configScope.Value.Values["active_app_id"]);
			zzzStandaloneAppRunPage.SelectAppForTest("charge_plan");
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope2 = fakeBackend.GetConfigScope("standalone-app");
			num = 2;
			List<string> list2 = new List<string>(num);
			CollectionsMarshal.SetCount(list2, num);
			Span<string> span2 = CollectionsMarshal.AsSpan(list2);
			span2[0] = "missing_app";
			span2[1] = "charge_plan";
			Assert.Equal(list2, Assert.IsType<List<string>>(configScope2.Value.Values["app_list"]));
			zzzStandaloneAppRunPage.AddAppForTest("coffee");
			zzzStandaloneAppRunPage.MoveAppForTest("coffee", -1);
			zzzStandaloneAppRunPage.SelectAppForTest("coffee");
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope3 = fakeBackend.GetConfigScope("standalone-app");
			num = 2;
			List<string> list3 = new List<string>(num);
			CollectionsMarshal.SetCount(list3, num);
			Span<string> span3 = CollectionsMarshal.AsSpan(list3);
			span3[0] = "coffee";
			span3[1] = "charge_plan";
			Assert.Equal(list3, Assert.IsType<List<string>>(configScope3.Value.Values["app_list"]));
			Assert.Equal("coffee", configScope3.Value.Values["active_app_id"]);
			Assert.Equal("coffee", zzzStandaloneAppRunPage.RunPanel.SelectedAppId);
			zzzStandaloneAppRunPage.RunPanel.InvokePrimaryActionAsync().GetAwaiter().GetResult();
			Assert.Equal("coffee", fakeBackend.LastStartRequest?.AppId);
			Assert.Equal("one_dragon", fakeBackend.LastStartRequest?.GroupId);
			zzzStandaloneAppRunPage.RunPanel.InvokeStopActionAsync().GetAwaiter().GetResult();
			zzzStandaloneAppRunPage.RemoveAppForTest("coffee");
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope4 = fakeBackend.GetConfigScope("standalone-app");
			num = 1;
			List<string> list4 = new List<string>(num);
			CollectionsMarshal.SetCount(list4, num);
			CollectionsMarshal.AsSpan(list4)[0] = "charge_plan";
			Assert.Equal(list4, Assert.IsType<List<string>>(configScope4.Value.Values["app_list"]));
			Assert.Equal("charge_plan", configScope4.Value.Values["active_app_id"]);
		});
	}

	/// <summary>
	/// 独立运行真实空态不应注入默认应用，页面隐藏后应取消实例和运行事件订阅。
	/// </summary>
	[Fact]
	public void StandaloneRunPageKeepsRealEmptyStateAndUnsubscribesWhenHidden()
	{
		EnsureAvaloniaServices();
		FakeBackend backend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, new ZzzAppDto[2]
		{
			new ZzzAppDto("coffee", "咖啡店", DefaultGroup: true, NeedNotify: true),
			new ZzzAppDto("charge_plan", "体力刷本", DefaultGroup: true, NeedNotify: true)
		});
		backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("standalone-app", new Dictionary<string, object>
		{
			["app_list"] = new List<string>(),
			["active_app_id"] = string.Empty
		}));
		ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandaloneAppRunPage page = null;
		RunOnUiThread(delegate
		{
			page = new ZzzOd.Gui.Views.FrontierPages.Standalone.FrontierStandaloneAppRunPage(backend, new ZzzGuiRunIntentService());
			page.OnPageShown();
			Assert.Empty(page.AppRows);
			Assert.Null(page.SelectedAppId);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backend.GetConfigScope("standalone-app");
			Assert.Empty(Assert.IsType<List<string>>(configScope.Value.Values["app_list"]));
			Assert.Equal(string.Empty, configScope.Value.Values["active_app_id"]);
			Assert.True(page.FindControl<Button>("AddAppButton").IsEnabled);
			Assert.True(backend.EventSubscriberCount > 0);
			page.OnPageHidden();
			page.DisposePage();
		});
		Assert.Equal(0, backend.EventSubscriberCount);
	}

	/// <summary>
	/// 账户页应管理实例列表，并把当前账户字段保存到活动实例配置。
	/// </summary>
	[Fact]
	public void AccountsPageWritesInstanceListAndCurrentAccountFields()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, null, new ZzzInstanceDto[2]
			{
				new ZzzInstanceDto(0, "主号", Active: true, "config/00"),
				new ZzzInstanceDto(1, "副号", Active: false, "config/01")
			});
			ZzzFrontierAccountsPage zzzAccountsPage = new ZzzFrontierAccountsPage(fakeBackend);
			zzzAccountsPage.OnPageShown();
			ZzzInstanceManagementPage instanceManagement = zzzAccountsPage.InstanceManagement;
			Assert.Equal("accounts", instanceManagement.PageModel.Key);
			Assert.True(instanceManagement.CanSwitch);
			Assert.Equal((ReadOnlySpan<int>)new int[2] { 0, 1 }, instanceManagement.Instances.Select((ZzzInstanceDto instance) => instance.Index).ToArray());
			instanceManagement.UpdateInstanceForTest(1, "副号改名", false);
			Assert.Equal("副号改名", fakeBackend.GetInstances().Value.Single((ZzzInstanceDto instance) => instance.Index == 1).Name);
			Assert.False(fakeBackend.GetInstances().Value.Single((ZzzInstanceDto instance) => instance.Index == 1).ActiveInOneDragon);
			instanceManagement.AddInstanceForTest();
			Assert.Contains((IEnumerable<ZzzInstanceDto>)fakeBackend.GetInstances().Value, (Predicate<ZzzInstanceDto>)((ZzzInstanceDto instance) => instance.Index == 2));
			instanceManagement.ActivateInstanceForTest(1);
			Assert.Equal(1, fakeBackend.GetCurrentInstance().Value.Index);
			instanceManagement.DeleteInstanceForTest(0);
			Assert.DoesNotContain((IEnumerable<ZzzInstanceDto>)fakeBackend.GetInstances().Value, (Predicate<ZzzInstanceDto>)((ZzzInstanceDto instance) => instance.Index == 0));
			Assert.False(instanceManagement.LoginInstanceForTest(1).Success);
			ZzzCurrentAccountSettingsPage currentAccountSettings = zzzAccountsPage.CurrentAccountSettings;
			currentAccountSettings.OnPageShown();
			Assert.Equal(1, currentAccountSettings.ActiveInstanceIndex);
			currentAccountSettings.SaveStringForTest("game_path", "D:\\Games\\ZenlessZoneZero.exe");
			currentAccountSettings.SaveBoolForTest("use_custom_win_title", value: true);
			currentAccountSettings.SaveStringForTest("custom_win_title", "ZZZ Custom");
			currentAccountSettings.SaveStringForTest("account", "user@example.com");
			currentAccountSettings.SaveStringForTest("password", "secret");
			currentAccountSettings.SetGameRegionForTest("cn_b");
			currentAccountSettings.SaveStringForTest("bilibili_account_name", "B服用户");
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = fakeBackend.GetConfigScope("instance", 1);
			Assert.Equal("D:\\Games\\ZenlessZoneZero.exe", configScope.Value.Values["game_path"]);
			Assert.True((bool)configScope.Value.Values["use_custom_win_title"]);
			Assert.Equal("ZZZ Custom", configScope.Value.Values["custom_win_title"]);
			Assert.Equal("cn_b", configScope.Value.Values["game_region"]);
			Assert.Equal("user@example.com", configScope.Value.Values["account"]);
			Assert.Equal("secret", configScope.Value.Values["password"]);
			Assert.Equal("B服用户", configScope.Value.Values["bilibili_account_name"]);
			Assert.False(currentAccountSettings.AccountPasswordVisible);
			Assert.True(currentAccountSettings.BilibiliVisible);
		});
	}

	/// <summary>
	/// 运行状态活跃时账户页应禁用实例切换相关操作并显示原因。
	/// </summary>
	[Fact]
	public void AccountsPageBlocksUnsafeInstanceSwitchingWhileRunIsActive()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend backend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, null, new ZzzInstanceDto[2]
			{
				new ZzzInstanceDto(0, "主号", Active: true, "config/00"),
				new ZzzInstanceDto(1, "副号", Active: false, "config/01")
			}, new ZzzRunStatusDto(ZzzRunState.Running, "coffee", "咖啡店"));
			ZzzFrontierAccountsPage zzzAccountsPage = new ZzzFrontierAccountsPage(backend);
			zzzAccountsPage.OnPageShown();
			Assert.False(zzzAccountsPage.InstanceManagement.CanSwitch);
			Assert.Contains("Running", zzzAccountsPage.InstanceManagement.BlockedReason, StringComparison.Ordinal);
			Assert.False(zzzAccountsPage.InstanceManagement.ActivateInstanceForTest(1).Success);
			Assert.False(zzzAccountsPage.InstanceManagement.AddInstanceForTest().Success);
			Assert.False(zzzAccountsPage.InstanceManagement.DeleteInstanceForTest(1).Success);
			Assert.Contains<string>("一条龙运行中，不能切换实例。", EnumerateText(zzzAccountsPage), StringComparer.Ordinal);
		});
	}

	/// <summary>
	/// 页面模型应记录可见控件、状态输出和不可用原因。
	/// </summary>
	[Fact]
	public void PageModelsExposeControlsStatusAndUnavailableState()
	{
		ZzzPageModel zzzPageModel = new ZzzPageModel("settings-resource-download", "资源下载").AddControl(new ZzzPageControlModel("ocr", "OCR 模型", "PP-OCR", Visible: true, Enabled: false, "服务未接入")).AddStatus(new ZzzPageStatusModel(ZzzPageStatusSeverity.Warning, "暂不开放", "资源下载服务未接入"));
		ZzzUnavailablePageModel unavailable = new ZzzUnavailablePageModel("暂不开放", "后端服务尚未就绪", "ResourceDownloadService");
		Assert.Equal("settings-resource-download", zzzPageModel.Key);
		Assert.Single(zzzPageModel.Controls);
		Assert.False(zzzPageModel.Controls[0].Enabled);
		Assert.Equal("服务未接入", zzzPageModel.Controls[0].ValidationMessage);
		Assert.Single(zzzPageModel.Statuses);
		Assert.Equal(ZzzPageStatusSeverity.Warning, unavailable.ToStatus().Severity);
		Assert.Contains("ResourceDownloadService", unavailable.ToStatus().Message, StringComparison.Ordinal);
		RunOnUiThread(delegate
		{
			FAInfoBar infoBar = Assert.IsType<FAInfoBar>(unavailable.ToControl());
			Assert.Equal(FAInfoBarSeverity.Warning, infoBar.Severity);
			Assert.True(infoBar.IsOpen);
		});
	}

	/// <summary>
	/// 设置项应通过绑定保存用户修改，并使用官方 SettingsExpanderItem。
	/// </summary>
	[Fact]
	public void SwitchSettingCardSavesBindingAndUsesFluentCardClass()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			bool value = false;
			ZzzSwitchSettingCard zzzSwitchSettingCard = new ZzzSwitchSettingCard("后台运行", null, new ZzzDelegateConfigBinding<bool>(() => value, delegate(bool next)
			{
				value = next;
			}));
			ToggleSwitch toggleSwitch = Assert.IsType<ToggleSwitch>(zzzSwitchSettingCard.SettingContent);
			toggleSwitch.IsChecked = true;
			Assert.True(value);
			Assert.IsAssignableFrom<FASettingsExpanderItem>(zzzSwitchSettingCard);
			Assert.DoesNotContain("zzz-card", (IEnumerable<string>)zzzSwitchSettingCard.Classes);
		});
	}

	/// <summary>
	/// 设置卡应公开禁用、等待、错误和校验状态。
	/// </summary>
	[Fact]
	public void SettingCardExposesDisabledWaitingErrorAndValidationState()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzTextSettingCard zzzTextSettingCard = new ZzzTextSettingCard("代理", "网络代理地址");
			zzzTextSettingCard.SetDisabled(disabled: true, "运行中不可修改");
			Assert.False(zzzTextSettingCard.IsEnabled);
			Assert.Equal(ZzzSettingCardStatus.Disabled, zzzTextSettingCard.Status);
			Assert.Equal("运行中不可修改", zzzTextSettingCard.StatusText);
			Assert.Contains("运行中不可修改", ((FASettingsExpanderItem)zzzTextSettingCard).Description?.ToString(), StringComparison.Ordinal);
			zzzTextSettingCard.SetDisabled(disabled: false);
			zzzTextSettingCard.SetWaiting(waiting: true, "保存中");
			Assert.True(zzzTextSettingCard.IsEnabled);
			Assert.False(zzzTextSettingCard.SettingContent.IsEnabled);
			Assert.Equal(ZzzSettingCardStatus.Waiting, zzzTextSettingCard.Status);
			Assert.Contains("保存中", ((FASettingsExpanderItem)zzzTextSettingCard).Description?.ToString(), StringComparison.Ordinal);
			zzzTextSettingCard.SetWaiting(waiting: false);
			zzzTextSettingCard.SetError("代理地址无效");
			Assert.Equal(ZzzSettingCardStatus.Error, zzzTextSettingCard.Status);
			Assert.Equal("代理地址无效", zzzTextSettingCard.StatusText);
			Assert.Contains("代理地址无效", ((FASettingsExpanderItem)zzzTextSettingCard).Description?.ToString(), StringComparison.Ordinal);
			zzzTextSettingCard.SetValidation("需要填写端口");
			Assert.Equal(ZzzSettingCardStatus.Validation, zzzTextSettingCard.Status);
			Assert.Equal("需要填写端口", zzzTextSettingCard.StatusText);
			Assert.Contains("需要填写端口", ((FASettingsExpanderItem)zzzTextSettingCard).Description?.ToString(), StringComparison.Ordinal);
			zzzTextSettingCard.ClearStatus();
			Assert.Equal(ZzzSettingCardStatus.Normal, zzzTextSettingCard.Status);
			Assert.Equal(string.Empty, zzzTextSettingCard.StatusText);
			Assert.True(zzzTextSettingCard.SettingContent.IsEnabled);
		});
	}

	/// <summary>
	/// 分段导航应公开选择状态、可聚焦状态、自动化名称和生命周期转发。
	/// </summary>
	[Fact]
	public void SegmentedNavigationExposesSelectionFocusAutomationAndLifecycle()
	{
		EnsureAvaloniaServices();
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB");
		try
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB", "第二页");
			RunOnUiThread(delegate
			{
				LifecycleControl lifecycleControl = new LifecycleControl();
				LifecycleControl lifecycleControl2 = new LifecycleControl();
				ZzzPivotPage zzzPivotPage = new ZzzPivotPage(new ZzzPivotPageItem[2]
				{
					new ZzzPivotPageItem("第一页", lifecycleControl),
					new ZzzPivotPageItem("第二页", lifecycleControl2)
				});
				Assert.Equal("TabControl", zzzPivotPage.NavigationTargetKind);
				Assert.Equal(new string[2] { "第一页", "第二页" }, zzzPivotPage.ItemHeaders);
				Assert.Equal(new string[2] { "第一页 选项卡", "第二页 选项卡" }, zzzPivotPage.ItemAutomationNames);
				Assert.All(zzzPivotPage.ItemFocusableStates, Assert.True);
				Assert.Equal(1, zzzPivotPage.SelectedIndex);
				Assert.Equal("第二页", zzzPivotPage.SelectedHeader);
				Assert.Same(lifecycleControl2, zzzPivotPage.SelectedContent);
				Assert.Same(lifecycleControl2, zzzPivotPage.FAFrame.Content);
				zzzPivotPage.OnPageShown();
				Assert.Equal(1, lifecycleControl2.Shown);
				zzzPivotPage.SelectedIndex = 0;
				Assert.Equal("第一页", zzzPivotPage.SelectedHeader);
				Assert.Same(lifecycleControl, zzzPivotPage.FAFrame.Content);
				Assert.Equal(1, lifecycleControl2.Left);
				Assert.Equal(1, lifecycleControl2.Hidden);
				Assert.Equal(1, lifecycleControl.Shown);
				Assert.True(zzzPivotPage.SelectByHeader("第二页"));
				Assert.Equal(1, zzzPivotPage.SelectedIndex);
				Assert.True(zzzPivotPage.FocusSegment(1));
				Assert.False(zzzPivotPage.FocusSegment(9));
				zzzPivotPage.OnPageHidden();
				zzzPivotPage.OnPageLeave();
				zzzPivotPage.DisposePage();
				Assert.Equal(2, lifecycleControl2.Hidden);
				Assert.Equal(2, lifecycleControl2.Left);
				Assert.Equal(1, lifecycleControl.Disposed);
				Assert.Equal(1, lifecycleControl2.Disposed);
			});
		}
		finally
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB", environmentVariable);
		}
	}

	/// <summary>
	/// 日志面应使用主题化控件和工具栏。
	/// </summary>
	[Fact]
	public void LogDisplayCardUsesFluentSurface()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzLogDisplayCard zzzLogDisplayCard = new ZzzLogDisplayCard(new FakeBackend(), 2);
			Assert.IsAssignableFrom<UserControl>(zzzLogDisplayCard);
			Assert.DoesNotContain("zzz-card", (IEnumerable<string>)zzzLogDisplayCard.Classes);
			Assert.NotNull(FindDescendant<ScrollViewer>(zzzLogDisplayCard));
			Assert.Equal(string.Empty, zzzLogDisplayCard.DisplayText);
			Assert.Equal("已停止", zzzLogDisplayCard.StatusText);
			zzzLogDisplayCard.Start();
			Assert.True(zzzLogDisplayCard.IsActive);
			Assert.Equal("跟随中", zzzLogDisplayCard.StatusText);
			zzzLogDisplayCard.AppendLine("one");
			zzzLogDisplayCard.AppendLine("two");
			Assert.Equal("one" + Environment.NewLine + "two", zzzLogDisplayCard.DisplayText);
			zzzLogDisplayCard.SetFollowing(following: false);
			zzzLogDisplayCard.AppendLine("three");
			Assert.False(zzzLogDisplayCard.IsFollowing);
			Assert.Equal(new string[2] { "two", "three" }, zzzLogDisplayCard.Lines);
			Assert.Equal("two" + Environment.NewLine + "three", zzzLogDisplayCard.DisplayText);
			zzzLogDisplayCard.SetFollowing(following: true);
			Assert.Equal("two" + Environment.NewLine + "three", zzzLogDisplayCard.DisplayText);
			zzzLogDisplayCard.Pause();
			Assert.Equal("已暂停", zzzLogDisplayCard.StatusText);
		});
	}

	/// <summary>
	/// 日志数量超过宿主高度时，视口保持边界并跟随最后一行。
	/// </summary>
	[Fact]
	public void LogDisplayCardKeepsFiniteViewportAndFollowsLatestLine()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			EnsureFluentTheme();
			ZzzLogDisplayCard card = new ZzzLogDisplayCard(new FakeBackend(), 64, TimeSpan.FromMilliseconds(20));
			AvaloniaWindow host = new AvaloniaWindow
			{
				Width = 320,
				Height = 140,
				WindowDecorations = WindowDecorations.None,
				ShowInTaskbar = false,
				ShowActivated = false,
				Content = card,
			};
			try
			{
				host.Show();
				card.Start();
				for (int index = 0; index < 64; index++)
				{
					card.AppendLine($"line-{index}");
				}

				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				ScrollViewer viewport = card.ScrollViewport;
				double maximum = Math.Max(0d, viewport.Extent.Height - viewport.Viewport.Height);
				Assert.True(viewport.Bounds.Height <= host.ClientSize.Height + 1d);
				Assert.True(maximum > 0d);
				Assert.InRange(viewport.Offset.Y, Math.Max(0d, maximum - 1d), maximum + 1d);
				Assert.EndsWith("line-63", card.DisplayText, StringComparison.Ordinal);
			}
			finally
			{
				card.DisposePage();
				host.Close();
			}
		});
	}

	/// <summary>
	/// 用户查看历史日志时保持偏移，回到底部或空闲后恢复跟随。
	/// </summary>
	[Fact]
	public void LogDisplayCardPausesAndResumesFollowingAroundUserScroll()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			EnsureFluentTheme();
			ZzzLogDisplayCard card = new ZzzLogDisplayCard(new FakeBackend(), 64, TimeSpan.FromMilliseconds(20));
			AvaloniaWindow host = new AvaloniaWindow
			{
				Width = 320,
				Height = 140,
				WindowDecorations = WindowDecorations.None,
				ShowInTaskbar = false,
				ShowActivated = false,
				Content = card,
			};
			try
			{
				host.Show();
				card.Start();
				for (int index = 0; index < 64; index++)
				{
					card.AppendLine($"line-{index}");
				}

				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				ScrollViewer viewport = card.ScrollViewport;
				viewport.Offset = new Vector(0d, 0d);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				card.PauseFollowingUntilIdle();
				card.AppendLine("history-view");
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				Assert.False(card.IsFollowing);
				Assert.Equal(0d, viewport.Offset.Y, precision: 3);
				Assert.EndsWith("history-view", card.DisplayText, StringComparison.Ordinal);

				double maximum = Math.Max(0d, viewport.Extent.Height - viewport.Viewport.Height);
				viewport.Offset = new Vector(0d, maximum);
				Dispatcher.UIThread.RunJobs();
				Assert.True(card.IsFollowing);

				card.PauseFollowingUntilIdle();
				TextBox output = card.FindControl<TextBox>("OutputText")!;
				output.SelectionStart = 0;
				output.SelectionEnd = 1;
				Thread.Sleep(35);
				Dispatcher.UIThread.RunJobs();
				Assert.False(card.IsFollowing);

				output.SelectionEnd = output.SelectionStart;
				card.PauseFollowingUntilIdle();
				Thread.Sleep(35);
				Dispatcher.UIThread.RunJobs();
				Assert.True(card.IsFollowing);
			}
			finally
			{
				card.DisposePage();
				host.Close();
			}
		});
	}

	/// <summary>
	/// 页面隐藏后停止恢复计时器，重新显示时重新订阅并跟随末尾。
	/// </summary>
	[Fact]
	public void LogDisplayCardStopsIdleResumeAcrossPageLifecycle()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend backend = new FakeBackend();
			ZzzLogDisplayCard card = new ZzzLogDisplayCard(backend, 20, TimeSpan.FromMilliseconds(20));
			card.OnPageShown();
			Assert.Equal(1, backend.EventSubscriberCount);
			card.PauseFollowingUntilIdle();
			Assert.True(card.FollowResumeTimerEnabled);

			card.OnPageHidden();
			Assert.False(card.IsActive);
			Assert.False(card.IsFollowing);
			Assert.False(card.FollowResumeTimerEnabled);
			Assert.Equal(0, backend.EventSubscriberCount);
			Thread.Sleep(35);
			Dispatcher.UIThread.RunJobs();
			Assert.False(card.IsFollowing);

			card.OnPageShown();
			Assert.True(card.IsActive);
			Assert.True(card.IsFollowing);
			Assert.Equal(1, backend.EventSubscriberCount);
			card.DisposePage();
			Assert.False(card.FollowResumeTimerEnabled);
			Assert.Equal(0, backend.EventSubscriberCount);
		});
	}

	/// <summary>
	/// 后端事件批量到达时只保留上限范围，并让显示文本与行集合一致。
	/// </summary>
	[Fact]
	public void LogDisplayCardConsumesBatchedBackendEventsWithinMaxLines()
	{
		EnsureAvaloniaServices();
		FakeBackend backend = new FakeBackend();
		ZzzLogDisplayCard? card = null;
		RunOnUiThread(delegate
		{
			card = new ZzzLogDisplayCard(backend, 3);
			card.Start();
			for (int index = 0; index < 5; index++)
			{
				backend.PublishEvent(
					"log.appended",
					new ZzzLogEntryDto(DateTimeOffset.UtcNow, "Information", "test", $"event-{index}", null));
			}
		});

		Thread.Sleep(180);
		RunOnUiThread(delegate
		{
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs();
			Assert.NotNull(card);
			Assert.Equal(3, card.Lines.Count);
			Assert.Contains("event-2", card.Lines[0], StringComparison.Ordinal);
			Assert.Contains("event-3", card.Lines[1], StringComparison.Ordinal);
			Assert.Contains("event-4", card.Lines[2], StringComparison.Ordinal);
			Assert.EndsWith("event-4", card.DisplayText, StringComparison.Ordinal);
			card.DisposePage();
		});
	}

	/// <summary>
	/// Overlay 超长日志保持在固定窗口内，并自动显示最后一行。
	/// </summary>
	[Fact]
	public void OverlayInfoPanelKeepsLongLogInsideViewportAndScrollsToEnd()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			EnsureFluentTheme();
			AvaloniaWindow owner = new AvaloniaWindow
			{
				Width = 8,
				Height = 8,
				ShowInTaskbar = false,
				ShowActivated = false,
				WindowDecorations = WindowDecorations.None,
			};
			ZzzOverlayInfoPanelWindow panel = new ZzzOverlayInfoPanelWindow();
			try
			{
				owner.Show();
				ZzzOverlayPanelSettings panelSettings = new ZzzOverlayPanelSettings("log", "日志面板", true, 100d, 100d, 480d, 200d)
				{
					IsFreeMode = true,
				};
				ZzzOverlayGuiSettings settings = new ZzzOverlayGuiSettings
				{
					LayoutEditMode = false,
					ClickThrough = true,
					PreventCapture = false,
				};
				ZzzWindowStatusDto gameWindow = new ZzzWindowStatusDto("test", true, true, false, 100, 100, 1140, 760);
				panel.ApplyConfiguration(panelSettings, settings, gameWindow, forceGeometry: true);
				panel.Show(owner);
				double width = panel.Width;
				double height = panel.Height;
				panel.UpdateContent(string.Join(Environment.NewLine, Enumerable.Range(0, 80).Select(index => $"overlay-line-{index}")));
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

				ScrollViewer viewport = panel.ContentScrollViewer;
				double maximum = Math.Max(0d, viewport.Extent.Height - viewport.Viewport.Height);
				Assert.Equal(width, panel.Width, precision: 3);
				Assert.Equal(height, panel.Height, precision: 3);
				Assert.True(viewport.Bounds.Height <= panel.ClientSize.Height + 1d);
				Assert.True(maximum > 0d);
				Assert.InRange(viewport.Offset.Y, Math.Max(0d, maximum - 1d), maximum + 1d);
				Assert.EndsWith("overlay-line-79", panel.ContentText, StringComparison.Ordinal);
				Assert.False(viewport.IsHitTestVisible);
			}
			finally
			{
				panel.Close();
				owner.Close();
			}
		});
	}

	/// <summary>
	/// 提示条直接使用 FluentAvalonia InfoBar。
	/// </summary>
	[Fact]
	public void InfoBarUsesFluentSeverityActionAndClosableState()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			Button button = new Button
			{
				Content = "处理"
			};
			FAInfoBar infoBar = new FAInfoBar
			{
				Title = "警告",
				Message = "需要处理",
				Severity = FAInfoBarSeverity.Warning,
				IsOpen = true,
				IsClosable = true,
				ActionButton = button,
			};
			Assert.Equal("警告", infoBar.Title);
			Assert.Equal("需要处理", infoBar.Message);
			Assert.Equal(FAInfoBarSeverity.Warning, infoBar.Severity);
			Assert.True(infoBar.IsOpen);
			Assert.True(infoBar.IsClosable);
			Assert.Same(button, infoBar.ActionButton);
		});
	}

	/// <summary>
	/// 命令栏直接使用 FluentAvalonia CommandBar 分组。
	/// </summary>
	[Fact]
	public void CommandBarUsesFluentPrimaryAndSecondaryGroups()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			Button item = new Button
			{
				Content = "保存"
			};
			Button item2 = new Button
			{
				Content = "更多"
			};
			FACommandBar commandBar = new FACommandBar();
			commandBar.PrimaryCommands.Add(new FACommandBarElementContainer { Content = item });
			commandBar.SecondaryCommands.Add(new FACommandBarElementContainer { Content = item2 });
			Assert.Single(commandBar.PrimaryCommands);
			Assert.Single(commandBar.SecondaryCommands);
			Assert.IsType<FACommandBarElementContainer>(commandBar.PrimaryCommands[0]);
			Assert.IsType<FACommandBarElementContainer>(commandBar.SecondaryCommands[0]);
		});
	}

	/// <summary>
	/// 对话服务应统一创建 ContentDialog 和 TeachingTip。
	/// </summary>
	[Fact]
	public void DialogServiceCreatesFluentDialogAndTeachingTip()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzDialogService zzzDialogService = new ZzzDialogService();
			Button button = new Button
			{
				Content = "目标"
			};
			FAContentDialog contentDialog = zzzDialogService.CreateMessageDialog("标题", "内容");
			FATeachingTip teachingTip = zzzDialogService.CreateTeachingTip("提示", "说明", button);
			Assert.Equal("标题", contentDialog.Title);
			Assert.Equal("确定", contentDialog.CloseButtonText);
			Assert.Equal(FAContentDialogButton.Close, contentDialog.DefaultButton);
			Assert.IsType<TextBlock>(contentDialog.Content);
			Assert.Equal("提示", teachingTip.Title);
			Assert.Equal("说明", teachingTip.Subtitle);
			Assert.Same(button, teachingTip.Target);
			Assert.Equal("知道了", teachingTip.CloseButtonContent);
			Assert.True(teachingTip.IsOpen);
			teachingTip.IsOpen = false;
		});
	}

	/// <summary>
	/// 页面生命周期服务应向旧页面发送离开和隐藏，向新页面发送显示。
	/// </summary>
	[Fact]
	public void PageLifecycleServiceCallsExpectedHooks()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			ZzzPageLifecycleService zzzPageLifecycleService = new ZzzPageLifecycleService();
			LifecycleControl lifecycleControl = new LifecycleControl();
			LifecycleControl lifecycleControl2 = new LifecycleControl();
			zzzPageLifecycleService.NavigateTo(lifecycleControl);
			zzzPageLifecycleService.NavigateTo(lifecycleControl);
			zzzPageLifecycleService.NavigateTo(lifecycleControl2);
			zzzPageLifecycleService.DisposeCurrent();
			Assert.Equal(1, lifecycleControl.Shown);
			Assert.Equal(1, lifecycleControl.Left);
			Assert.Equal(1, lifecycleControl.Hidden);
			Assert.Equal(0, lifecycleControl.Disposed);
			Assert.Equal(1, lifecycleControl2.Shown);
			Assert.Equal(1, lifecycleControl2.Left);
			Assert.Equal(1, lifecycleControl2.Hidden);
			Assert.Equal(1, lifecycleControl2.Disposed);
		});
	}

	/// <summary>
	/// 设置绑定适配器应读取并保存委托值。
	/// </summary>
	[Fact]
	public void ConfigBindingAdapterReadsAndSavesValue()
	{
		string value = "old";
		ZzzDelegateConfigBinding<string> zzzDelegateConfigBinding = new ZzzDelegateConfigBinding<string>(() => value, delegate(string next)
		{
			value = next;
		});
		Assert.Equal("old", zzzDelegateConfigBinding.Read());
		zzzDelegateConfigBinding.Save("new");
		Assert.Equal("new", zzzDelegateConfigBinding.Read());
	}

	/// <summary>
	/// 后端配置绑定应通过 .NET 业务门面读写真实配置 scope。
	/// </summary>
	[Fact]
	public void BackendConfigBindingReadsAndSavesScopeValue()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendConfigBinding<bool> zzzBackendConfigBinding = new ZzzBackendConfigBinding<bool>(backendHarness.Backend, "game", "background_mode", fallback: false);
		Assert.False(zzzBackendConfigBinding.Read());
		zzzBackendConfigBinding.Save(value: true);
		Assert.True(zzzBackendConfigBinding.Read());
	}

	/// <summary>
	/// 证据选择应稳定解析页面、子页、主题、窗口尺寸和开发工具模式。
	/// </summary>
	[Fact]
	public void EvidenceSelectionParsesDeterministicEnvironment()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE");
		string environmentVariable2 = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB");
		string environmentVariable3 = Environment.GetEnvironmentVariable("ZZZOD_GUI_THEME");
		string environmentVariable4 = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PANE");
		string environmentVariable5 = Environment.GetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_SIZE");
		string environmentVariable6 = Environment.GetEnvironmentVariable("ZZZOD_GUI_DEV_MODE");
		try
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE", "settings");
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB", "脚本环境");
			Environment.SetEnvironmentVariable("ZZZOD_GUI_THEME", "Dark");
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PANE", "compact");
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_SIZE", "1280x720");
			Environment.SetEnvironmentVariable("ZZZOD_GUI_DEV_MODE", "1");
			ZzzGuiEvidenceSelection zzzGuiEvidenceSelection = ZzzGuiEvidenceSelection.FromEnvironment();
			Assert.Equal("settings", zzzGuiEvidenceSelection.Page);
			Assert.Equal("脚本环境", zzzGuiEvidenceSelection.Tab);
			Assert.Equal("Dark", zzzGuiEvidenceSelection.Theme);
			Assert.Equal("compact", zzzGuiEvidenceSelection.Pane);
			Assert.Equal(1280.0, zzzGuiEvidenceSelection.Width);
			Assert.Equal(720.0, zzzGuiEvidenceSelection.Height);
			Assert.True(zzzGuiEvidenceSelection.DevToolsEnabled);
		}
		finally
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PAGE", environmentVariable);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_TAB", environmentVariable2);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_THEME", environmentVariable3);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_PANE", environmentVariable4);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_EVIDENCE_SIZE", environmentVariable5);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_DEV_MODE", environmentVariable6);
		}
	}

	/// <summary>
	/// 首页预检查应只核对 BaselineParity 的游戏路径和闪光识别模型文件。
	/// </summary>
	[Fact]
	public void HomePreFlightMatchesPythonGamePathAndFlashClassifierFiles()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ModelConfig modelConfig = new ModelConfig();
		ZzzDashboardReadinessService zzzDashboardReadinessService = new ZzzDashboardReadinessService(backendHarness.Backend, new ZzzRunRoot(backendHarness.RunRoot));
		ZzzDashboardReadinessResult zzzDashboardReadinessResult = zzzDashboardReadinessService.Check();
		Assert.False(zzzDashboardReadinessResult.Ready);
		Assert.Equal(2, zzzDashboardReadinessResult.Issues.Count);
		Assert.Equal("未设置游戏路径 - 请在「账户管理 → 多账户管理 → 当前账户设置」中配置", zzzDashboardReadinessResult.Issues[0].Message);
		Assert.Equal("accounts", zzzDashboardReadinessResult.Issues[0].TargetNavigationKey);
		Assert.Equal("闪光识别模型未下载 - 请在「设置 → 资源下载」中下载", zzzDashboardReadinessResult.Issues[1].Message);
		Assert.Equal("settings-resource-download", zzzDashboardReadinessResult.Issues[1].TargetNavigationKey);
		backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("instance", new Dictionary<string, object> { ["game_path"] = Path.Combine(backendHarness.RunRoot, "ZenlessZoneZero.exe") }, 0));
		backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("model", new Dictionary<string, object> { ["flash_classifier"] = modelConfig.FlashClassifier }));
		string[] buffer = new string[5];
		buffer[0] = backendHarness.RunRoot;
		buffer[1] = "assets";
		buffer[2] = "models";
		buffer[3] = "flash_classifier";
		buffer[4] = modelConfig.FlashClassifier;
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "labels.csv"), string.Empty);
		File.WriteAllBytes(Path.Combine(text, "model.onnx"), default(ReadOnlySpan<byte>));
		ZzzDashboardReadinessResult zzzDashboardReadinessResult2 = zzzDashboardReadinessService.Check();
		Assert.True(zzzDashboardReadinessResult2.Ready);
		Assert.Empty(zzzDashboardReadinessResult2.Issues);
	}

	/// <summary>
	/// 首页预检查应要求选中模型目录同时具有 labels.csv 和 model.onnx。
	/// </summary>
	[Fact]
	public void HomePreFlightRequiresBothFilesInSelectedModelDirectory()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ModelConfig modelConfig = new ModelConfig();
		string flashClassifierBackup = modelConfig.FlashClassifierBackup;
		backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("instance", new Dictionary<string, object> { ["game_path"] = Path.Combine(backendHarness.RunRoot, "ZenlessZoneZero.exe") }, 0));
		backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("model", new Dictionary<string, object> { ["flash_classifier"] = flashClassifierBackup }));
		string[] buffer = new string[5];
		buffer[0] = backendHarness.RunRoot;
		buffer[1] = "assets";
		buffer[2] = "models";
		buffer[3] = "flash_classifier";
		buffer[4] = flashClassifierBackup;
		string text = Path.Combine(buffer);
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "labels.csv"), string.Empty);
		ZzzDashboardReadinessService zzzDashboardReadinessService = new ZzzDashboardReadinessService(backendHarness.Backend, new ZzzRunRoot(backendHarness.RunRoot));
		ZzzDashboardReadinessResult zzzDashboardReadinessResult = zzzDashboardReadinessService.Check();
		Assert.False(zzzDashboardReadinessResult.Ready);
		Assert.Single(zzzDashboardReadinessResult.Issues);
		Assert.Contains("闪光识别模型未下载", zzzDashboardReadinessResult.Issues[0].Message, StringComparison.Ordinal);
		File.WriteAllBytes(Path.Combine(text, "model.onnx"), default(ReadOnlySpan<byte>));
		Assert.True(zzzDashboardReadinessService.Check().Ready);
	}

	/// <summary>
	/// 首页模型问题应解析到设置根页的资源下载 Pivot。
	/// </summary>
	[Fact]
	public void ShellNavigationResolvesPreFlightResourceDownloadSubpage()
	{
		ZzzShellNavigationService zzzShellNavigationService = new ZzzShellNavigationService();
		ZzzShellNavigationTarget zzzShellNavigationTarget = zzzShellNavigationService.Resolve("settings-resource-download");
		Assert.Equal("settings", zzzShellNavigationTarget.RootKey);
		Assert.Equal("资源下载", zzzShellNavigationTarget.PivotHeader);
	}

	/// <summary>
	/// 首页媒体主题色使用 BaselineParity 的加权主色相和固定 HSV 规则。
	/// </summary>
	[Fact]
	public void HomeThemeColorMatchesPythonDominantHueAlgorithm()
	{
		using Mat source = new Mat(64, 64, MatType.CV_8UC3, new Scalar(71.0, 112.0, 179.0));
		Color color;
		bool condition = ZzzHomeThemeColorExtractor.TryExtract(source, out color);
		Assert.True(condition);
		Assert.Equal(Color.FromRgb(179, 111, 71), color);
	}

	/// <summary>
	/// 首页媒体服务应按 BaselineParity 配置选择版本、静态、动态、自定义或默认背景。
	/// </summary>
	[Fact]
	public async Task LauncherMediaServiceSelectsPythonBackgroundTypesFromRealConfig()
	{
		using BackendHarness harness = BackendHarness.Create();
		string runRoot = harness.RunRoot;
		try
		{
			ZzzLauncherMediaService service = new ZzzLauncherMediaService(new ZzzRunRoot(runRoot), harness.Backend);
			Directory.CreateDirectory(service.UiDirectory);
			File.WriteAllBytes(Path.Combine(service.UiDirectory, "index.png"), OnePixelPng);
			File.WriteAllBytes(Path.Combine(service.UiDirectory, "version_poster.webp"), OnePixelPng);
			File.WriteAllBytes(Path.Combine(service.UiDirectory, "static_background.webp"), OnePixelPng);
			File.WriteAllBytes(Path.Combine(service.UiDirectory, "dynamic_background.webm"), (ReadOnlySpan<byte>)new byte[4] { 26, 69, 223, 163 });
			string currentFetchTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			harness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object>
			{
				["background_type"] = "none",
				["custom_banner"] = false,
				["last_version_poster_fetch_time"] = currentFetchTime,
				["last_static_background_fetch_time"] = currentFetchTime,
				["last_dynamic_background_fetch_time"] = currentFetchTime
			}));
			IReadOnlyList<ZzzLauncherMediaItem> defaultItems = await service.GetDashboardMediaAsync();
			Assert.Single(defaultItems);
			Assert.Equal(ZzzLauncherMediaKind.DefaultBackground, defaultItems[0].Kind);
			Assert.Equal(Path.Combine(service.UiDirectory, "index.png"), defaultItems[0].LocalPath);
			harness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object> { ["background_type"] = "version_poster" }));
			IReadOnlyList<ZzzLauncherMediaItem> versionPoster = await service.GetDashboardMediaAsync();
			Assert.Single(versionPoster);
			Assert.Equal(ZzzLauncherMediaKind.VersionPoster, versionPoster[0].Kind);
			harness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object> { ["background_type"] = "static_background" }));
			IReadOnlyList<ZzzLauncherMediaItem> staticBackground = await service.GetDashboardMediaAsync();
			Assert.Single(staticBackground);
			Assert.Equal(ZzzLauncherMediaKind.StaticBackground, staticBackground[0].Kind);
			harness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object> { ["background_type"] = "dynamic_background" }));
			IReadOnlyList<ZzzLauncherMediaItem> dynamicBackground = await service.GetDashboardMediaAsync();
			Assert.Single(dynamicBackground);
			Assert.Equal(ZzzLauncherMediaKind.DynamicBackground, dynamicBackground[0].Kind);
			Assert.True(dynamicBackground[0].IsVideo);
			string source = Path.Combine(runRoot, "source.png");
			File.WriteAllBytes(source, OnePixelPng);
			string savedPath = await service.SaveCustomBackgroundAsync(source);
			harness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object> { ["custom_banner"] = true }));
			IReadOnlyList<ZzzLauncherMediaItem> items = await service.GetDashboardMediaAsync();
			ZzzLauncherMediaReadiness readiness = service.GetCachedMediaReadiness();
			string[] buffer = new string[5];
			buffer[0] = runRoot;
			buffer[1] = "custom";
			buffer[2] = "assets";
			buffer[3] = "ui";
			buffer[4] = "banner";
			Assert.Equal(Path.Combine(buffer), savedPath);
			Assert.Single(items);
			Assert.Equal(ZzzLauncherMediaKind.CustomBackground, items[0].Kind);
			Assert.True(readiness.HasCustomBackground);
			Assert.True(readiness.HasVersionPoster);
			Assert.True(readiness.HasStaticBackground);
			Assert.True(readiness.HasDynamicBackground);
			Assert.True(readiness.HasDefaultImage);
			Assert.True(readiness.HasAnyMedia);
		}
		finally
		{
			string source2 = Path.Combine(runRoot, "source.png");
			if (File.Exists(source2))
			{
				File.Delete(source2);
			}
		}
	}

	/// <summary>
	/// 首页公告服务应从真实 project.notice_url 读取三类文章、日期、链接和 Banner。
	/// </summary>
	[Fact]
	public async Task NoticeServiceLoadsRealProjectNoticeUrl()
	{
		using BackendHarness harness = BackendHarness.Create();
		string noticeUrl = "https://one-dragon.com/notice/zzz/notice.json";
		File.WriteAllText(Path.Combine(harness.RunRoot, "config", "project.yml"), "notice_url: " + noticeUrl + "\n");
		ZzzBackendResult<ZzzConfigScopeValuesDto> project = harness.Backend.GetConfigScope("project");
		string configuredUrl = Assert.IsType<string>(project.Value.Values["notice_url"]);
		ZzzNoticeService service = new ZzzNoticeService(new ZzzRunRoot(harness.RunRoot));
		ZzzNoticeLoadResult result = await service.LoadAsync(configuredUrl);
		Assert.True(result.Success, result.Error);
		Assert.NotNull(result.Content);
		Assert.NotEmpty(result.Content.Banners);
		Assert.NotEmpty(result.Content.GameGuides);
		Assert.NotEmpty(result.Content.SoftwareResearch);
		Assert.NotEmpty(result.Content.Announcements);
		Assert.All(result.Content.GameGuides.Concat(result.Content.SoftwareResearch).Concat(result.Content.Announcements), delegate(ZzzNoticePost post)
		{
			Assert.False(string.IsNullOrWhiteSpace(post.Title));
			Assert.False(string.IsNullOrWhiteSpace(post.Date));
			Assert.True(Uri.TryCreate(post.Link, UriKind.Absolute, out Uri _));
		});
		Assert.True(result.Content.GameGuides.Count <= 3);
		Assert.True(result.Content.SoftwareResearch.Count <= 3);
		Assert.True(result.Content.Announcements.Count <= 3);
		ZzzNoticeLoadResult cached = await service.LoadAsync("https://127.0.0.1:1/notice.json");
		Assert.True(cached.Success, cached.Error);
		Assert.True(cached.FromCache);
		Assert.Equal(result.Content.GameGuides, cached.Content.GameGuides);
	}

	/// <summary>
	/// 公告请求失败且没有有效缓存时应返回真实失败信息。
	/// </summary>
	[Fact]
	public async Task NoticeServiceReportsRealFailureWithoutCache()
	{
		using BackendHarness harness = BackendHarness.Create();
		ZzzNoticeService service = new ZzzNoticeService(new ZzzRunRoot(harness.RunRoot));
		ZzzNoticeLoadResult result = await service.LoadAsync("https://127.0.0.1:1/notice.json");
		Assert.False(result.Success);
		Assert.False(string.IsNullOrWhiteSpace(result.Error));
		Assert.Null(result.Content);
		Assert.False(result.FromCache);
	}

	/// <summary>
	/// 业务门面应提供已知配置 scope，并拒绝未知 key。
	/// </summary>
	[Fact]
	public void BackendConfigScopesExposeKnownDescriptorsAndRejectUnknownKeys()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>> configScopes = backendHarness.Backend.GetConfigScopes();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("game", new Dictionary<string, object> { ["not_a_real_key"] = true }));
		Assert.True(configScopes.Success);
		Assert.Contains((IEnumerable<ZzzConfigScopeDescriptorDto>)configScopes.Value, (Predicate<ZzzConfigScopeDescriptorDto>)((ZzzConfigScopeDescriptorDto scope) => scope.Scope == "game"));
		Assert.Contains((IEnumerable<ZzzConfigScopeDescriptorDto>)configScopes.Value, (Predicate<ZzzConfigScopeDescriptorDto>)((ZzzConfigScopeDescriptorDto scope) => scope.Scope == "model" && !scope.InstanceBound));
		Assert.Contains((IEnumerable<ZzzConfigScopeDescriptorDto>)configScopes.Value, (Predicate<ZzzConfigScopeDescriptorDto>)((ZzzConfigScopeDescriptorDto scope) => scope.Scope == "custom" && scope.Writable && !scope.InstanceBound));
		Assert.Contains((IEnumerable<ZzzConfigScopeDescriptorDto>)configScopes.Value, (Predicate<ZzzConfigScopeDescriptorDto>)((ZzzConfigScopeDescriptorDto scope) => scope.Scope == "push" && scope.Writable && !scope.InstanceBound));
		Assert.Contains((IEnumerable<ZzzConfigScopeDescriptorDto>)configScopes.Value, (Predicate<ZzzConfigScopeDescriptorDto>)((ZzzConfigScopeDescriptorDto scope) => scope.Scope == "notify" && scope.Writable && scope.InstanceBound));
		Assert.False(zzzBackendResult.Success);
		Assert.Equal(ZzzBackendErrorCode.Validation, zzzBackendResult.ErrorCode);
		Assert.Contains("not_a_real_key", zzzBackendResult.Error, StringComparison.Ordinal);
	}

	/// <summary>
	/// 配置 scope 保存后应能再读出相同语义值。
	/// </summary>
	[Fact]
	public void BackendConfigScopeRoundTripsPythonYamlKey()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("game", new Dictionary<string, object> { ["background_mode"] = true }));
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("game");
		Assert.True(zzzBackendResult.Success);
		Assert.True(configScope.Success);
		Assert.True((bool)configScope.Value.Values["background_mode"]);
	}

	/// <summary>
	/// 通知 scope 应迁移旧整数等级，并在保存新字段时保留旧键和 schema 标记。
	/// </summary>
	[Fact]
	public void NotifyScopeMigratesLegacyModesAndPreservesUnknownYamlKeys()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		string text = Path.Combine(backendHarness.RunRoot, "config", "00");
		string path = Path.Combine(text, "notify.yml");
		Directory.CreateDirectory(text);
		File.WriteAllText(path, "title: 旧通知\nenable_notify: true\nnotify_on_error: false\nenable_before_notify: false\ncoffee: 1\ncharge_plan: 3\ncustom_marker: keep\n");
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("notify", 0);
		Dictionary<string, NotifyApplicationSetting> dictionary = Assert.IsType<Dictionary<string, NotifyApplicationSetting>>(configScope.Value.Values["applications"]);
		Assert.Equal("finish_only", dictionary["coffee"].Lifecycle);
		Assert.Equal("off", dictionary["coffee"].Detail);
		Assert.Equal("finish_only", dictionary["charge_plan"].Lifecycle);
		Assert.Equal("merge", dictionary["charge_plan"].Detail);
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("notify", new Dictionary<string, object> { ["enable_notify"] = false }, 0));
		string actualString = File.ReadAllText(path);
		Assert.True(zzzBackendResult.Success);
		Assert.Contains("notify_schema_version: 2", actualString, StringComparison.Ordinal);
		Assert.Contains("custom_marker: keep", actualString, StringComparison.Ordinal);
		Assert.Contains("coffee: 1", actualString, StringComparison.Ordinal);
		Assert.Contains("enable_notify: false", actualString, StringComparison.Ordinal);
	}

	/// <summary>
	/// 体力计划 scope 应读取真实 YAML，并在保存页面字段时保留 BaselineParity 其他键。
	/// </summary>
	[Fact]
	public void ChargePlanScopePreservesPythonYamlKeys()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		string text = Path.Combine(backendHarness.RunRoot, "config", "00", "one_dragon");
		string path = Path.Combine(text, "charge_plan.yml");
		Directory.CreateDirectory(text);
		File.WriteAllText(path, "loop: true\nskip_plan: false\nuse_coupon: false\nplan_list:\n- tab_name: 训练\n  category_name: 实战模拟室\n  mission_type_name: 基础材料\n  mission_name: 调查专项\n  run_times: 1\n  plan_times: 2\n  card_num: 默认数量\n  predefined_team_idx: -1\n  notorious_hunt_buff_num: 1\n  plan_id: plan-1\n");
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("charge-plan", 0, "one_dragon");
		List<ChargePlanItem> list = Assert.IsType<List<ChargePlanItem>>(configScope.Value.Values["plan_list"]);
		Assert.Single(list);
		Assert.Equal("调查专项", list[0].MissionName);
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("charge-plan", new Dictionary<string, object> { ["loop"] = false }, 0, "one_dragon"));
		string actualString = File.ReadAllText(path);
		Assert.True(zzzBackendResult.Success);
		Assert.Contains("use_coupon: false", actualString, StringComparison.Ordinal);
		Assert.Contains("plan_id: plan-1", actualString, StringComparison.Ordinal);
		Assert.Contains("loop: false", actualString, StringComparison.Ordinal);
	}

	/// <summary>
	/// 通知设置页遇到旧值或未知值时应回到 BaselineParity 默认模式。
	/// </summary>
	[Fact]
	public void NotifySettingsPageFallsBackFromUnknownModes()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			FakeBackend fakeBackend = new FakeBackend(contextReady: true, hasOneDragonApp: true, windowValid: true, null, new ZzzAppDto[] { new ZzzAppDto("coffee", "咖啡店", DefaultGroup: true, NeedNotify: true) });
			fakeBackend.SaveConfigScope(new ZzzSaveConfigScopeRequest("notify", new Dictionary<string, object>
			{
				["merge_error_immediate_notify"] = true,
				["applications"] = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal) { ["coffee"] = new NotifyApplicationSetting
				{
					Lifecycle = "legacy_unknown",
					Detail = "legacy_unknown"
				} }
			}, 0));
			FrontierNotifySettingsPage zzzNotifySettingsPage = new FrontierNotifySettingsPage(fakeBackend, 0);
			zzzNotifySettingsPage.OnPageShown();
			Assert.False(zzzNotifySettingsPage.FindControl<FAInfoBar>("ErrorBar").IsOpen);
			zzzNotifySettingsPage.DisposePage();
		});
	}

	/// <summary>
	/// Pivot 二级页应驱动 Shell 返回状态，并按 BaselineParity PageStackWrapper 生命周期返回根页。
	/// </summary>
	[Fact]
	public void PivotSecondaryNavigationDrivesShellBackState()
	{
		EnsureAvaloniaServices();
		RunOnUiThread(delegate
		{
			LifecycleControl lifecycleControl = new LifecycleControl();
			LifecycleControl lifecycleControl2 = new LifecycleControl();
			ZzzPivotPage zzzPivotPage = new ZzzPivotPage(new ZzzPivotPageItem[] { new ZzzPivotPageItem("根页面", lifecycleControl) });
			int stateChanges = 0;
			zzzPivotPage.BackNavigationStateChanged += delegate
			{
				stateChanges++;
			};
			zzzPivotPage.OnPageShown();
			zzzPivotPage.PushSecondary("设置", lifecycleControl2);
			Assert.True(zzzPivotPage.CanGoBack);
			Assert.Equal(1, lifecycleControl.Left);
			Assert.Equal(1, lifecycleControl.Hidden);
			Assert.Equal(1, lifecycleControl2.Shown);
			zzzPivotPage.GoBack();
			Assert.False(zzzPivotPage.CanGoBack);
			Assert.Equal(2, lifecycleControl.Shown);
			Assert.Equal(1, lifecycleControl2.Left);
			Assert.Equal(1, lifecycleControl2.Hidden);
			Assert.Equal(1, lifecycleControl2.Disposed);
			Assert.Equal(2, stateChanges);
			zzzPivotPage.DisposePage();
			Assert.Equal(1, lifecycleControl.Disposed);
		});
	}

	/// <summary>
	/// 设置页使用的配置 scope 应写入预期实例级或共享 YAML。
	/// </summary>
	[Fact]
	public void BackendSettingsScopesWriteRetainedValuesToExpectedYamlFiles()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("game", new Dictionary<string, object>
		{
			["type_input_way"] = "input",
			["key_switch_next"] = "tab",
			["xbox_action_map"] = new List<string> { "lt", "a" }
		}, 0));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("env", new Dictionary<string, object>
		{
			["screenshot_method"] = "BitBlt",
			["is_debug"] = true,
			["copy_screenshot"] = false,
			["proxy_type"] = "Personal",
			["personal_proxy"] = "http://127.0.0.1:8080",
			["key_start_running"] = "f6",
			["key_stop_running"] = "f7",
			["key_screenshot"] = "f8",
			["key_debug"] = "f9"
		}));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult3 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("model", new Dictionary<string, object>
		{
			["ocr_profile"] = "ppocrv6",
			["flash_classifier_gpu"] = true
		}));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult4 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("notify", new Dictionary<string, object> { ["title"] = "测试通知" }, 0));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult5 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("push", new Dictionary<string, object>
		{
			["send_image"] = false,
			["proxy"] = "PERSONAL",
			["smtp_server"] = "smtp.example.invalid:465",
			["qywx_key"] = "qywx-test"
		}));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult6 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("custom", new Dictionary<string, object>
		{
			["ui_language"] = "en",
			["theme"] = "Dark",
			["global_theme_color"] = "1,2,3",
			["background_type"] = "dynamic_background",
			["custom_banner"] = true
		}));
		Assert.True(zzzBackendResult.Success);
		Assert.True(zzzBackendResult2.Success);
		Assert.True(zzzBackendResult3.Success);
		Assert.True(zzzBackendResult4.Success);
		Assert.True(zzzBackendResult5.Success);
		Assert.True(zzzBackendResult6.Success);
		Assert.Equal("测试通知", backendHarness.Backend.GetConfigScope("notify", 0).Value.Values["title"]);
		Assert.Equal("qywx-test", backendHarness.Backend.GetConfigScope("push").Value.Values["qywx_key"]);
		Assert.Equal("en", backendHarness.Backend.GetConfigScope("custom").Value.Values["ui_language"]);
		string actualString = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "00", "game.yml"));
		string actualString2 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "env.yml"));
		string actualString3 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "model.yml"));
		string actualString4 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "00", "notify.yml"));
		string actualString5 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "push.yml"));
		string actualString6 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "custom.yml"));
		Assert.Contains("type_input_way: input", actualString, StringComparison.Ordinal);
		Assert.Contains("key_switch_next: tab", actualString, StringComparison.Ordinal);
		Assert.Contains("proxy_type: Personal", actualString2, StringComparison.Ordinal);
		Assert.Contains("ocr_profile: ppocrv6", actualString3, StringComparison.Ordinal);
		Assert.Contains("title: 测试通知", actualString4, StringComparison.Ordinal);
		Assert.Contains("qywx_key: qywx-test", actualString5, StringComparison.Ordinal);
		Assert.Contains("ui_language: en", actualString6, StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗助手 scope 应保存 BaselineParity 兼容 YAML key 和 control_method 值。
	/// </summary>
	[Fact]
	public void BackendBattleAssistantScopeWritesPythonYamlValues()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("battle-assistant", new Dictionary<string, object>
		{
			["control_method"] = "ds4",
			["auto_battle_config"] = "安比模板",
			["dodge_assistant_config"] = "音频闪避",
			["screenshot_interval"] = 0.05,
			["use_merged_file"] = false,
			["auto_ultimate_enabled"] = true
		}));
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("battle-assistant");
		string actualString = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "00", "battle_assistant.yml"));
		Assert.True(zzzBackendResult.Success);
		Assert.True(configScope.Success);
		Assert.Equal("ds4", configScope.Value.Values["control_method"]);
		Assert.Equal("安比模板", configScope.Value.Values["auto_battle_config"]);
		Assert.Contains("control_method: ds4", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("control_method: 键鼠", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("control_method: 手柄", actualString, StringComparison.Ordinal);
	}

	/// <summary>
	/// 游戏助手相关 scope 应写入模型 GPU 和委托助手 BaselineParity 兼容 key。
	/// </summary>
	[Fact]
	public void BackendGameAssistantScopesWriteModelAndCommissionValues()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		string text = Path.Combine(backendHarness.RunRoot, "config", "00", "one_dragon");
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "screenshot_helper.yml");
		File.WriteAllText(path, "frequency_second: 1.5\nlength_second: 10\nkey_save: f8\ndodge_detect: true\nscreenshot_before_key: true\nmini_map_angle_detect: false\nfishing_interact_press_time: 0.8");
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("model", new Dictionary<string, object> { ["flash_classifier_gpu"] = true }));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("commission-assistant", new Dictionary<string, object>
		{
			["pause_in_background"] = false,
			["dialog_option"] = "第一个",
			["dialog_click_interval"] = 0.75,
			["story_mode"] = "跳过剧情",
			["sleep_after_empty_screen"] = 1.25,
			["dodge_config"] = "自定义闪避",
			["dodge_switch"] = "F5",
			["auto_battle"] = "安比模板",
			["auto_battle_switch"] = "F6"
		}, null, "one_dragon"));
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendHarness.Backend.GetConfigScope("model");
		ZzzBackendResult<ZzzConfigScopeValuesDto> configScope2 = backendHarness.Backend.GetConfigScope("commission-assistant", null, "one_dragon");
		string actualString = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "model.yml"));
		string actualString2 = File.ReadAllText(path);
		Assert.True(zzzBackendResult.Success);
		Assert.True(zzzBackendResult2.Success);
		Assert.True(configScope.Success);
		Assert.True(configScope2.Success);
		Assert.True((bool)configScope.Value.Values["flash_classifier_gpu"]);
		Assert.False((bool)configScope2.Value.Values["pause_in_background"]);
		Assert.Equal("第一个", configScope2.Value.Values["dialog_option"]);
		Assert.Equal("跳过剧情", configScope2.Value.Values["story_mode"]);
		Assert.Equal("自定义闪避", configScope2.Value.Values["dodge_config"]);
		Assert.Contains("flash_classifier_gpu: true", actualString, StringComparison.Ordinal);
		Assert.Contains("dialog_option: 第一个", actualString2, StringComparison.Ordinal);
		Assert.Contains("auto_battle: 安比模板", actualString2, StringComparison.Ordinal);
		Assert.Contains("frequency_second: 1.5", actualString2, StringComparison.Ordinal);
		Assert.Contains("key_save: f8", actualString2, StringComparison.Ordinal);
		Assert.Contains("fishing_interact_press_time: 0.8", actualString2, StringComparison.Ordinal);
		string[] buffer = new string[6];
		buffer[0] = backendHarness.RunRoot;
		buffer[1] = "config";
		buffer[2] = "00";
		buffer[3] = "app_config";
		buffer[4] = "one_dragon";
		buffer[5] = "commission_assistant.yml";
		Assert.False(File.Exists(Path.Combine(buffer)));
	}

	/// <summary>
	/// 一条龙相关 scope 应写入 BaselineParity 兼容 YAML key 和应用组文件。
	/// </summary>
	[Fact]
	public void BackendOneDragonScopesWritePythonCompatibleValues()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon", new Dictionary<string, object>
		{
			["instance_run"] = "仅运行当前",
			["after_done"] = "关机",
			["enable_notify"] = false
		}));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult2 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("one-dragon-group", new Dictionary<string, object> { ["app_list"] = new List<OneDragonApplicationConfigItem>
		{
			new OneDragonApplicationConfigItem("coffee", enabled: true),
			new OneDragonApplicationConfigItem("charge_plan", enabled: false)
		} }, null, "one_dragon"));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult3 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("charge-plan", new Dictionary<string, object>
		{
			["plan_list"] = new List<ChargePlanItem>
			{
				new ChargePlanItem
				{
					CategoryName = "区域巡防",
					MissionTypeName = "自定义类型",
					RunTimes = 2,
					PlanTimes = 4
				}
			},
			["combat_simulation_double_reward_config"] = new ChargePlanItem
			{
				MissionTypeName = "基础材料",
				MissionName = "调查专项"
			}
		}, null, "one_dragon"));
		ZzzAppBackend backend = backendHarness.Backend;
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		List<PredefinedTeamInfo> list = new List<PredefinedTeamInfo>();
		int num = 3;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span = CollectionsMarshal.AsSpan(list2);
		span[0] = "ellen";
		span[1] = "rina";
		span[2] = "soukaku";
		list.Add(new PredefinedTeamInfo(0, "速刷队", "自定义战斗", list2));
		dictionary["team_list"] = list;
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult4 = backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("team", dictionary));
		ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult5 = backendHarness.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("standalone-app", new Dictionary<string, object>
		{
			["app_list"] = new List<string> { "coffee", "charge_plan" },
			["active_app_id"] = "coffee"
		}));
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult6 = backendHarness.Backend.CreateInstance();
		ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> zzzBackendResult7 = backendHarness.Backend.UpdateInstance(new ZzzUpdateInstanceRequest(1, "副号", false));
		Assert.True(zzzBackendResult.Success);
		Assert.True(zzzBackendResult2.Success);
		Assert.True(zzzBackendResult3.Success);
		Assert.True(zzzBackendResult4.Success);
		Assert.True(zzzBackendResult5.Success);
		Assert.True(zzzBackendResult6.Success);
		Assert.True(zzzBackendResult7.Success);
		Assert.Equal("仅运行当前", backendHarness.Backend.GetConfigScope("one-dragon").Value.Values["instance_run"]);
		Assert.False((bool)backendHarness.Backend.GetConfigScope("one-dragon").Value.Values["enable_notify"]);
		Assert.Equal("区域巡防", Assert.IsType<List<ChargePlanItem>>(backendHarness.Backend.GetConfigScope("charge-plan", null, "one_dragon").Value.Values["plan_list"])[0].CategoryName);
		Assert.Equal("速刷队", Assert.IsType<List<PredefinedTeamInfo>>(backendHarness.Backend.GetConfigScope("team").Value.Values["team_list"])[0].Name);
		Assert.Equal("coffee", backendHarness.Backend.GetConfigScope("standalone-app").Value.Values["active_app_id"]);
		Assert.Equal("副号", backendHarness.Backend.GetInstances().Value.Single((ZzzInstanceDto instance) => instance.Index == 1).Name);
		string actualString = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "one_dragon.yml"));
		string[] buffer = new string[5];
		buffer[0] = backendHarness.RunRoot;
		buffer[1] = "config";
		buffer[2] = "00";
		buffer[3] = "one_dragon";
		buffer[4] = "_group.yml";
		string actualString2 = File.ReadAllText(Path.Combine(buffer));
		string[] buffer2 = new string[5];
		buffer2[0] = backendHarness.RunRoot;
		buffer2[1] = "config";
		buffer2[2] = "00";
		buffer2[3] = "one_dragon";
		buffer2[4] = "charge_plan.yml";
		string actualString3 = File.ReadAllText(Path.Combine(buffer2));
		string actualString4 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "00", "team.yml"));
		string actualString5 = File.ReadAllText(Path.Combine(backendHarness.RunRoot, "config", "00", "standalone_app.yml"));
		Assert.Contains("instance_run: 仅运行当前", actualString, StringComparison.Ordinal);
		Assert.Contains("after_done: 关机", actualString, StringComparison.Ordinal);
		Assert.Contains("enable_notify: false", actualString, StringComparison.Ordinal);
		Assert.Contains("name: 副号", actualString, StringComparison.Ordinal);
		Assert.Contains("active_in_od: false", actualString, StringComparison.Ordinal);
		Assert.Contains("app_id: coffee", actualString2, StringComparison.Ordinal);
		Assert.Contains("enabled: false", actualString2, StringComparison.Ordinal);
		Assert.Contains("category_name: 区域巡防", actualString3, StringComparison.Ordinal);
		Assert.Contains("name: 速刷队", actualString4, StringComparison.Ordinal);
		Assert.Contains("active_app_id: coffee", actualString5, StringComparison.Ordinal);
		Assert.Contains("- coffee", actualString5, StringComparison.Ordinal);
	}

	/// <summary>
	/// 一条龙应用列表合并应保留未注册项原位，新注册应用以未持久化临时项置顶且不触发写盘。
	/// </summary>
	[Fact]
	public void OneDragonAppListMergerKeepsHiddenItemsAndShowsNewAppsTransientAtTop()
	{
		OneDragonApplicationConfigItem[] savedApps = new OneDragonApplicationConfigItem[2]
		{
			new OneDragonApplicationConfigItem("removed-app", enabled: true),
			new OneDragonApplicationConfigItem("coffee", enabled: true)
		};
		string[] defaultGroupAppIds = new string[2] { "coffee", "charge_plan" };
		HashSet<string> registered = defaultGroupAppIds.ToHashSet<string>(StringComparer.Ordinal);
		ZzzOneDragonAppMergeResult mergeResult = ZzzOneDragonAppListMerger.Merge(savedApps, defaultGroupAppIds, registered.Contains);
		Assert.False(mergeResult.Changed);
		Assert.Equal(new string[2] { "charge_plan", "coffee" }, mergeResult.VisibleApps.Select((OneDragonApplicationConfigItem app) => app.AppId));
		Assert.False(mergeResult.VisibleApps[0].Enabled);
		Assert.False(mergeResult.VisibleApps[0].IsPersisted);
		Assert.True(mergeResult.VisibleApps[1].Enabled);
		Assert.Equal(new string[1] { "charge_plan" }, mergeResult.TransientAppIds);
		Assert.Equal(new string[2] { "removed-app", "coffee" }, mergeResult.AllApps.Select((OneDragonApplicationConfigItem app) => app.AppId));
		Assert.Empty(mergeResult.MigratedAppIds);
	}

	/// <summary>
	/// 已注册但退出默认组的应用：启用的保留显示并标记迁移，禁用的从配置清除，关闭已迁移项即永久移除。
	/// </summary>
	[Fact]
	public void OneDragonAppListMergerMigratesEnabledNonDefaultAppsAndRemovesOnDisable()
	{
		OneDragonApplicationConfigItem[] savedApps = new OneDragonApplicationConfigItem[3]
		{
			new OneDragonApplicationConfigItem("hou_hou_bakery", enabled: true),
			new OneDragonApplicationConfigItem("scratch_card", enabled: false),
			new OneDragonApplicationConfigItem("coffee", enabled: true)
		};
		string[] defaultGroupAppIds = new string[1] { "coffee" };
		HashSet<string> registered = new HashSet<string>(StringComparer.Ordinal) { "hou_hou_bakery", "scratch_card", "coffee" };
		ZzzOneDragonAppMergeResult mergeResult = ZzzOneDragonAppListMerger.Merge(savedApps, defaultGroupAppIds, registered.Contains);
		Assert.True(mergeResult.Changed);
		Assert.Equal(new string[1] { "hou_hou_bakery" }, mergeResult.MigratedAppIds);
		Assert.Equal(new string[2] { "hou_hou_bakery", "coffee" }, mergeResult.VisibleApps.Select((OneDragonApplicationConfigItem app) => app.AppId));
		Assert.Equal(new string[2] { "hou_hou_bakery", "coffee" }, mergeResult.AllApps.Select((OneDragonApplicationConfigItem app) => app.AppId));
		ZzzOneDragonAppUpdateDto[] disableMigrated = new ZzzOneDragonAppUpdateDto[2]
		{
			new ZzzOneDragonAppUpdateDto("hou_hou_bakery", Enabled: false),
			new ZzzOneDragonAppUpdateDto("coffee", Enabled: true)
		};
		IReadOnlyList<OneDragonApplicationConfigItem> saved = ZzzOneDragonAppListMerger.ApplyVisibleOrder(mergeResult, disableMigrated);
		Assert.Equal(new string[1] { "coffee" }, saved.Select((OneDragonApplicationConfigItem app) => app.AppId));
	}

	/// <summary>
	/// 未触碰的临时项不写入保存顺序；用户启用或挪动后按可见顺序转正，未注册项保持原位。
	/// </summary>
	[Fact]
	public void OneDragonAppListMergerPersistsTransientAppsOnlyAfterUserInteraction()
	{
		OneDragonApplicationConfigItem[] savedApps = new OneDragonApplicationConfigItem[2]
		{
			new OneDragonApplicationConfigItem("removed-app", enabled: true),
			new OneDragonApplicationConfigItem("coffee", enabled: true)
		};
		string[] defaultGroupAppIds = new string[2] { "coffee", "charge_plan" };
		HashSet<string> registered = defaultGroupAppIds.ToHashSet<string>(StringComparer.Ordinal);
		ZzzOneDragonAppMergeResult mergeResult = ZzzOneDragonAppListMerger.Merge(savedApps, defaultGroupAppIds, registered.Contains);
		ZzzOneDragonAppUpdateDto[] untouched = new ZzzOneDragonAppUpdateDto[2]
		{
			new ZzzOneDragonAppUpdateDto("charge_plan", Enabled: false),
			new ZzzOneDragonAppUpdateDto("coffee", Enabled: true)
		};
		IReadOnlyList<OneDragonApplicationConfigItem> savedUntouched = ZzzOneDragonAppListMerger.ApplyVisibleOrder(mergeResult, untouched);
		Assert.Equal(new string[2] { "removed-app", "coffee" }, savedUntouched.Select((OneDragonApplicationConfigItem app) => app.AppId));
		ZzzOneDragonAppUpdateDto[] enabledTransient = new ZzzOneDragonAppUpdateDto[2]
		{
			new ZzzOneDragonAppUpdateDto("charge_plan", Enabled: true),
			new ZzzOneDragonAppUpdateDto("coffee", Enabled: true)
		};
		IReadOnlyList<OneDragonApplicationConfigItem> savedPersisted = ZzzOneDragonAppListMerger.ApplyVisibleOrder(mergeResult, enabledTransient);
		Assert.Equal(new string[3] { "removed-app", "charge_plan", "coffee" }, savedPersisted.Select((OneDragonApplicationConfigItem app) => app.AppId));
		Assert.True(savedPersisted[1].Enabled);
	}

	/// <summary>
	/// 生产后端应按 BaselineParity save_app_list 语义保存拖拽顺序和启用状态，同时让未注册应用保持原位。
	/// </summary>
	[Fact]
	public void BackendPersistsOneDragonDragOrderAndEnabledStateToRealGroupYaml()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		Directory.CreateDirectory(Path.Combine(backendHarness.RunRoot, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(backendHarness.RunRoot, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(backendHarness.RunRoot, "assets", "game_data", "screen_info"));
		string text = Path.Combine(backendHarness.RunRoot, "config", "00", "one_dragon");
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "_group.yml");
		File.WriteAllText(path, "app_list:\n- app_id: removed-app\n  enabled: true\n- app_id: coffee\n  enabled: false\n- app_id: charge_plan\n  enabled: true");
		ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> oneDragonApps = backendHarness.Backend.GetOneDragonApps(0);
		Assert.True(oneDragonApps.Success, oneDragonApps.Error);
		Assert.NotNull(oneDragonApps.Value);
		List<ZzzOneDragonAppUpdateDto> list = oneDragonApps.Value.Select((ZzzOneDragonAppDto app) => new ZzzOneDragonAppUpdateDto(app.AppId, app.Enabled)).ToList();
		int num = list.FindIndex((ZzzOneDragonAppUpdateDto app) => app.AppId == "coffee");
		int num2 = list.FindIndex((ZzzOneDragonAppUpdateDto app) => app.AppId == "charge_plan");
		Assert.True(num >= 0);
		Assert.True(num2 >= 0);
		ZzzOneDragonAppUpdateDto item = list[num]with
		{
			Enabled = true
		};
		list.RemoveAt(num);
		num2 = list.FindIndex((ZzzOneDragonAppUpdateDto app) => app.AppId == "charge_plan");
		list.Insert(num2, item);
		ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> zzzBackendResult = backendHarness.Backend.SaveOneDragonApps(new ZzzSaveOneDragonAppsRequest(list, 0));
		Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
		Assert.NotNull(zzzBackendResult.Value);
		Assert.Equal(list.Select((ZzzOneDragonAppUpdateDto app) => app.AppId), zzzBackendResult.Value.Select((ZzzOneDragonAppDto app) => app.AppId));
		Assert.True(zzzBackendResult.Value.Single((ZzzOneDragonAppDto app) => app.AppId == "coffee").Enabled);
		string text2 = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
		int num3 = text2.IndexOf("app_id: removed-app", StringComparison.Ordinal);
		int num4 = text2.IndexOf("app_id: coffee", StringComparison.Ordinal);
		int num5 = text2.IndexOf("app_id: charge_plan", StringComparison.Ordinal);
		Assert.True(num3 >= 0);
		Assert.True(num4 > num3);
		Assert.True(num5 > num4);
	}

	/// <summary>
	/// 最近日志通过门面返回有界结果。
	/// </summary>
	[Fact]
	public void BackendReturnsRecentLogs()
	{
		using BackendHarness backendHarness = BackendHarness.Create();
		backendHarness.LogProvider.CreateLogger("test").Log(Microsoft.Extensions.Logging.LogLevel.Information, 0, "hello", null, (string state, Exception? _) => state);
		ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>> recentLogs = backendHarness.Backend.GetRecentLogs(10);
		Assert.True(recentLogs.Success);
		Assert.Contains((IEnumerable<ZzzLogEntryDto>)recentLogs.Value, (Predicate<ZzzLogEntryDto>)((ZzzLogEntryDto entry) => entry.Message == "hello"));
	}

	private static void EnsureAvaloniaServices()
	{
		AvaloniaThread.Value.EnsureStarted();
	}

	private static void EnsureFluentTheme()
	{
		if (Avalonia.Application.Current?.Styles.OfType<FluentAvaloniaTheme>().Any() == false)
		{
			Avalonia.Application.Current.Styles.Add(new FluentAvaloniaTheme());
		}

		if (Avalonia.Application.Current is { } application
			&& !application.TryGetResource("ZzzBattleStateRecentBrush1", ThemeVariant.Light, out _))
		{
			ResourceDictionary resources = (ResourceDictionary)AvaloniaXamlLoader.Load(
				new Uri("avares://ZzzOd.Gui/Theme/ZzzFluentTheme.axaml"),
				new Uri("avares://ZzzOd.Gui/"));
			application.Resources.MergedDictionaries.Add(resources);
		}

		if (Avalonia.Application.Current is { } current
			&& current.ActualThemeVariant == ThemeVariant.Default)
		{
			current.RequestedThemeVariant = ThemeVariant.Light;
		}
	}

	internal static void RunOnUiThread(Action action)
	{
		AvaloniaThread.Value.Invoke(action);
	}

	private static void WithGuiEnvironment(string? devMode, string? diagnostics, Action action)
	{
		string environmentVariable = Environment.GetEnvironmentVariable("ZZZOD_GUI_DEV_MODE");
		string environmentVariable2 = Environment.GetEnvironmentVariable("ZZZOD_GUI_ENABLE_DIAGNOSTICS");
		try
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_DEV_MODE", devMode);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_ENABLE_DIAGNOSTICS", diagnostics);
			action();
		}
		finally
		{
			Environment.SetEnvironmentVariable("ZZZOD_GUI_DEV_MODE", environmentVariable);
			Environment.SetEnvironmentVariable("ZZZOD_GUI_ENABLE_DIAGNOSTICS", environmentVariable2);
		}
	}

	private static IEnumerable<string> EnumerateText(Control root)
	{
		Queue<Control> queue = new Queue<Control>();
		queue.Enqueue(root);
		Control current;
		string text = default(string);
		string textBoxText = default(string);
		string buttonText = default(string);
		while (queue.TryDequeue(out current))
		{
			int num;
			if (current is TextBlock textBlock)
			{
				text = textBlock.Text;
				if (text != null)
				{
					num = ((text.Length > 0) ? 1 : 0);
					goto IL_00da;
				}
			}
			num = 0;
			goto IL_00da;
			IL_0133:
			int num2;
			if (num2 != 0)
			{
				yield return textBoxText;
			}
			int num3;
			if (current is Button button)
			{
				object content = button.Content;
				buttonText = content as string;
				num3 = ((buttonText != null) ? 1 : 0);
			}
			else
			{
				num3 = 0;
			}
			if (num3 != 0)
			{
				yield return buttonText;
			}
			if (current is FASettingsExpanderItem settingsItem)
			{
				object content = settingsItem.Content;
				if (content is string itemContent)
				{
					yield return itemContent;
				}
				if (!string.IsNullOrWhiteSpace(settingsItem.Description))
				{
					yield return settingsItem.Description;
				}
			}
			if (current is FASettingsExpander settingsExpander)
			{
				object content = settingsExpander.Header;
				if (content is string expanderHeader)
				{
					yield return expanderHeader;
				}
				if (!string.IsNullOrWhiteSpace(settingsExpander.Description))
				{
					yield return settingsExpander.Description;
				}
			}
			if (current is FAInfoBar infoBar)
			{
				if (!string.IsNullOrWhiteSpace(infoBar.Title))
				{
					yield return infoBar.Title;
				}
				if (!string.IsNullOrWhiteSpace(infoBar.Message))
				{
					yield return infoBar.Message;
				}
			}
			foreach (Control child in GetChildren(current))
			{
				queue.Enqueue(child);
			}
			text = null;
			textBoxText = null;
			buttonText = null;
			continue;
			IL_00da:
			if (num != 0)
			{
				yield return text;
			}
			if (current is TextBox textBox)
			{
				textBoxText = textBox.Text;
				if (textBoxText != null)
				{
					num2 = ((textBoxText.Length > 0) ? 1 : 0);
					goto IL_0133;
				}
			}
			num2 = 0;
			goto IL_0133;
		}
	}

	private static T FindDescendant<T>(Control root) where T : Control
	{
		Queue<Control> queue = new Queue<Control>();
		queue.Enqueue(root);
		Control result;
		while (queue.TryDequeue(out result))
		{
			if (result is T result2)
			{
				return result2;
			}
			foreach (Control child in GetChildren(result))
			{
				queue.Enqueue(child);
			}
		}
		throw new InvalidOperationException("找不到控件 " + typeof(T).Name + "。");
	}

	private static IEnumerable<Control> GetChildren(Control control)
	{
		if (!(control is FASettingsExpanderItem settingsItem))
		{
			if (!(control is FASettingsExpander { Footer: var footer } settingsExpander))
			{
				if (!(control is TabControl tabControl))
				{
					if (control is Border border)
					{
						Control child = border.Child;
						if (child != null)
						{
							yield return child;
						}
					}
					else if (control is ContentControl contentControl)
					{
						object content = contentControl.Content;
						if (!(content is Control child2))
						{
							if (content is string)
							{
							}
						}
						else
						{
							yield return child2;
						}
					}
					else
					{
						if (!(control is Panel panel))
						{
							yield break;
						}
						foreach (Control child5 in panel.Children)
						{
							yield return child5;
						}
					}
					yield break;
				}
			foreach (object item in tabControl.Items)
				{
					if (item is Control child3)
					{
						yield return child3;
					}
				}
				yield break;
			}
			if (footer is Control expanderFooter)
			{
				yield return expanderFooter;
			}
			foreach (object item2 in settingsExpander.Items)
			{
				if (item2 is Control child4)
				{
					yield return child4;
				}
			}
		}
		else
		{
			object content2 = settingsItem.Content;
			if (content2 is Control settingsContent)
			{
				yield return settingsContent;
			}
			content2 = settingsItem.Footer;
			if (content2 is Control settingsFooter)
			{
				yield return settingsFooter;
			}
		}
	}
}
