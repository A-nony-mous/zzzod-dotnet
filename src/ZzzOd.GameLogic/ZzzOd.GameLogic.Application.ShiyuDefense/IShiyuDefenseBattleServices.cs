using System;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战战斗服务。
/// </summary>
public interface IShiyuDefenseBattleServices
{
	/// <summary>加载自动战斗指令。</summary>
	void LoadAutoOperation(ZContext context, int predefinedTeamIndex);

	/// <summary>战斗画面是否已准备。</summary>
	bool IsBattleScreenReady(ZContext context, Mat? screen);

	/// <summary>战斗前移动。</summary>
	OperationResult PrepareBattle(ZContext context, Mat? screen);

	/// <summary>消费当前 Operation 截图执行一次自动战斗检查。</summary>
	OperationResult RunAutoBattle(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc);

	/// <summary>战斗后移动。</summary>
	OperationResult MoveAfterBattle(ZContext context, Mat? screen);

	/// <summary>停止自动战斗。</summary>
	void StopAutoBattle(ZContext context);

	/// <summary>主动退出前打开退出菜单。</summary>
	OperationResult PrepareVoluntaryExit(ZContext context, Mat? screen);
}
