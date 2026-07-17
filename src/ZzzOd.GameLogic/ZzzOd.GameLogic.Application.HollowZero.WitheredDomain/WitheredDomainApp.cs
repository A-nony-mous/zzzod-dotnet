using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都应用。
/// </summary>
public sealed class WitheredDomainApp : ZApplication
{
	public const string StatusInHollow = "在空洞内";

	public const string StatusNoReward = "无奖励可领取";

	public const string StatusTimesFinished = "已完成基本次数";

	public const string StatusNoEvalPoint = "已完成刷取业绩";

	private readonly WitheredDomainConfig _config;

	private readonly WitheredDomainRunRecord _runRecord;

	private readonly IWitheredDomainAppFlow _flow;

	private bool _resumeAutoBattleAfterPause;

	/// <summary>
	/// 初始化枯萎之都应用。
	/// </summary>
	public WitheredDomainApp(ZContext context, WitheredDomainConfig? config = null, WitheredDomainRunRecord? runRecord = null, IWitheredDomainAppFlow? flow = null)
		: base(context, "withered_domain", runRecord, "枯萎之都")
	{
		_config = config ?? WitheredDomainConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "default");
		_runRecord = runRecord ?? WitheredDomainRunRecord.Load(context.Environment, _config, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationWitheredDomainAppFlow();
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_resumeAutoBattleAfterPause = false;
			base.Context.AutoBattleContext.StopAutoBattle();
		}
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		_resumeAutoBattleAfterPause = base.Context.AutoBattleContext.IsRuntimeRunning;
		if (_resumeAutoBattleAfterPause)
		{
			base.Context.AutoBattleContext.StopAutoBattle();
		}
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task OnResumeAsync(CancellationToken cancellationToken)
	{
		await base.OnResumeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (_resumeAutoBattleAfterPause)
		{
			_resumeAutoBattleAfterPause = false;
			base.Context.AutoBattleContext.ResumeAutoBattle();
		}
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_resumeAutoBattleAfterPause = false;
		base.Context.AutoBattleContext.StopAutoBattle();
		return Task.CompletedTask;
	}
}
