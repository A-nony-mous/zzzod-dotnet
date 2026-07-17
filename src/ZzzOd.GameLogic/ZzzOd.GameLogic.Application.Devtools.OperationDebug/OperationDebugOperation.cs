using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试 Operation 节点图。
/// </summary>
public sealed class OperationDebugOperation : Operation
{
	private readonly OperationDebugService _service;

	/// <summary>
	/// 初始化 Operation。
	/// </summary>
	public OperationDebugOperation(ZContext context, OperationDebugService service)
		: base(context, "指令调试", 1)
	{
		_service = service;
	}

	/// <summary>
	/// 检测手柄。
	/// </summary>
	[OperationNode("手柄检测", IsStartNode = true, ScreenshotBeforeRound = false)]
	public OperationRoundResult CheckGamepad()
	{
		OperationDebugControllerModeResult operationDebugControllerModeResult = _service.CheckGamepad();
		return operationDebugControllerModeResult.IsSuccess ? RoundSuccess(operationDebugControllerModeResult.Status) : RoundFail(operationDebugControllerModeResult.Status);
	}

	/// <summary>
	/// 加载动作指令。
	/// </summary>
	[NodeFrom("手柄检测")]
	[OperationNode("加载动作指令", ScreenshotBeforeRound = false)]
	public OperationRoundResult LoadOperations()
	{
		try
		{
			OperationDebugStepResult operationDebugStepResult = _service.LoadOperations();
			return operationDebugStepResult.IsSuccess ? RoundSuccess(operationDebugStepResult.Status) : RoundFail(operationDebugStepResult.Status);
		}
		catch (Exception ex)
		{
			return RoundFail("指令模板加载失败", ex.Message);
		}
	}

	/// <summary>
	/// 执行动作指令。
	/// </summary>
	[NodeFrom("加载动作指令")]
	[OperationNode("执行指令", ScreenshotBeforeRound = false)]
	public OperationRoundResult RunOperations()
	{
		OperationDebugStepResult operationDebugStepResult = _service.RunNextOperation();
		if (!operationDebugStepResult.IsSuccess)
		{
			return RoundFail(operationDebugStepResult.Status);
		}
		return operationDebugStepResult.Completed ? RoundSuccess(operationDebugStepResult.Status) : RoundWait(operationDebugStepResult.Status);
	}
}
