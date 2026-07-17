using System;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 自动战斗应用服务。
/// </summary>
public interface IAutoBattleAppServices
{
	/// <summary>启用键鼠。</summary>
	bool EnableKeyboard(ZContext context);

	/// <summary>启用 Xbox。</summary>
	bool EnableXbox(ZContext context);

	/// <summary>启用 DS4。</summary>
	bool EnableDs4(ZContext context);

	/// <summary>设置手柄按键时长。</summary>
	void SetGamepadKeyPressTime(ZContext context, float seconds);

	/// <summary>虚拟手柄依赖是否可用。</summary>
	bool IsVirtualGamepadInstalled();

	/// <summary>加载自动战斗指令。</summary>
	AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName);

	/// <summary>设置自动终结技开关。</summary>
	void SetAutoUltimateEnabled(ZContext context, bool enabled);

	/// <summary>发布指令已加载事件。</summary>
	void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp);

	/// <summary>启动自动战斗。</summary>
	void StartAutoBattle(ZContext context);

	/// <summary>停止自动战斗。</summary>
	void StopAutoBattle(ZContext context);

	/// <summary>恢复自动战斗。</summary>
	void ResumeAutoBattle(ZContext context);

	/// <summary>检查战斗状态。</summary>
	void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc, bool sync);
}
