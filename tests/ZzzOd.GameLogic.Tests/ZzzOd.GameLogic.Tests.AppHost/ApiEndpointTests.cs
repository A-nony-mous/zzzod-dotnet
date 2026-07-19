using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZzzOd.Api;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// ZZZ API endpoint 测试。
/// </summary>
public sealed class ApiEndpointTests
{
	private sealed class FakeBackend : IZzzAppBackend
	{
		private readonly ZzzBackendEventBus _eventBus = new ZzzBackendEventBus();

		public int StopRunCalls { get; private set; }

		public ZzzBackendResult<ZzzHealthDto> GetHealth()
		{
			return ZzzBackendResult<ZzzHealthDto>.Ok(new ZzzHealthDto(ZzzHostMode.ApiOnly, "test", "test-root", ApiEnabled: true, ContextReady: true, 0));
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> GetInstances()
		{
			return ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>>.Ok(new ZzzInstanceDto[] { new ZzzInstanceDto(0, "00", Active: true, "config/00") });
		}

		public ZzzBackendResult<ZzzInstanceDto> GetCurrentInstance()
		{
			return ZzzBackendResult<ZzzInstanceDto>.Ok(new ZzzInstanceDto(0, "00", Active: true, "config/00"));
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int instanceIndex)
		{
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> CreateInstance()
		{
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request)
		{
			return GetInstances();
		}

		public ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int instanceIndex)
		{
			return GetInstances();
		}

		public ZzzBackendResult<ZzzRunStatusDto> LoginInstance(int instanceIndex)
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Fail(ZzzBackendErrorCode.NotReady, "当前未配置登录操作。");
		}

		public ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetApps()
		{
			return ZzzBackendResult<IReadOnlyList<ZzzAppDto>>.Ok(new ZzzAppDto[] { new ZzzAppDto("test-app", "测试应用", DefaultGroup: true, NeedNotify: false, RunAvailable: true, SupportsGroup: true, new string[] { "game" }) });
		}

		public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> GetOneDragonApps(int? instanceIndex = null)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(Array.Empty<ZzzOneDragonAppDto>());
		}

