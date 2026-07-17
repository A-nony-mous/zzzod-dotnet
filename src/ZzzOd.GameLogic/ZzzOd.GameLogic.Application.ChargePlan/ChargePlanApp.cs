using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ChargePlan;

/// <summary>
/// 电量计划应用。
/// </summary>
public sealed class ChargePlanApp : ZApplication
{
	private readonly ChargePlanConfig _config;

	private readonly ChargePlanRunRecord _runRecord;

	private readonly IChargePlanAppFlow _flow;

	/// <summary>
	/// 初始化电量计划应用。
	/// </summary>
	public ChargePlanApp(ZContext context, ChargePlanConfig? config = null, ChargePlanRunRecord? runRecord = null, IChargePlanAppFlow? flow = null)
		: base(context, "charge_plan", runRecord, "体力刷本")
	{
		_config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? ChargePlanRunRecord.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationChargePlanAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
	}
}
