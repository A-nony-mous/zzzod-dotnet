using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 路线执行服务。
/// </summary>
public interface IWorldPatrolRunRouteServices
{
	/// <summary>当前时间。</summary>
	DateTimeOffset Now { get; }

	/// <summary>战斗轮询等待时间。</summary>
	TimeSpan BattleWaitDelay { get; }

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>传送到路线起点。</summary>
	Task<OperationResult> TransportAsync(ZContext context, WorldPatrolRoute route, CancellationToken cancellationToken);

	/// <summary>获取路线指定指令前位置。</summary>
	WorldPatrolPoint? GetRoutePosBeforeOpIdx(ZContext context, WorldPatrolRoute route, int opIdx);

	/// <summary>获取路线大地图。</summary>
	WorldPatrolLargeMap? GetRouteLargeMap(ZContext context, WorldPatrolRoute route);

	/// <summary>裁剪小地图。</summary>
	WorldPatrolMiniMapSnapshot CutMiniMap(ZContext context, Mat? screen);

	/// <summary>计算当前位置。</summary>
	WorldPatrolPoint? CalculateCurrentPosition(ZContext context, WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect possibleRect);

	/// <summary>停止前进。</summary>
	void StopMovingForward(ZContext context);

	/// <summary>开始前进。</summary>
	void StartMovingForward(ZContext context);

	/// <summary>纵向转向。</summary>
	void TurnVerticalByDistance(ZContext context, double distance);

	/// <summary>按角度转向。</summary>
	void TurnByAngleDiff(ZContext context, double angleDiff);

	/// <summary>切换最佳移动角色。</summary>
	void SwitchToBestAgentForMoving(ZContext context);

	/// <summary>切人脱困。</summary>
	void SwitchNextForUnstuck(ZContext context);

	/// <summary>执行脱困移动。</summary>
	void MoveUnstuck(ZContext context, int direction, string tag);

	/// <summary>初始化自动战斗。</summary>
	void InitAutoBattle(ZContext context, string autoBattleName);

	/// <summary>开始自动战斗。</summary>
	void StartAutoBattle(ZContext context);

	/// <summary>停止自动战斗。</summary>
	void StopAutoBattle(ZContext context);

	/// <summary>等待自动战斗停止后按键松开。</summary>
	void WaitAfterAutoBattleStop(ZContext context);

	/// <summary>检测战斗状态。</summary>
	WorldPatrolBattleCheckResult CheckBattleState(ZContext context, Mat? screen, DateTimeOffset screenshotTime);

	/// <summary>是否出现交互按钮。</summary>
	bool HasInteraction(ZContext context, Mat? screen);
}
