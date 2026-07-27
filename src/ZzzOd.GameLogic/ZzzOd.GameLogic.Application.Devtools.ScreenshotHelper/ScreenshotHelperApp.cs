using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 闪避截图应用。
/// </summary>
public sealed class ScreenshotHelperApp : ZApplication
{
	private readonly ScreenshotHelperService _service;

	private readonly bool _ownsService;

	private ControllerBase? _screenshotController;

	/// <summary>
	/// 应用配置。
	/// </summary>
	public ScreenshotHelperConfig Config { get; }

	/// <summary>
	/// 初始化闪避截图应用。
	/// </summary>
	public ScreenshotHelperApp(ZContext context, ScreenshotHelperConfig config, ZApplicationRunRecord? runRecord = null, ScreenshotHelperService? service = null)
		: base(context, "screenshot_helper", runRecord, "闪避截图")
	{
		Config = config;
		_ownsService = service == null;
		_service = service ?? new ScreenshotHelperService(config, new ZContextScreenshotHelperCaptureSource(context), new DebugScreenshotHelperImageStore(context.Environment), new ZContextScreenshotHelperDodgeDetector(context), new ZContextScreenshotHelperMiniMapAngleDetector(context), canAcceptKey: () => !context.RunContext.IsContextPause);
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		base.Context.Logger.Information("截图助手已暂停采集");
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override Task OnResumeAsync(CancellationToken cancellationToken)
	{
		base.Context.Logger.Information("截图助手已恢复采集");
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		ClearControllerScreenshotCache();
		_service.Dispose();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		try
		{
			ConfigureControllerScreenshotCache();
			if (_ownsService)
			{
				base.Context.AutoBattleContext.InitAutoOp(base.Context.BattleAssistantConfig.DodgeAssistantConfig, "dodge");
			}
			using (ScreenshotHelperGlobalInputSource.Subscribe(_service.HandleKeyPress))
			{
				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await WaitWhilePausedAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					ScreenshotHelperTickResult result = _service.CaptureAndProcess();
					await Task.Delay(result.NextDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		finally
		{
			_service.Dispose();
			ClearControllerScreenshotCache();
		}
	}

	private async Task WaitWhilePausedAsync(CancellationToken cancellationToken)
	{
		while (base.Context.RunContext.IsContextPause)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Delay(10, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private void ConfigureControllerScreenshotCache()
	{
		_screenshotController = base.Context.Controller ?? throw new InvalidOperationException("截图助手需要已初始化的游戏控制器。");
		_screenshotController.ScreenshotAliveTime = TimeSpan.FromSeconds(Config.LengthSecond + 1.0);
		_screenshotController.MaxScreenshotCount = checked((int)Math.Floor(Config.LengthSecond / Config.FrequencySecond) + 5);
	}

	private void ClearControllerScreenshotCache()
	{
		if (_screenshotController != null)
		{
			_screenshotController.ScreenshotAliveTime = TimeSpan.Zero;
			_screenshotController.MaxScreenshotCount = 0;
			_screenshotController = null;
		}
	}
}
