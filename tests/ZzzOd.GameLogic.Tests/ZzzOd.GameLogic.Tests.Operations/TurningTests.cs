using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Operations;
using Xunit;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class TurningTests
{
	[Fact]
	public void TurnToAngle_ReturnsRetryWhenMiniMapMaskIsMissing()
	{
		AngleTurnCompensator compensator = new AngleTurnCompensator(delegate
		{
		});
		OperationRoundResult operationRoundResult = TurnToAngleHelper.TurnToAngle(new MiniMapAngleResult(PlayMaskFound: false, 90.0), compensator, 180.0, "转向正西");
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("未识别到小地图", operationRoundResult.Status);
	}

	[Fact]
	public void TurnToAngle_ReturnsRetryWhenAngleIsMissing()
	{
		AngleTurnCompensator compensator = new AngleTurnCompensator(delegate
		{
		});
		OperationRoundResult operationRoundResult = TurnToAngleHelper.TurnToAngle(new MiniMapAngleResult(PlayMaskFound: true, null), compensator, 180.0, "转向正西");
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("识别朝向失败", operationRoundResult.Status);
	}

	[Fact]
	public void TurnToAngle_TurnsByShortestDeltaWhenOutsideThreshold()
	{
		List<double> list = new List<double>();
		AngleTurnCompensator compensator = new AngleTurnCompensator(list.Add);
		OperationRoundResult operationRoundResult = TurnToAngleHelper.TurnToAngle(new MiniMapAngleResult(PlayMaskFound: true, 350.0), compensator, 10.0, "转向正东", 2.0, TimeSpan.Zero);
		Assert.Equal(OperationRoundResultKind.Retry, operationRoundResult.Kind);
		Assert.Equal("转向正东", operationRoundResult.Status);
		int num = 1;
		List<double> list2 = new List<double>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 20.0;
		Assert.Equal(list2, list);
	}

	[Fact]
	public void TurnToAngle_SucceedsWhenAngleWithinThreshold()
	{
		List<double> list = new List<double>();
		AngleTurnCompensator compensator = new AngleTurnCompensator(list.Add);
		OperationRoundResult operationRoundResult = TurnToAngleHelper.TurnToAngle(new MiniMapAngleResult(PlayMaskFound: true, 9.0), compensator, 10.0, "转向正东");
		Assert.Equal(OperationRoundResultKind.Success, operationRoundResult.Kind);
		Assert.Empty(list);
	}

	[Fact]
	public void AngleTurnCompensator_LearnsScaleFromObservedTurn()
	{
		AngleTurnCompensator angleTurnCompensator = new AngleTurnCompensator(delegate
		{
		});
		angleTurnCompensator.Learn(0.0, 90.0, 45.0);
		Assert.Equal(1.1, angleTurnCompensator.Scale, 6);
	}

	[Fact]
	public void AngleTurnCompensator_UnfoldsReverseObservationNearOneEightyDegrees()
	{
		AngleTurnCompensator angleTurnCompensator = new AngleTurnCompensator(delegate
		{
		});
		angleTurnCompensator.Learn(0.0, 185.0, 185.0);
		Assert.Equal(1.0, angleTurnCompensator.Scale, 6);
	}

	[Fact]
	public void AngleTurnCompensator_TurnFromAngleLearnsPreviousSampleBeforeNextTurn()
	{
		List<double> list = new List<double>();
		AngleTurnCompensator angleTurnCompensator = new AngleTurnCompensator(list.Add);
		double actual = angleTurnCompensator.TurnFromAngle(0.0, 90.0);
		double actual2 = angleTurnCompensator.TurnFromAngle(45.0, 90.0);
		Assert.Equal(90.0, actual);
		Assert.Equal(99.0, actual2, 6);
		Assert.Collection(list, delegate(double command)
		{
			Assert.Equal(90.0, command);
		}, delegate(double command)
		{
			Assert.Equal(99.0, command, 6);
		});
		Assert.Equal(1.1, angleTurnCompensator.Scale, 6);
	}

	[Fact]
	public void AngleTurnCompensator_TurnClampsEffectiveAngle()
	{
		List<double> list = new List<double>();
		AngleTurnCompensator angleTurnCompensator = new AngleTurnCompensator(list.Add);
		double actual = angleTurnCompensator.Turn(90.0, 45.0);
		Assert.Equal(45.0, actual);
		int num = 1;
		List<double> list2 = new List<double>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 45.0;
		Assert.Equal(list2, list);
	}
}
