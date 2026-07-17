using System;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 使用 ZContext 的控制方式切换器。
/// </summary>
public sealed class ZContextOperationDebugControllerModeSwitcher : IOperationDebugControllerModeSwitcher
{
	private readonly ZContext _context;

	private readonly Func<bool> _isVirtualGamepadInstalled;

	/// <summary>
	/// 初始化切换器。
	/// </summary>
	public ZContextOperationDebugControllerModeSwitcher(ZContext context, Func<bool>? isVirtualGamepadInstalled = null)
	{
		_context = context;
		_isVirtualGamepadInstalled = isVirtualGamepadInstalled ?? new Func<bool>(new ViGEmVirtualGamepadDependencyChecker().IsAvailable);
	}

	/// <inheritdoc />
	public OperationDebugControllerModeResult CheckAndApply()
	{
		string controlMethod = _context.BattleAssistantConfig.ControlMethod;
		if (string.Equals(controlMethod, "keyboard", StringComparison.OrdinalIgnoreCase))
		{
			(_context.Controller as ZPcController)?.EnableKeyboard();
			return new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄");
		}
		if (!_isVirtualGamepadInstalled())
		{
			(_context.Controller as ZPcController)?.EnableKeyboard();
			return new OperationDebugControllerModeResult(IsSuccess: false, "未安装虚拟手柄依赖");
		}
		if (string.Equals(controlMethod, "xbox", StringComparison.OrdinalIgnoreCase))
		{
			(_context.Controller as ZPcController)?.EnableXbox();
			(_context.Controller as ZPcController)?.SetBackgroundGamepadKeyPressTime(TimeSpan.FromSeconds(_context.GameConfig.XboxKeyPressTime));
			return new OperationDebugControllerModeResult(IsSuccess: true, "已安装虚拟手柄依赖");
		}
		if (string.Equals(controlMethod, "ds4", StringComparison.OrdinalIgnoreCase))
		{
			(_context.Controller as ZPcController)?.EnableDs4();
			(_context.Controller as ZPcController)?.SetBackgroundGamepadKeyPressTime(TimeSpan.FromSeconds(_context.GameConfig.Ds4KeyPressTime));
			return new OperationDebugControllerModeResult(IsSuccess: true, "已安装虚拟手柄依赖");
		}
		(_context.Controller as ZPcController)?.EnableKeyboard();
		return new OperationDebugControllerModeResult(IsSuccess: true, "无需手柄");
	}
}
