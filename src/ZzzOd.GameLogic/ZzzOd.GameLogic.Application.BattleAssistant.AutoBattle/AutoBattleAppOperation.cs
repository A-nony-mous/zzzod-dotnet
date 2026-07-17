using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 自动战斗应用节点图。
/// </summary>
public sealed class AutoBattleAppOperation : ZOperation
{
	private readonly IAutoBattleAppServices _services;

	private bool _screenNodeActive;

	/// <summary>
	/// 当前是否处于画面识别节点。
	/// </summary>
	public bool ScreenNodeActive => _screenNodeActive;

	/// <summary>
	/// 初始化自动战斗应用节点图。
	/// </summary>
	public AutoBattleAppOperation(ZContext context, IAutoBattleAppServices? services = null)
		: base(context, "自动战斗")
	{
		_services = services ?? new DefaultAutoBattleAppServices();
	}

	/// <summary>
	/// 手柄检测。
	/// </summary>
	[OperationNode("手柄检测", IsStartNode = true)]
	public OperationRoundResult CheckGamepad()
	{
		string controlMethod = base.ZContext.BattleAssistantConfig.ControlMethod;
		if (string.Equals(controlMethod, "keyboard", StringComparison.OrdinalIgnoreCase))
		{
			return _services.EnableKeyboard(base.ZContext) ? RoundSuccess("无需手柄") : RoundFail("游戏控制器未就绪");
		}
		if (!_services.IsVirtualGamepadInstalled())
		{
			if (!_services.EnableKeyboard(base.ZContext))
			{
				return RoundFail("游戏控制器未就绪");
			}
			return RoundFail("未安装虚拟手柄依赖");
		}
		if (string.Equals(controlMethod, "xbox", StringComparison.OrdinalIgnoreCase))
		{
			if (!_services.EnableXbox(base.ZContext))
			{
				return RoundFail("游戏控制器未就绪");
			}
			_services.SetGamepadKeyPressTime(base.ZContext, base.ZContext.GameConfig.XboxKeyPressTime);
			return RoundSuccess("已安装虚拟手柄依赖");
		}
		if (string.Equals(controlMethod, "ds4", StringComparison.OrdinalIgnoreCase))
		{
			if (!_services.EnableDs4(base.ZContext))
			{
				return RoundFail("游戏控制器未就绪");
			}
			_services.SetGamepadKeyPressTime(base.ZContext, base.ZContext.GameConfig.Ds4KeyPressTime);
			return RoundSuccess("已安装虚拟手柄依赖");
		}
		return RoundSuccess("已安装虚拟手柄依赖");
	}

	/// <summary>
	/// 加载自动战斗指令。
	/// </summary>
	[NodeFrom("手柄检测")]
	[OperationNode("加载自动战斗指令")]
	public OperationRoundResult LoadOp()
	{
		try
		{
			AutoBattleOperator autoOp = _services.LoadAutoOp(base.ZContext, "auto_battle", base.ZContext.BattleAssistantConfig.AutoBattleConfig);
			_services.SetAutoUltimateEnabled(base.ZContext, base.ZContext.BattleAssistantConfig.AutoUltimateEnabled);
			_services.DispatchOpLoaded(base.ZContext, autoOp);
			_services.StartAutoBattle(base.ZContext);
			_services.SetAutoUltimateEnabled(base.ZContext, base.ZContext.BattleAssistantConfig.AutoUltimateEnabled);
			return RoundSuccess();
		}
		catch (Exception exception)
		{
			base.ZContext.Logger.Error(exception, "加载自动战斗指令失败");
			return RoundFail("加载指令失败");
		}
	}

	/// <summary>
	/// 画面识别。
	/// </summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("画面识别", Mute = true)]
	public OperationRoundResult CheckScreen()
	{
		_screenNodeActive = true;
		if (base.LastScreenshot == null || base.LastScreenshot.Empty())
		{
			return RoundRetry("未获取截图");
		}
		_services.CheckBattleState(base.ZContext, base.LastScreenshot, base.LastScreenshotTimeUtc, sync: false);
		return RoundWaitForScreenshotRound(TimeSpan.FromSeconds(base.ZContext.BattleAssistantConfig.ScreenshotInterval));
	}

	/// <summary>
	/// 暂停自动战斗。
	/// </summary>
	public void PauseAutoBattle()
	{
		_services.StopAutoBattle(base.ZContext);
	}

	/// <summary>
	/// 停止自动战斗并清除当前节点标记，避免停止后的恢复事件重新启动已结束的应用。
	/// </summary>
	public void StopAutoBattle()
	{
		_screenNodeActive = false;
		_services.StopAutoBattle(base.ZContext);
	}

	/// <summary>
	/// 恢复自动战斗。
	/// </summary>
	public void ResumeAutoBattle()
	{
		if (_screenNodeActive)
		{
			_services.ResumeAutoBattle(base.ZContext);
		}
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		_screenNodeActive = false;
		return Task.CompletedTask;
	}
}
