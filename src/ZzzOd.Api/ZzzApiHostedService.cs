using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Api;

/// <summary>
/// GUI 进程内 API 服务。
/// </summary>
public sealed class ZzzApiHostedService : IHostedService, IZzzApiServerController
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IZzzAppBackend _backend;
    private readonly ZzzApiOptions _options;
    private readonly ZzzRunRoot _runRoot;
    private readonly ILogger<ZzzApiHostedService> _logger;
    private WebApplication? _app;
    private Task? _runTask;
    private string? _lastError;

    /// <summary>
    /// 初始化 GUI 进程内 API 服务。
    /// </summary>
    /// <param name="backend">业务门面。</param>
    /// <param name="options">API 配置。</param>
    /// <param name="runRoot">运行根目录。</param>
    /// <param name="logger">日志。</param>
    public ZzzApiHostedService(
        IZzzAppBackend backend,
        ZzzApiOptions options,
        ZzzRunRoot runRoot,
        ILogger<ZzzApiHostedService> logger)
    {
        _backend = backend;
        _options = options;
        _runRoot = runRoot;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Enabled && _options.StartWithGui)
        {
            await StartServerAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => StopServerAsync(cancellationToken);

    /// <inheritdoc />
    public ZzzApiServerStatusDto GetStatus() =>
        new(_app is not null, _options.Enabled, GetUrl(), _lastError);

    /// <inheritdoc />
    public async Task<ZzzApiServerStatusDto> StartServerAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_app is not null)
            {
                return GetStatus();
            }

            if (!_options.Enabled)
            {
                _lastError = "API 配置未启用。";
                return GetStatus();
            }

            try
            {
                WebApplication app = BuildApplication();
                _runTask = app.RunAsync(cancellationToken);
                _app = app;
                _lastError = null;
                _logger.LogInformation("GUI 进程内 API 服务已启动：{Url}", GetUrl());
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
                _logger.LogWarning(exception, "GUI 进程内 API 服务启动失败。");
            }

            return GetStatus();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ZzzApiServerStatusDto> StopServerAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_app is null)
            {
                return GetStatus();
            }

            WebApplication app = _app;
            Task? runTask = _runTask;
            _app = null;
            _runTask = null;
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
            if (runTask is not null)
            {
                await runTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            }

            await app.DisposeAsync().ConfigureAwait(false);
            _lastError = null;
            _logger.LogInformation("GUI 进程内 API 服务已停止。");
            return GetStatus();
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
            _logger.LogWarning(exception, "GUI 进程内 API 服务停止失败。");
            return GetStatus();
        }
        finally
        {
            _gate.Release();
        }
    }

    private WebApplication BuildApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = _runRoot.Path,
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_backend);
        builder.Services.AddSingleton(_options);
        builder.Services.AddZzzApiServices();
        builder.Services.AddZzzApiCors(_options);
        builder.WebHost.UseUrls(GetUrl());
        WebApplication app = builder.Build();
        app.UseZzzApiCors(_options);
        app.MapZzzApiEndpoints();
        return app;
    }

    private string GetUrl() => $"http://{_options.ListenAddress}:{_options.Port}";
}
