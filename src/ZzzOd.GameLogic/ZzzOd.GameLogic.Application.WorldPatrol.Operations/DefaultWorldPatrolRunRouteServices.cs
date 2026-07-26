using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OpenCvSharp;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 默认路线执行服务。
/// </summary>
public sealed class DefaultWorldPatrolRunRouteServices : IWorldPatrolRunRouteServices
{
	private readonly ZContext _context;

	/// <summary>
	/// 初始化默认路线执行服务。
	/// </summary>
	public DefaultWorldPatrolRunRouteServices(ZContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		_context = context;
	}

	/// <inheritdoc />
	public DateTimeOffset Now => DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public TimeSpan BattleWaitDelay => TimeSpan.FromSeconds(_context.BattleAssistantConfig.ScreenshotInterval);

	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new BackToNormalWorld(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context, WorldPatrolRoute route, CancellationToken cancellationToken)
	{
		if (route.TpArea == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "路线区域为空"));
		}
		return new TransportBy3dMap(context, route.TpArea, route.TpName).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public WorldPatrolPoint? GetRoutePosBeforeOpIdx(ZContext context, WorldPatrolRoute route, int opIdx)
	{
		return context.WorldPatrolService.GetRoutePosBeforeOpIdx(route, opIdx);
	}

	/// <inheritdoc />
	public WorldPatrolLargeMap? GetRouteLargeMap(ZContext context, WorldPatrolRoute route)
	{
		return context.WorldPatrolService.GetRouteLargeMap(route);
	}

	/// <inheritdoc />
	public WorldPatrolMiniMapSnapshot CutMiniMap(ZContext context, Mat? screen)
	{
		return context.WorldPatrolService.CutMiniMap(context, screen);
	}

	/// <inheritdoc />
	public WorldPatrolPoint? CalculateCurrentPosition(ZContext context, WorldPatrolLargeMap largeMap, WorldPatrolMiniMapSnapshot miniMap, OneDragon.Core.Abstractions.Geometry.Rect possibleRect)
	{
		return context.WorldPatrolService.CalculateCurrentPosition(context, largeMap, miniMap, possibleRect);
	}

	/// <inheritdoc />
	public void StopMovingForward(ZContext context)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.StopMovingForward();
		}
	}

	/// <inheritdoc />
	public void StartMovingForward(ZContext context)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.StartMovingForward();
		}
	}

	/// <inheritdoc />
	public void TurnVerticalByDistance(ZContext context, double distance)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.TurnVerticalByDistance((float)distance);
		}
	}

	/// <inheritdoc />
	public void TurnByAngleDiff(ZContext context, double angleDiff)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.TurnByAngleDiff((float)angleDiff);
		}
	}

	/// <inheritdoc />
	public void SwitchToBestAgentForMoving(ZContext context)
	{
		AutoBattleUtils.SwitchToBestAgentForMoving(context);
	}

	/// <inheritdoc />
	public void SwitchNextForUnstuck(ZContext context)
	{
		context.AutoBattleContext.SwitchNext();
	}

	/// <inheritdoc />
	public void MoveUnstuck(ZContext context, int direction, string tag)
	{
		if (context.Controller is ZPcController zPcController)
		{
			TimeSpan value = TimeSpan.FromSeconds((direction <= 3) ? 1 : 2);
			switch (direction)
			{
			case 0:
				zPcController.MoveA(press: true, TimeSpan.FromSeconds(1L), release: true);
				break;
			case 1:
				zPcController.MoveD(press: true, TimeSpan.FromSeconds(1L), release: true);
				break;
			case 2:
			case 4:
				zPcController.MoveS(press: true, value, release: true);
				zPcController.MoveA(press: true, value, release: true);
				zPcController.MoveW(press: true, value, release: true);
				break;
			case 3:
			case 5:
				zPcController.MoveS(press: true, value, release: true);
				zPcController.MoveD(press: true, value, release: true);
				zPcController.MoveW(press: true, value, release: true);
				break;
			}
		}
	}

	/// <inheritdoc />
	public void InitAutoBattle(ZContext context, string autoBattleName)
	{
		if (context.AutoBattleContext.AutoOp == null)
		{
			context.AutoBattleContext.InitAutoOp(autoBattleName);
		}
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
	public void WaitAfterAutoBattleStop(ZContext context)
	{
		Thread.Sleep(TimeSpan.FromSeconds(5L));
	}

	/// <inheritdoc />
	public WorldPatrolBattleCheckResult CheckBattleState(ZContext context, Mat? screen, DateTimeOffset screenshotTime)
	{
		if (screen == null || screen.Empty())
		{
			return new WorldPatrolBattleCheckResult(context.AutoBattleContext.LastCheckInBattle, null, FrameValid: false);
		}
		context.AutoBattleContext.CheckBattleState(screen, screenshotTime, checkBattleEndNormalResult: true);
		return new WorldPatrolBattleCheckResult(context.AutoBattleContext.LastCheckInBattle);
	}

	/// <inheritdoc />
	public bool HasInteraction(ZContext context, Mat? screen)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, "战斗画面", "按键-交互") == FindAreaResultEnum.True;
	}
}
