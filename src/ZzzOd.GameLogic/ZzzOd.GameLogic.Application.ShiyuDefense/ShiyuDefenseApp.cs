using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用。
/// </summary>
public sealed class ShiyuDefenseApp : ZApplication
{
	private sealed record ShiyuDefenseDependencies(ShiyuDefenseConfig Config, ShiyuDefenseRunRecord RunRecord);

	private readonly ShiyuDefenseConfig _config;

	private readonly ShiyuDefenseRunRecord _runRecord;

	private readonly IShiyuDefenseAppFlow _flow;

	private bool _resumeAutoBattle;

	/// <summary>
	/// 初始化式舆防卫战应用。
	/// </summary>
	public ShiyuDefenseApp(ZContext context, ShiyuDefenseConfig? config = null, ShiyuDefenseRunRecord? runRecord = null, IShiyuDefenseAppFlow? flow = null)
		: this(context, ResolveDependencies(context, config, runRecord), flow)
	{
	}

	private ShiyuDefenseApp(ZContext context, ShiyuDefenseDependencies dependencies, IShiyuDefenseAppFlow? flow)
		: base(context, "shiyu_defense", dependencies.RunRecord, "式舆防卫战")
	{
		_config = dependencies.Config;
		_runRecord = dependencies.RunRecord;
		_flow = flow ?? new OperationShiyuDefenseAppFlow();
	}

	private static ShiyuDefenseDependencies ResolveDependencies(ZContext context, ShiyuDefenseConfig? config, ShiyuDefenseRunRecord? runRecord)
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		ShiyuDefenseConfig config2 = config ?? ShiyuDefenseConfig.Load(context.Environment, valueOrDefault, "one_dragon");
		ShiyuDefenseRunRecord runRecord2 = runRecord ?? ShiyuDefenseRunRecord.Load(context.Environment, valueOrDefault, config2, context.GameAccountConfig.GameRefreshHourOffset);
		return new ShiyuDefenseDependencies(config2, runRecord2);
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		_resumeAutoBattle = base.Context.AutoBattleContext.AutoOp?.IsRunning ?? false;
		base.Context.AutoBattleContext.StopAutoBattle();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override Task OnResumeAsync(CancellationToken cancellationToken)
	{
		if (_resumeAutoBattle)
		{
			base.Context.AutoBattleContext.ResumeAutoBattle();
		}
		_resumeAutoBattle = false;
		return base.OnResumeAsync(cancellationToken);
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_resumeAutoBattle = false;
		base.Context.AutoBattleContext.StopAutoBattle();
		return Task.CompletedTask;
	}
}
