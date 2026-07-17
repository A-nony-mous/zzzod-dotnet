using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 业务后端入口。
/// </summary>
public sealed class ZzzBackendContext
{
	private readonly ZContext _context;

	private readonly BackendRunSlot _runSlot;

	public bool IsStarted { get; private set; }

	public ZzzBackendContext(ZContext context)
	{
		_context = context;
		_runSlot = new BackendRunSlot(context);
	}

	public void Start()
	{
		IsStarted = true;
	}

	public void Shutdown()
	{
		_runSlot.Stop();
		IsStarted = false;
	}

	public WindowStatus CheckWindow()
	{
		EnsureReady();
		ControllerBase controllerBase = _context.Controller ?? throw new BackendNotReadyException("控制器未初始化。");
		if (controllerBase is IBackendWindowStatusProvider backendWindowStatusProvider)
		{
			return backendWindowStatusProvider.GetWindowStatus();
		}
		return new WindowStatus(null, controllerBase.IsGameWindowReady, controllerBase.IsGameWindowReady, IsWinScale: false);
	}

	public Mat Capture()
	{
		EnsureReady();
		ControllerBase controllerBase = _context.Controller ?? throw new BackendNotReadyException("控制器未初始化。");
		if (!controllerBase.IsGameWindowReady)
		{
			throw new BackendNotReadyException("游戏窗口未就绪。");
		}
		Mat item = controllerBase.Screenshot().Screen;
		return item ?? throw new BackendNotReadyException("截图失败。");
	}

	public string CloseGame()
	{
		EnsureReady();
		ControllerBase controllerBase = _context.Controller ?? throw new BackendNotReadyException("控制器未初始化。");
		if (!controllerBase.IsGameWindowReady)
		{
			throw new BackendNotReadyException("游戏窗口未就绪。");
		}
		controllerBase.CloseGame();
		return "已发送关闭游戏信号,可用 check_window 验证";
	}

	public (bool Started, Task<OperationResult>? RunTask) StartRun(string source, Func<ZContext, Operation> operationFactory)
	{
		return _runSlot.Start(source, operationFactory);
	}

	public RunStatusResult QueryStatus()
	{
		return _runSlot.QueryStatus();
	}

	public StopRunResult Stop()
	{
		return _runSlot.Stop();
	}

	private void EnsureReady()
	{
		if (!_context.ReadyForApplication)
		{
			throw new BackendNotReadyException("ZContext 未就绪。");
		}
	}
}
