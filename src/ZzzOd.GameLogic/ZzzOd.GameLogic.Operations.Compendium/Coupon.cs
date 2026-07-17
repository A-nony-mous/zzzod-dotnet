using System;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.Compendium;

/// <summary>
/// 处理家政券使用弹窗。
/// </summary>
public sealed class Coupon : ZOperation
{
	/// <summary>家政券可继续使用。</summary>
	public const string StatusCouponAvailable = "可以使用家政券";

	/// <summary>继续使用电量。</summary>
	public const string StatusContinueRunWithCharge = "继续使用电量";

	private readonly ChargePlanItem _plan;

	private readonly ChargePlanConfig _config;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	/// <summary>
	/// 初始化家政券操作。
	/// </summary>
	public Coupon(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "处理家政券")
	{
		_plan = plan;
		_config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.RunContext.CurrentGroupId ?? "one_dragon");
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	[NodeFrom("关闭弹窗", Status = "可以使用家政券")]
	[OperationNode("使用", IsStartNode = true)]
	private OperationRoundResult UseCoupon()
	{
		OperationRoundResult operationRoundResult = RoundByClickArea("家政券", "使用", clickLeftTop: false, _preClickDelay, TimeSpan.FromMilliseconds(500L), _retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L)) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("使用")]
	[OperationNode("确认")]
	private OperationRoundResult ConfirmUseCoupon()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "家政券", "确认", _preClickDelay, TimeSpan.FromMilliseconds(500L), _retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			_config.AddPlanRunTimes(_plan);
			return RoundSuccess("可以使用家政券", null, TimeSpan.FromMilliseconds(500L));
		}
		return (_plan.RunTimes < _plan.PlanTimes) ? RoundSuccess("继续使用电量", null, TimeSpan.FromMilliseconds(500L)) : RoundSuccess();
	}

	[NodeFrom("确认", Status = "可以使用家政券")]
	[OperationNode("关闭弹窗")]
	private OperationRoundResult CloseCouponWindow()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "家政券", "绳网信用");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		base.ZContext.Controller?.Click(new Point(1500, 200));
		return RoundSuccess("可以使用家政券", null, TimeSpan.FromMilliseconds(500L));
	}
}
