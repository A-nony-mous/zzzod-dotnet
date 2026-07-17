using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码应用。
/// </summary>
public sealed class RedemptionCodeApp : ZApplication
{
	private readonly RedemptionCodeConfig _config;

	private readonly RedemptionCodeRunRecord _runRecord;

	private readonly IRedemptionCodeAppFlow _flow;

	/// <summary>
	/// 初始化兑换码应用。
	/// </summary>
	public RedemptionCodeApp(ZContext context, RedemptionCodeConfig? config = null, RedemptionCodeRunRecord? runRecord = null, IRedemptionCodeAppFlow? flow = null)
		: base(context, "redemption_code", runRecord ?? LoadRunRecord(context, config), "兑换码")
	{
		_config = config ?? RedemptionCodeConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = (RedemptionCodeRunRecord)base.RunRecord;
		_flow = flow ?? new OperationRedemptionCodeAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
	}

	private static RedemptionCodeRunRecord LoadRunRecord(ZContext context, RedemptionCodeConfig? config)
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		RedemptionCodeConfig config2 = config ?? RedemptionCodeConfig.Load(context.Environment, valueOrDefault, "one_dragon");
		return RedemptionCodeRunRecord.Load(context.Environment, valueOrDefault, context.GameAccountConfig.GameRefreshHourOffset, null, config2);
	}
}
