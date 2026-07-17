using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店应用。
/// </summary>
public sealed class CoffeeApp : ZApplication
{
	private readonly CoffeeConfig _config;

	private readonly ChargePlanConfig _chargePlanConfig;

	private readonly ICoffeeAppFlow _flow;

	/// <summary>
	/// 初始化咖啡店应用。
	/// </summary>
	public CoffeeApp(ZContext context, CoffeeConfig? config = null, ChargePlanConfig? chargePlanConfig = null, ZApplicationRunRecord? runRecord = null, ICoffeeAppFlow? flow = null)
		: base(context, "coffee", runRecord, "咖啡店")
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		_config = config ?? CoffeeConfig.Load(context.Environment, valueOrDefault, "one_dragon");
		_chargePlanConfig = chargePlanConfig ?? ChargePlanConfig.Load(context.Environment, valueOrDefault, "one_dragon");
		_flow = flow ?? new OperationCoffeeAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _chargePlanConfig, cancellationToken);
	}
}
