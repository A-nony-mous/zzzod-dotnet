using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.LifeOnLine;

/// <summary>
/// 生命热线应用。
/// </summary>
public sealed class LifeOnLineApp : ZApplication
{
	private readonly LifeOnLineConfig _config;

	private readonly LifeOnLineRunRecord _runRecord;

	private readonly ILifeOnLineAppFlow _flow;

	/// <summary>
	/// 初始化生命热线应用。
	/// </summary>
	public LifeOnLineApp(ZContext context, LifeOnLineConfig? config = null, LifeOnLineRunRecord? runRecord = null, ILifeOnLineAppFlow? flow = null)
		: base(context, "life_on_line", runRecord ?? LoadRunRecord(context, config), "真·拿命验收")
	{
		_config = config ?? LifeOnLineConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = (LifeOnLineRunRecord)base.RunRecord;
		_flow = flow ?? new OperationLifeOnLineAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("life_on_line");
		try
		{
			return await _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}

	private static LifeOnLineRunRecord LoadRunRecord(ZContext context, LifeOnLineConfig? config)
	{
		LifeOnLineConfig config2 = config ?? LifeOnLineConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		return LifeOnLineRunRecord.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), config2, context.GameAccountConfig.GameRefreshHourOffset);
	}
}
