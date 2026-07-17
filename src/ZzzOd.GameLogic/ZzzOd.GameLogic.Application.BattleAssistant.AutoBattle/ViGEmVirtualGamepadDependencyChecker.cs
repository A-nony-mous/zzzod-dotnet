using OneDragon.Core.Logging;
using OneDragon.Core.Windows.Input;
using Serilog;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 基于 ViGEm 的虚拟手柄依赖检查器。
/// </summary>
public sealed class ViGEmVirtualGamepadDependencyChecker : IVirtualGamepadDependencyChecker
{
	private readonly ILogger _logger;

	/// <summary>
	/// 初始化 ViGEm 虚拟手柄依赖检查器。
	/// </summary>
	public ViGEmVirtualGamepadDependencyChecker(ILogger? logger = null)
	{
		_logger = logger ?? OneDragonLoggerFactory.CreateLogger(new OneDragonLogOptions());
	}

	/// <inheritdoc />
	public bool IsAvailable()
	{
		using ViGEmClientWrapper viGEmClientWrapper = new ViGEmClientWrapper(_logger);
		return viGEmClientWrapper.TryInitialize();
	}
}
