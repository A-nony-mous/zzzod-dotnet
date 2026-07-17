using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Api;

/// <summary>
/// ZZZ API endpoint 扩展。
/// </summary>
public static class ZzzApiEndpointExtensions
{
    /// <summary>
    /// 注册 ZZZ API 所需服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddZzzApiServices(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        return services;
    }

    /// <summary>
    /// 注册 ZZZ API CORS 配置。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="options">API 配置。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddZzzApiCors(this IServiceCollection services, ZzzApiOptions options)
    {
        if (options.CorsOrigins.Count == 0)
        {
            return services;
        }

        services.AddCors(cors =>
        {
            cors.AddPolicy("configured", policy =>
            {
                policy.WithOrigins(options.CorsOrigins.ToArray())
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
        return services;
    }

    /// <summary>
    /// 使用 ZZZ API CORS 配置。
    /// </summary>
    /// <param name="app">Web 应用。</param>
    /// <param name="options">API 配置。</param>
    /// <returns>Web 应用。</returns>
    public static WebApplication UseZzzApiCors(this WebApplication app, ZzzApiOptions options)
    {
        if (options.CorsOrigins.Count > 0)
        {
            app.UseCors("configured");
        }

        return app;
    }

    /// <summary>
    /// 映射 ZZZ API endpoint。
    /// </summary>
    /// <param name="app">Web 应用。</param>
    /// <returns>Web 应用。</returns>
    public static WebApplication MapZzzApiEndpoints(this WebApplication app)
    {
        app.UseWebSockets();
        app.MapOpenApi("/openapi/{documentName}.json");

        RouteGroupBuilder api = app.MapGroup("/api");
        api.MapGet("/health", GetHealth)
            .WithName("GetHealth");
        api.MapGet("/instances", GetInstances)
            .WithName("GetInstances");
        api.MapGet("/instances/current", GetCurrentInstance)
            .WithName("GetCurrentInstance");
        api.MapPost("/instances/{index:int}/activate", ActivateInstance)
            .WithName("ActivateInstance");
        api.MapGet("/window", GetWindow)
            .WithName("GetWindow");
        api.MapGet("/screenshot", GetScreenshot)
            .WithName("GetScreenshot");
        api.MapGet("/apps", GetApps)
            .WithName("GetApps");
        api.MapPost("/runs", StartRunAsync)
            .WithName("StartRun");
        api.MapGet("/runs/current", GetCurrentRun)
            .WithName("GetCurrentRun");
        api.MapPost("/runs/current/pause", PauseRun)
            .WithName("PauseRun");
        api.MapPost("/runs/current/resume", ResumeRun)
            .WithName("ResumeRun");
        api.MapPost("/runs/current/stop", StopRunAsync)
            .WithName("StopRun");
        api.MapGet("/logs/recent", GetRecentLogs)
            .WithName("GetRecentLogs");
        api.MapGet("/config/scopes", GetConfigScopes)
            .WithName("GetConfigScopes");
        api.MapGet("/config/{scope}", GetConfigScope)
            .WithName("GetConfigScope");
        api.MapPut("/config/{scope}", SaveConfigScope)
            .WithName("SaveConfigScope");

        api.Map("/events/ws", HandleWebSocketAsync);
        app.Map("/ws", HandleWebSocketAsync);
        return app;
    }

    private static IResult GetHealth(IZzzAppBackend backend) =>
        ToHttpResult(backend.GetHealth(), allowProblemDetails: false);

    private static IResult GetInstances(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetInstances()) : Results.Unauthorized();

    private static IResult GetCurrentInstance(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetCurrentInstance()) : Results.Unauthorized();

    private static IResult ActivateInstance(int index, HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.ActivateInstance(index)) : Results.Unauthorized();

    private static IResult GetWindow(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetWindow()) : Results.Unauthorized();

    private static IResult GetScreenshot(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options)
    {
        if (!IsAuthorized(http, options))
        {
            return Results.Unauthorized();
        }

        ZzzBackendResult<ZzzScreenshotDto> result = backend.GetScreenshot();
        return result.Success && result.Value is not null
            ? Results.File(result.Value.Bytes, result.Value.ContentType)
            : ToHttpResult(result);
    }

    private static IResult GetApps(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetApps()) : Results.Unauthorized();

    private static async Task<IResult> StartRunAsync(
        ZzzStartRunRequest request,
        HttpContext http,
        IZzzAppBackend backend,
        ZzzApiOptions options)
    {
        if (!IsAuthorized(http, options))
        {
            return Results.Unauthorized();
        }

        return ToHttpResult(await backend.StartRunAsync(request).ConfigureAwait(false));
    }

    private static IResult GetCurrentRun(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetCurrentRun()) : Results.Unauthorized();

    private static IResult PauseRun(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.PauseRun()) : Results.Unauthorized();

    private static IResult ResumeRun(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.ResumeRun()) : Results.Unauthorized();

    private static async Task<IResult> StopRunAsync(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options)
    {
        if (!IsAuthorized(http, options))
        {
            return Results.Unauthorized();
        }

        return ToHttpResult(await backend.StopRunAsync().ConfigureAwait(false));
    }

    private static IResult GetRecentLogs(int? limit, HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetRecentLogs(limit ?? 200)) : Results.Unauthorized();

    private static IResult GetConfigScopes(HttpContext http, IZzzAppBackend backend, ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetConfigScopes()) : Results.Unauthorized();

    private static IResult GetConfigScope(
        string scope,
        int? instanceIndex,
        string? groupId,
        HttpContext http,
        IZzzAppBackend backend,
        ZzzApiOptions options) =>
        IsAuthorized(http, options) ? ToHttpResult(backend.GetConfigScope(scope, instanceIndex, groupId)) : Results.Unauthorized();

    private static IResult SaveConfigScope(
        string scope,
        ZzzConfigScopeSaveRequest request,
        HttpContext http,
        IZzzAppBackend backend,
        ZzzApiOptions options)
    {
        if (!IsAuthorized(http, options))
        {
            return Results.Unauthorized();
        }

        return ToHttpResult(backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            scope,
            request.Values,
            request.InstanceIndex,
            request.GroupId)));
    }

    private static bool IsAuthorized(HttpContext http, ZzzApiOptions options)
    {
        string? header = http.Request.Headers.Authorization;
        if (header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return options.IsTokenValid(header["Bearer ".Length..].Trim());
        }

        string? queryToken = http.Request.Query["token"];
        return options.IsTokenValid(queryToken);
    }

    private static IResult ToHttpResult<T>(ZzzBackendResult<T> result, bool allowProblemDetails = true)
    {
        if (result.Success)
        {
            return Results.Ok(result.Value);
        }

        int statusCode = result.ErrorCode switch
        {
            ZzzBackendErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
            ZzzBackendErrorCode.Conflict => StatusCodes.Status409Conflict,
            ZzzBackendErrorCode.Validation => StatusCodes.Status400BadRequest,
            ZzzBackendErrorCode.NotFound => StatusCodes.Status404NotFound,
            ZzzBackendErrorCode.NotReady => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
        if (result.ErrorCode == ZzzBackendErrorCode.Validation)
        {
            return Results.Json(TryParseValidationError(result.Error), statusCode: statusCode);
        }

        return allowProblemDetails
            ? Results.Problem(result.Error, statusCode: statusCode)
            : Results.Json(new { error = result.Error }, statusCode: statusCode);
    }

    private static object TryParseValidationError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return new ZzzValidationErrorDto(null, null, "请求校验失败。");
        }

        try
        {
            return JsonSerializer.Deserialize<ZzzValidationErrorDto>(error, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new ZzzValidationErrorDto(null, null, error);
        }
        catch (JsonException)
        {
            return new ZzzValidationErrorDto(null, null, error);
        }
    }

    private static async Task HandleWebSocketAsync(HttpContext http)
    {
        if (!http.WebSockets.IsWebSocketRequest)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        ZzzApiOptions options = http.RequestServices.GetRequiredService<ZzzApiOptions>();
        if (!IsAuthorized(http, options))
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        IZzzAppBackend backend = http.RequestServices.GetRequiredService<IZzzAppBackend>();
        using WebSocket socket = await http.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        System.Threading.Channels.ChannelReader<ZzzBackendEvent> reader = backend.SubscribeEvents();
        using CancellationTokenSource connectionCts = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        using PeriodicTimer heartbeat = new(TimeSpan.FromSeconds(15));
        using SemaphoreSlim sendLock = new(1, 1);
        Task heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeat.WaitForNextTickAsync(connectionCts.Token).ConfigureAwait(false))
                {
                    await SendAsync(
                        socket,
                        new ZzzWebSocketEnvelope("heartbeat", DateTimeOffset.UtcNow, new { ok = true }),
                        jsonOptions,
                        sendLock,
                        connectionCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);

        try
        {
            await SendAsync(
                socket,
                new ZzzWebSocketEnvelope("heartbeat", DateTimeOffset.UtcNow, new { ok = true }),
                jsonOptions,
                sendLock,
                connectionCts.Token).ConfigureAwait(false);

            ZzzBackendResult<ZzzRunStatusDto> run = backend.GetCurrentRun();
            if (run.Success && run.Value is not null)
            {
                await SendAsync(
                    socket,
                    new ZzzWebSocketEnvelope("run.stateChanged", DateTimeOffset.UtcNow, run.Value),
                    jsonOptions,
                    sendLock,
                    connectionCts.Token).ConfigureAwait(false);
            }
            else
            {
                await SendAsync(
                    socket,
                    new ZzzWebSocketEnvelope("error.raised", DateTimeOffset.UtcNow, new
                    {
                        Code = run.ErrorCode,
                        Error = run.Error,
                    }),
                    jsonOptions,
                    sendLock,
                    connectionCts.Token).ConfigureAwait(false);
            }

            await foreach (ZzzBackendEvent item in reader.ReadAllAsync(connectionCts.Token).ConfigureAwait(false))
            {
                await SendAsync(
                    socket,
                    new ZzzWebSocketEnvelope(item.Type, item.Timestamp, item.Data),
                    jsonOptions,
                    sendLock,
                    connectionCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            connectionCts.Cancel();
            await heartbeatTask.ConfigureAwait(false);
            backend.UnsubscribeEvents(reader);
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task SendAsync(
        WebSocket socket,
        ZzzWebSocketEnvelope envelope,
        JsonSerializerOptions options,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, options);
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            sendLock.Release();
        }
    }
}

/// <summary>
/// 保存配置 scope 请求体。
/// </summary>
/// <param name="Values">待保存值。</param>
/// <param name="InstanceIndex">实例编号。</param>
/// <param name="GroupId">应用组编号。</param>
public sealed record ZzzConfigScopeSaveRequest(
    IReadOnlyDictionary<string, object?> Values,
    int? InstanceIndex = null,
    string? GroupId = null);
