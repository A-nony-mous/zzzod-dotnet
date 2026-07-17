using System;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 默认自动战斗应用服务。
/// </summary>
public sealed class DefaultAutoBattleAppServices : IAutoBattleAppServices
{
	private readonly IVirtualGamepadDependencyChecker _virtualGamepadDependencyChecker;

	/// <summary>
	/// 初始化默认自动战斗应用服务。
	/// </summary>
	public DefaultAutoBattleAppServices(IVirtualGamepadDependencyChecker? virtualGamepadDependencyChecker = null)
	{
		_virtualGamepadDependencyChecker = virtualGamepadDependencyChecker ?? new ViGEmVirtualGamepadDependencyChecker();
	}

	/// <inheritdoc />
	public bool EnableKeyboard(ZContext context)
	{
		if (!(context.Controller is ZPcController { IsGameWindowReady: not false } zPcController))
		{
			return false;
		}
		zPcController.EnableKeyboard();
		return true;
	}

	/// <inheritdoc />
	public bool EnableXbox(ZContext context)
	{
		if (!(context.Controller is ZPcController { IsGameWindowReady: not false } zPcController))
		{
			return false;
		}
		zPcController.EnableXbox();
		return true;
	}

	/// <inheritdoc />
	public bool EnableDs4(ZContext context)
	{
		if (!(context.Controller is ZPcController { IsGameWindowReady: not false } zPcController))
		{
			return false;
		}
		zPcController.EnableDs4();
		return true;
	}

	/// <inheritdoc />
	public void SetGamepadKeyPressTime(ZContext context, float seconds)
	{
		(context.Controller as ZPcController)?.SetBackgroundGamepadKeyPressTime(TimeSpan.FromSeconds(seconds));
	}

	/// <inheritdoc />
	public bool IsVirtualGamepadInstalled()
	{
		return _virtualGamepadDependencyChecker.IsAvailable();
	}

	/// <inheritdoc />
	public AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName)
	{
		return context.AutoBattleContext.InitAutoOp(opName, subDir);
	}

	/// <inheritdoc />
	public void SetAutoUltimateEnabled(ZContext context, bool enabled)
	{
		context.AutoBattleContext.AutoUltimateEnabled = enabled;
	}

	/// <inheritdoc />
	public void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp)
	{
		context.EventBus.Publish("指令已加载", autoOp);
	}

	/// <inheritdoc />
	public void StartAutoBattle(ZContext context)
	{
		context.AutoBattleContext.StartAutoBattle();
	}

	/// <inheritdoc />
	public void StopAutoBattle(ZContext context)
	{
		context.AutoBattleContext.StopAutoBattle();
	}

	/// <inheritdoc />
	public void ResumeAutoBattle(ZContext context)
	{
		context.AutoBattleContext.ResumeAutoBattle();
	}

	/// <inheritdoc />
	public void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc, bool sync)
	{
		context.AutoBattleContext.CheckBattleState(screen, screenshotTimeUtc, checkBattleEndNormalResult: false, checkBattleEndHollowResult: false, checkBattleEndDefenseResult: false, checkDistance: false, sync);
	}
}
