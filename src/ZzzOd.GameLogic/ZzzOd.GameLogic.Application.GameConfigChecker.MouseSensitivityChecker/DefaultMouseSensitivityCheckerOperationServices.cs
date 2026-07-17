using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 默认鼠标灵敏度检测服务。
/// </summary>
public sealed class DefaultMouseSensitivityCheckerOperationServices : IMouseSensitivityCheckerOperationServices
{
	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	/// <inheritdoc />
	public Task<OperationResult> TransportToVideoStoreAsync(ZContext context)
	{
		return new Transport(context, "录像店", "房间").ExecuteAsync();
	}

	/// <inheritdoc />
	public bool IsGamepadMode(ZContext context)
	{
		return (context.Controller is ZPcController zPcController) ? zPcController.IsBackgroundMode : context.GameConfig.BackgroundMode;
	}

	/// <inheritdoc />
	public double? ReadViewAngle(ZContext context)
	{
		using Mat screen = context.Controller?.Screenshot().Screen;
		return context.WorldPatrolService.CutMiniMap(context, screen).ViewAngle;
	}

	/// <inheritdoc />
	public void TurnByDistance(ZContext context, int distance)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.TurnByDistance(distance);
		}
	}

	/// <inheritdoc />
	public void TurnGamepad(ZContext context, double durationSeconds)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.MoveGamepadRightStick(1f, 0f, TimeSpan.FromSeconds(durationSeconds));
		}
	}

	/// <inheritdoc />
	public void UpdateTurnDx(ZContext context, double turnDx)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.UpdateTurnDx((float)turnDx);
		}
	}

	/// <inheritdoc />
	public void UpdateGamepadTurnSpeed(ZContext context, double speed)
	{
		if (context.Controller is ZPcController zPcController)
		{
			zPcController.UpdateGamepadTurnSpeed((float)speed);
		}
	}
}
