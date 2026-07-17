using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Windows.Controller;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations.EnterGame;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用基类。
/// </summary>
public abstract class ZApplication : IApplication
{
	private readonly Func<CancellationToken, Task<OperationResult>>? _enterGameAsync;

	private readonly Func<ZContext, CancellationToken, Task<OperationResult>> _defaultEnterGameAsync;

	private readonly Action<OperationResult>? _operationCallback;

	/// <summary>
	/// ZZZ 上下文。
	/// </summary>
	protected ZContext Context { get; }

	/// <inheritdoc />
	public string AppId { get; }

	/// <summary>
	/// 应用运行记录。
	/// </summary>
	public ZApplicationRunRecord? RunRecord { get; }

	/// <summary>
	/// 应用显示名称。
	/// </summary>
	public string AppName { get; }

	/// <summary>
	/// 节点最大重试次数。
	/// </summary>
	public int NodeMaxRetryTimes { get; }

	/// <summary>
	/// 应用超时时间。
	/// </summary>
	public TimeSpan? Timeout { get; }

	/// <summary>
	/// 运行前是否需要检查并进入游戏窗口。
	/// </summary>
	public bool NeedCheckGameWindow { get; }

	/// <summary>
	/// 初始化 ZZZ 应用。
	/// </summary>
	protected ZApplication(ZContext context, string appId, ZApplicationRunRecord? runRecord = null, string? appName = null, int nodeMaxRetryTimes = 1, TimeSpan? timeout = null, bool needCheckGameWindow = true, Func<CancellationToken, Task<OperationResult>>? enterGameAsync = null, Func<ZContext, CancellationToken, Task<OperationResult>>? defaultEnterGameAsync = null, Action<OperationResult>? operationCallback = null)
	{
		Context = context;
		AppId = appId;
		RunRecord = runRecord;
		AppName = appName ?? appId;
		NodeMaxRetryTimes = nodeMaxRetryTimes;
		Timeout = timeout;
		NeedCheckGameWindow = needCheckGameWindow;
		_enterGameAsync = enterGameAsync;
		_defaultEnterGameAsync = defaultEnterGameAsync ?? new Func<ZContext, CancellationToken, Task<OperationResult>>(RunDefaultEnterGameAsync);
		_operationCallback = operationCallback;
	}

	/// <inheritdoc />
	public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken)
	{
		RunRecord?.CheckAndUpdateStatus();
		RunRecord?.UpdateStatus(3);
		bool needNotify = Context.RunContext.IsAppNeedNotify(AppId);
		if (needNotify)
		{
			Context.OperationNotificationService.OnApplicationStart(AppId, AppName);
		}
		try
		{
			if (NeedCheckGameWindow)
			{
				OperationResult enterGameResult = await EnterGameAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!enterGameResult.IsSuccess)
				{
					RunRecord?.UpdateStatus(2);
					if (needNotify)
					{
						Context.OperationNotificationService.OnApplicationCompleted(AppId, AppName, success: false);
					}
					_operationCallback?.Invoke(enterGameResult);
					return enterGameResult;
				}
			}
			OperationResult result = await ExecuteCoreAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			RunRecord?.UpdateStatus(result.IsSuccess ? 1 : 2);
			if (needNotify)
			{
				Context.OperationNotificationService.OnApplicationCompleted(AppId, AppName, result.IsSuccess);
			}
			_operationCallback?.Invoke(result);
			return result;
		}
		catch (OperationCanceledException)
		{
			RunRecord?.UpdateStatus(2);
			if (needNotify)
			{
				Context.OperationNotificationService.OnApplicationCompleted(AppId, AppName, success: false);
			}
			throw;
		}
		catch
		{
			RunRecord?.UpdateStatus(2);
			if (needNotify)
			{
				Context.OperationNotificationService.OnApplicationCompleted(AppId, AppName, success: false);
			}
			throw;
		}
	}

	/// <inheritdoc />
	public virtual Task OnPauseAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public virtual Task OnResumeAsync(CancellationToken cancellationToken)
	{
		if (Context.Controller is WindowsGameController windowsGameController)
		{
			windowsGameController.ActivateWindow();
		}
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public virtual Task OnStopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	/// <summary>
	/// 执行应用主体逻辑。
	/// </summary>
	protected abstract Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken);

	/// <summary>
	/// 运行主体前进入游戏。
	/// </summary>
	protected virtual Task<OperationResult> EnterGameAsync(CancellationToken cancellationToken)
	{
		ControllerBase? controller = Context.Controller;
		if (controller != null && controller.IsGameWindowReady)
		{
			return Task.FromResult(new OperationResult(IsSuccess: true));
		}
		return _enterGameAsync?.Invoke(cancellationToken) ?? _defaultEnterGameAsync(Context, cancellationToken);
	}

	private static Task<OperationResult> RunDefaultEnterGameAsync(ZContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return new OpenAndEnterGame(context).ExecuteAsync();
	}

	/// <summary>
	/// 创建成功结果。
	/// </summary>
	protected static OperationResult Success(string? status = null, object? data = null)
	{
		return new OperationResult(IsSuccess: true, status, data);
	}

	/// <summary>
	/// 创建失败结果。
	/// </summary>
	protected static OperationResult Fail(string? status = null, object? data = null)
	{
		return new OperationResult(IsSuccess: false, status, data);
	}
}
