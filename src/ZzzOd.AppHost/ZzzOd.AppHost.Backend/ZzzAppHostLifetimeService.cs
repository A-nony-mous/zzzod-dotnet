using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZzzOd.AppHost.Backend;

internal sealed class ZzzAppHostLifetimeService : IHostedService
{
	private readonly IZzzAppBackend _backend;

	private readonly ZzzBackendEventBus _eventBus;

	private readonly ZzzRunRoot _runRoot;

	private readonly ILogger<ZzzAppHostLifetimeService> _logger;

	public ZzzAppHostLifetimeService(IZzzAppBackend backend, ZzzBackendEventBus eventBus, ZzzRunRoot runRoot, ILogger<ZzzAppHostLifetimeService> logger)
	{
		_backend = backend;
		_eventBus = eventBus;
		_runRoot = runRoot;
		_logger = logger;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("AppHost 使用运行根目录 {RunRoot}，配置目录 {ConfigRoot}", _runRoot.Path, Path.Combine(_runRoot.Path, "config"));
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		try
		{
			ZzzRunStatusDto status = _backend.GetCurrentRun().Value ?? new ZzzRunStatusDto(ZzzRunState.Idle);
			ZzzRunState state = status.State;
			if ((uint)(state - 1) <= 3u)
			{
				await _backend.StopRunAsync().WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("宿主关闭等待运行停止超时。");
		}
		catch (Exception ex2)
		{
			Exception exception = ex2;
			_logger.LogWarning(exception, "宿主关闭时停止运行失败。");
		}
		finally
		{
			_eventBus.Complete();
		}
	}
}
