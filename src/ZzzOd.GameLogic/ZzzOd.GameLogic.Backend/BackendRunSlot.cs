using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Operations;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Backend;

internal sealed class BackendRunSlot
{
	private static readonly DedicatedTaskScheduler ApplicationRunScheduler = new DedicatedTaskScheduler("zzz-application-run", 1);

	private static readonly TaskFactory ApplicationRunExecutor = new TaskFactory(ApplicationRunScheduler);

	private readonly ZContext _context;

	private readonly Lock _lock = new Lock();

	private Task<OperationResult>? _runTask;

	private CancellationTokenSource? _cancellationTokenSource;

	private string? _source;

	private string? _app;

	private DateTimeOffset? _startedAt;

	private DateTimeOffset? _finishedAt;

	private BackendRunState? _terminalState;

	private string? _lastStatus;

	private string? _failedNode;

	public BackendRunSlot(ZContext context)
	{
		_context = context;
	}

	public (bool Started, Task<OperationResult>? RunTask) Start(string source, Func<ZContext, Operation> operationFactory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source, "source");
		ArgumentNullException.ThrowIfNull(operationFactory, "operationFactory");
		using (_lock.EnterScope())
		{
			if (_runTask != null && !_runTask.IsCompleted)
			{
				return (Started: false, RunTask: null);
			}
			_source = source;
			_app = null;
			_startedAt = DateTimeOffset.UtcNow;
			_finishedAt = null;
			_terminalState = null;
			_lastStatus = null;
			_failedNode = null;
			_cancellationTokenSource = new CancellationTokenSource();
			_runTask = ApplicationRunExecutor.StartNew(() => RunAsync(source, operationFactory, _cancellationTokenSource.Token)).Unwrap();
			return (Started: true, RunTask: _runTask);
		}
	}

	public RunStatusResult QueryStatus()
	{
		using (_lock.EnterScope())
		{
			BackendRunState? terminalState = _terminalState;
			if (terminalState.HasValue)
			{
				string state = _terminalState.Value.ToSchemaValue();
				string? source = _source;
				string? app = _app;
				string? startedAt = FormatTimestamp(_startedAt);
				double? durationSeconds = GetDurationSeconds(_startedAt, _finishedAt);
				string lastStatus = _lastStatus;
				string failedNode = _failedNode;
				return new RunStatusResult(state, source, app, startedAt, durationSeconds, null, null, lastStatus, failedNode);
			}
			DateTimeOffset? startedAt2 = _startedAt;
			if (!startedAt2.HasValue)
			{
				return new RunStatusResult(BackendRunState.Idle.ToSchemaValue(), _source);
			}
			return new RunStatusResult(BackendRunState.Running.ToSchemaValue(), _source, _app, FormatTimestamp(_startedAt), GetDurationSeconds(_startedAt, DateTimeOffset.UtcNow));
		}
	}

	public StopRunResult Stop()
	{
		using (_lock.EnterScope())
		{
			if (_runTask == null || _runTask.IsCompleted || _cancellationTokenSource == null)
			{
				return new StopRunResult(Stopped: false, null, "当前无运行");
			}
			_cancellationTokenSource.Cancel();
			return new StopRunResult(Stopped: true, _source);
		}
	}

	private async Task<OperationResult> RunAsync(string source, Func<ZContext, Operation> operationFactory, CancellationToken cancellationToken)
	{
		string failedNode = null;
		OperationResult result;
		BackendRunState terminalState;
		try
		{
			if (!_context.RunContext.StartRunning())
			{
				result = new OperationResult(IsSuccess: false, "start_running 初始化失败");
				terminalState = BackendRunState.Failed;
			}
			else
			{
				Operation operation = operationFactory(_context);
				using (_lock.EnterScope())
				{
					_app = operation.GetType().Name;
				}
				try
				{
					result = await operation.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					terminalState = (result.IsSuccess ? BackendRunState.Success : BackendRunState.Failed);
				}
				catch (OperationCanceledException)
				{
					result = new OperationResult(IsSuccess: false, "人工结束");
					terminalState = BackendRunState.Stopped;
				}
			}
		}
		catch (OperationCanceledException)
		{
			result = new OperationResult(IsSuccess: false, "人工结束");
			terminalState = BackendRunState.Stopped;
		}
		catch (Exception ex3)
		{
			Exception exception = ex3;
			result = new OperationResult(IsSuccess: false, "执行异常");
			terminalState = BackendRunState.Failed;
			failedNode = exception.Message;
		}
		finally
		{
			await _context.RunContext.StopRunningAsync(TimeSpan.FromSeconds(1L)).ConfigureAwait(continueOnCapturedContext: false);
		}
		using (_lock.EnterScope())
		{
			_source = source;
			_terminalState = terminalState;
			_lastStatus = result.Status;
			_failedNode = failedNode;
			_finishedAt = DateTimeOffset.UtcNow;
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
			return result;
		}
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
}
