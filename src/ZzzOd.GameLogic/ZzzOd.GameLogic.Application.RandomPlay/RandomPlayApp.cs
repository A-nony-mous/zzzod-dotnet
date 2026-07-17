using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业应用。
/// </summary>
public sealed class RandomPlayApp : ZApplication
{
	private readonly RandomPlayConfig _config;

	private readonly RandomPlayRunRecord _runRecord;

	private readonly IRandomPlayAppFlow _flow;

	/// <summary>
	/// 初始化录像店营业应用。
	/// </summary>
	public RandomPlayApp(ZContext context, RandomPlayConfig? config = null, RandomPlayRunRecord? runRecord = null, IRandomPlayAppFlow? flow = null)
		: base(context, "random_play", runRecord, "录像店营业")
	{
		_config = config ?? RandomPlayConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? new RandomPlayRunRecord(context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationRandomPlayAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
	}
}