		public ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> SaveOneDragonApps(ZzzSaveOneDragonAppsRequest request)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>>.Ok(Array.Empty<ZzzOneDragonAppDto>());
		}

		public ZzzBackendResult<ZzzChargePlanCatalogDto> GetChargePlanCatalog()
		{
			return ZzzBackendResult<ZzzChargePlanCatalogDto>.Ok(new ZzzChargePlanCatalogDto(Array.Empty<ZzzChargePlanCategoryDto>(), Array.Empty<ZzzChargePlanTeamDto>(), Array.Empty<string>()));
		}

		public ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> ResetShiyuDefenseRunRecord(int instanceIndex)
		{
			return ZzzBackendResult<ZzzShiyuDefenseRunRecordDto>.Ok(new ZzzShiyuDefenseRunRecordDto(instanceIndex, Array.Empty<int>()));
		}

		public ZzzBackendResult<ZzzLifeOnLineRunRecordDto> GetLifeOnLineRunRecord(int? instanceIndex = null)
		{
			return ZzzBackendResult<ZzzLifeOnLineRunRecordDto>.Ok(new ZzzLifeOnLineRunRecordDto(instanceIndex.GetValueOrDefault(), 0));
		}

		public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> GetBattleAssistantConfigCatalog()
		{
			return ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto>.Ok(new ZzzBattleAssistantConfigCatalogDto(Array.Empty<string>(), Array.Empty<string>()));
		}

		public ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> DeleteBattleAssistantConfig(ZzzDeleteBattleAssistantConfigRequest request)
		{
			return GetBattleAssistantConfigCatalog();
		}

		public ZzzBackendResult<ZzzBattleAssistantRuntimeDto> GetBattleAssistantRuntime()
		{
			return ZzzBackendResult<ZzzBattleAssistantRuntimeDto>.Ok(new ZzzBattleAssistantRuntimeDto(IsRunning: false, null, null, null, Array.Empty<ZzzBattleAssistantStateDto>()));
		}

		public void SubscribeBattleAssistantOperationLoaded(Action callback)
		{
		}

		public void UnsubscribeBattleAssistantOperationLoaded(Action callback)
		{
		}

		public ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>> GetRecentLogs(int limit = 200)
		{
			return ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>>.Ok(new ZzzLogEntryDto[] { new ZzzLogEntryDto(DateTimeOffset.UtcNow, "Information", "test", "hello", null) });
		}

		public ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>> GetConfigScopes()
		{
			return ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>>.Ok(new ZzzConfigScopeDescriptorDto[] { ConfigDescriptor() });
		}

		public ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex = null, string? groupId = null)
		{
			return (scope == "game") ? ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(ConfigDescriptor(), 0, null, new Dictionary<string, object> { ["background_mode"] = false })) : ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, "{\"scope\":\"game\",\"key\":null,\"message\":\"未知配置 scope。\"}");
		}

		public ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfigScope(ZzzSaveConfigScopeRequest request)
		{
			if (request.Values.Keys.Any((string key) => key != "background_mode"))
			{
				string text = request.Values.Keys.First((string key) => key != "background_mode");
				return ZzzBackendResult<ZzzConfigScopeValuesDto>.Fail(ZzzBackendErrorCode.Validation, "{\"scope\":\"game\",\"key\":\"" + text + "\",\"message\":\"未知配置 key。\"}");
			}
			return ZzzBackendResult<ZzzConfigScopeValuesDto>.Ok(new ZzzConfigScopeValuesDto(ConfigDescriptor(), 0, null, request.Values));
		}

		public ZzzBackendResult<ZzzWindowStatusDto> GetWindow()
		{
			return ZzzBackendResult<ZzzWindowStatusDto>.Ok(new ZzzWindowStatusDto("测试窗口", IsWinValid: true, IsWinActive: true, IsWinScale: false));
		}

		public ZzzBackendResult<ZzzScreenshotDto> GetScreenshot()
		{
			return ZzzBackendResult<ZzzScreenshotDto>.Ok(new ZzzScreenshotDto("image/png", new byte[3] { 1, 2, 3 }));
		}

		public Task<ZzzBackendResult<ZzzRunStatusDto>> StartRunAsync(ZzzStartRunRequest request)
		{
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Running, request.AppId)));
		}

		public ZzzBackendResult<ZzzRunStatusDto> PauseRun()
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Paused));
		}

		public ZzzBackendResult<ZzzRunStatusDto> ResumeRun()
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Running));
		}

		public Task<ZzzBackendResult<ZzzRunStatusDto>> StopRunAsync()
		{
			StopRunCalls++;
			return Task.FromResult(ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Cancelled)));
		}

		public ZzzBackendResult<ZzzRunStatusDto> GetCurrentRun()
		{
			return ZzzBackendResult<ZzzRunStatusDto>.Ok(new ZzzRunStatusDto(ZzzRunState.Idle));
		}

		public ChannelReader<ZzzBackendEvent> SubscribeEvents()
		{
			return _eventBus.Subscribe();
		}

		public void UnsubscribeEvents(ChannelReader<ZzzBackendEvent> reader)
		{
			_eventBus.Unsubscribe(reader);
		}

		private static ZzzConfigScopeDescriptorDto ConfigDescriptor()
		{
			return new ZzzConfigScopeDescriptorDto("game", "游戏设置", InstanceBound: true, GroupBound: false, Writable: true, new ZzzConfigSettingDescriptorDto[] { new ZzzConfigSettingDescriptorDto("background_mode", "后台模式", ZzzConfigValueType.Boolean, Writable: true, false) });
		}
	}

	/// <summary>
	/// 健康检查可以匿名访问，受保护接口会拒绝未认证请求。
	/// </summary>
	[Fact]
	public async Task RestApiHandlesHealthAndUnauthorizedRequest()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient
		{
			BaseAddress = baseAddress
		};
		HttpResponseMessage health = await client.GetAsync("/api/health");
		HttpResponseMessage apps = await client.GetAsync("/api/apps");
		Assert.Equal(HttpStatusCode.OK, health.StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, apps.StatusCode);
	}

	/// <summary>
	/// REST API 使用 Bearer token 后可以访问运行控制接口。
	/// </summary>
	[Fact]
	public async Task RestApiAllowsAuthenticatedRunQuery()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient
		{
			BaseAddress = baseAddress
		};
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
		HttpResponseMessage response = await client.GetAsync("/api/runs/current");
		string json = await response.Content.ReadAsStringAsync();
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("\"state\"", json, StringComparison.Ordinal);
	}

	/// <summary>
	/// REST 停止端点必须进入共享后端停止入口。
	/// </summary>
	[Fact]
	public async Task RestApiStopUsesSharedBackendEntry()
	{
		ZzzApiOptions options;
		FakeBackend backend;
		await using WebApplication app = BuildApp(out options, out backend);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient { BaseAddress = baseAddress };
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);

		HttpResponseMessage response = await client.PostAsync("/api/runs/current/stop", null);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(1, backend.StopRunCalls);
	}

	/// <summary>
	/// OpenAPI 文档包含主要 REST 路由和启动请求 DTO。
	/// </summary>
	[Fact]
	public async Task OpenApiDocumentContainsRoutesAndDtos()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient
		{
			BaseAddress = baseAddress
		};
		string json = await client.GetStringAsync("/openapi/v1.json");
		Assert.Contains("/api/runs", json, StringComparison.Ordinal);
		Assert.Contains("/api/logs/recent", json, StringComparison.Ordinal);
		Assert.Contains("/api/config/{scope}", json, StringComparison.Ordinal);
		Assert.Contains("ZzzStartRunRequest", json, StringComparison.Ordinal);
		Assert.Contains("ZzzConfigScopeSaveRequest", json, StringComparison.Ordinal);
	}

	/// <summary>
	/// 远程能力 API 应覆盖应用、日志、实例、配置、窗口和截图。
	/// </summary>
	[Fact]
	public async Task AuthenticatedRemoteCapabilityEndpointsReturnExpectedResponses()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient
		{
			BaseAddress = baseAddress
		};
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
		HttpResponseMessage apps = await client.GetAsync("/api/apps");
		HttpResponseMessage logs = await client.GetAsync("/api/logs/recent?limit=5");
		HttpResponseMessage currentInstance = await client.GetAsync("/api/instances/current");
		HttpResponseMessage scopes = await client.GetAsync("/api/config/scopes");
		HttpResponseMessage gameConfig = await client.GetAsync("/api/config/game");
		HttpResponseMessage saveConfig = await client.PutAsync("/api/config/game", JsonContent.Create(new ZzzConfigScopeSaveRequest(new Dictionary<string, object> { ["background_mode"] = true })));
		HttpResponseMessage unknownConfig = await client.PutAsync("/api/config/game", JsonContent.Create(new ZzzConfigScopeSaveRequest(new Dictionary<string, object> { ["bad_key"] = true })));
		HttpResponseMessage window = await client.GetAsync("/api/window");
		HttpResponseMessage screenshot = await client.GetAsync("/api/screenshot");
		Assert.Equal(HttpStatusCode.OK, apps.StatusCode);
		Assert.Contains("configScopes", await apps.Content.ReadAsStringAsync(), StringComparison.Ordinal);
		Assert.Equal(HttpStatusCode.OK, logs.StatusCode);
		Assert.Equal(HttpStatusCode.OK, currentInstance.StatusCode);
		Assert.Equal(HttpStatusCode.OK, scopes.StatusCode);
		Assert.Equal(HttpStatusCode.OK, gameConfig.StatusCode);
		Assert.Equal(HttpStatusCode.OK, saveConfig.StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, unknownConfig.StatusCode);
		Assert.Contains("bad_key", await unknownConfig.Content.ReadAsStringAsync(), StringComparison.Ordinal);
		Assert.Equal(HttpStatusCode.OK, window.StatusCode);
		Assert.Equal(HttpStatusCode.OK, screenshot.StatusCode);
	}

	/// <summary>
	/// WebSocket 连接成功后会收到心跳消息。
	/// </summary>
	[Fact]
	public async Task WebSocketReceivesHeartbeat()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using ClientWebSocket socket = new ClientWebSocket();
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5L));
		Uri wsUri = new Uri($"ws://{baseAddress.Host}:{baseAddress.Port}/api/events/ws?token={options.Token}");
		await socket.ConnectAsync(wsUri, timeout.Token);
		byte[] buffer = new byte[1024];
		WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, timeout.Token);
		string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
		socket.Abort();
		Assert.Equal(WebSocketMessageType.Text, result.MessageType);
		Assert.Contains("\"type\":\"heartbeat\"", text, StringComparison.Ordinal);
	}

	/// <summary>
	/// WebSocket 建连后的状态快照必须来自与 REST 相同的后端运行状态。
	/// </summary>
	[Fact]
	public async Task WebSocketReceivesCurrentRunSnapshotFromBackend()
	{
		ZzzApiOptions options;
		await using WebApplication app = BuildApp(out options);
		await app.StartAsync();
		Uri baseAddress = GetBaseAddress(app);
		using HttpClient client = new HttpClient
		{
			BaseAddress = baseAddress
		};
		string restText = await client.GetStringAsync("/api/runs/current?token=" + options.Token);
		using ClientWebSocket socket = new ClientWebSocket();
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5L));
		Uri wsUri = new Uri($"ws://{baseAddress.Host}:{baseAddress.Port}/api/events/ws?token={options.Token}");
		await socket.ConnectAsync(wsUri, timeout.Token);
		byte[] buffer = new byte[1024];
		await socket.ReceiveAsync(buffer, timeout.Token);
		WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, timeout.Token);
		string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
		socket.Abort();
		using JsonDocument rest = JsonDocument.Parse(restText);
		using JsonDocument webSocket = JsonDocument.Parse(text);
		string restState = rest.RootElement.GetProperty("state").GetRawText();
		string webSocketState = webSocket.RootElement.GetProperty("data").GetProperty("state").GetRawText();
		Assert.Equal(WebSocketMessageType.Text, result.MessageType);
		Assert.Contains("\"type\":\"run.stateChanged\"", text, StringComparison.Ordinal);
		Assert.Equal(restState, webSocketState);
	}

	private static WebApplication BuildApp(out ZzzApiOptions options) => BuildApp(out options, out _);

	private static WebApplication BuildApp(out ZzzApiOptions options, out FakeBackend backend)
	{
		options = new ZzzApiOptions
		{
			Token = "test-token"
		};
		WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
		webApplicationBuilder.WebHost.UseUrls("http://127.0.0.1:0");
		backend = new FakeBackend();
		webApplicationBuilder.Services.AddSingleton((IZzzAppBackend)backend);
		webApplicationBuilder.Services.AddSingleton(options);
		webApplicationBuilder.Services.AddZzzApiServices();
		WebApplication webApplication = webApplicationBuilder.Build();
		webApplication.MapZzzApiEndpoints();
		return webApplication;
	}

	private static Uri GetBaseAddress(WebApplication app)
	{
		string uriString = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
		return new Uri(uriString);
	}
}
