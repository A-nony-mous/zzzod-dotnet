using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;

/// <summary>
/// 闪避助手节点图。
/// </summary>
public sealed class DodgeAssistantOperation : ZOperation
{
	private readonly IAutoBattleAppServices _services;

	private bool _dodgeNodeActive;

	/// <summary>
	/// 当前是否处于闪避判断节点。
	/// </summary>
	public bool DodgeNodeActive => _dodgeNodeActive;

	/// <summary>
	/// 初始化闪避助手节点图。
	/// </summary>
	public DodgeAssistantOperation(ZContext context, IAutoBattleAppServices? services = null)
		: base(context, "闪避助手")
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
	/// 加载闪避指令。
	/// </summary>
	[NodeFrom("手柄检测")]
	[OperationNode("加载自动战斗指令")]
	public OperationRoundResult LoadOp()
	{
		try
		{
			AutoBattleOperator autoOp = _services.LoadAutoOp(base.ZContext, "dodge", base.ZContext.BattleAssistantConfig.DodgeAssistantConfig);
			_services.DispatchOpLoaded(base.ZContext, autoOp);
			_services.StartAutoBattle(base.ZContext);
			return RoundSuccess();
		}
		catch (Exception ex)
		{
			base.ZContext.Logger.Error(ex, "加载闪避指令失败");
			return RoundFail("加载指令失败: " + ex.Message);
		}
	}

	/// <summary>
	/// 闪避判断。
	/// </summary>
	[NodeFrom("加载自动战斗指令")]
	[OperationNode("闪避判断", Mute = true)]
	public OperationRoundResult CheckDodge()
	{
		_dodgeNodeActive = true;
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
	/// 停止闪避助手并清除当前节点标记，避免停止后的恢复事件重新启动已结束的应用。
	/// </summary>
	public void StopAutoBattle()
	{
		_dodgeNodeActive = false;
		_services.StopAutoBattle(base.ZContext);
	}

	/// <summary>
	/// 恢复自动战斗。
	/// </summary>
	public void ResumeAutoBattle()
	{
		if (_dodgeNodeActive)
		{
			_services.ResumeAutoBattle(base.ZContext);
		}
	}

	/// <inheritdoc />
	protected override Task OnAfterOperationDoneAsync(CancellationToken cancellationToken)
	{
		_dodgeNodeActive = false;
		return Task.CompletedTask;
	}
}
