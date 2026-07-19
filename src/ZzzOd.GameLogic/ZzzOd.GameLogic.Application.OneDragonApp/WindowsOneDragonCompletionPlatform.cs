using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// 一条龙自然完成后的 Windows 平台动作实现。
/// </summary>
public sealed class WindowsOneDragonCompletionPlatform : IZOneDragonCompletionPlatform
{
	private static readonly TimeSpan CloseGameRetryDelay = TimeSpan.FromSeconds(3L);

	private static readonly TimeSpan AfterCloseGameDelay = TimeSpan.FromSeconds(10L);

	/// <inheritdoc />
	public async Task<OperationResult> CloseGameAsync(ControllerBase? controller, CancellationToken cancellationToken)
	{
		if (controller == null)
		{
			return new OperationResult(IsSuccess: false, "未初始化游戏控制器。");
		}

		int retryCount = 0;
		while (controller.IsGameWindowReady && retryCount <= 3)
		{
			controller.CloseGame();
			await Task.Delay(CloseGameRetryDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			retryCount++;
		}

		if (controller.IsGameWindowReady)
		{
			return new OperationResult(IsSuccess: false, "检查是否关闭成功");
		}

		await Task.Delay(AfterCloseGameDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new OperationResult(IsSuccess: true);
	}

	/// <inheritdoc />
	public async Task<OperationResult> ShutdownAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			using Process? process = Process.Start(new ProcessStartInfo
			{
				FileName = "shutdown.exe",
				UseShellExecute = false,
				CreateNoWindow = true,
				ArgumentList = { "/s", "/t", "60" },
			});
			if (process == null)
			{
				return new OperationResult(IsSuccess: false, "无法启动 Windows 关机命令。");
			}

			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return process.ExitCode == 0
				? new OperationResult(IsSuccess: true, "已请求 Windows 关机")
				: new OperationResult(IsSuccess: false, $"Windows 关机命令失败，退出码 {process.ExitCode}");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return new OperationResult(IsSuccess: false, "Windows 关机失败: " + ex.Message);
		}
	}
}
