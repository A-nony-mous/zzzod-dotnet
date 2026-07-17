using System;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Utils;

namespace ZzzOd.GameLogic.Operations.Turning;

/// <summary>
/// 转向到目标角度的纯逻辑 helper。
/// </summary>
public static class TurnToAngleHelper
{
	/// <summary>
	/// 根据当前小地图朝向决定是否继续转向。
	/// </summary>
	public static OperationRoundResult TurnToAngle(MiniMapAngleResult miniMap, AngleTurnCompensator compensator, double targetAngle, string turnStatus, double angleThreshold = 2.0, TimeSpan? turnWait = null)
	{
		if (!miniMap.PlayMaskFound)
		{
			return new OperationRoundResult(OperationRoundResultKind.Retry, "未识别到小地图", null, TimeSpan.FromSeconds(1L));
		}
		if (!miniMap.ViewAngle.HasValue)
		{
			return new OperationRoundResult(OperationRoundResultKind.Retry, "识别朝向失败", null, TimeSpan.FromSeconds(1L));
		}
		double num = CalUtils.AngleDelta(miniMap.ViewAngle.Value, targetAngle);
		if (Math.Abs(num) > angleThreshold)
		{
			compensator.TurnFromAngle(miniMap.ViewAngle.Value, num);
			return new OperationRoundResult(OperationRoundResultKind.Retry, turnStatus, null, turnWait ?? TimeSpan.FromMilliseconds(500L));
		}
		return new OperationRoundResult(OperationRoundResultKind.Success);
	}
}
