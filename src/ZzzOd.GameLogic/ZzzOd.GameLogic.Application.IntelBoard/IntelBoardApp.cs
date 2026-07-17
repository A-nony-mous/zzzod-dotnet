using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板应用。
/// </summary>
public sealed class IntelBoardApp : ZApplication
{
	private sealed record IntelBoardAppDependencies(IntelBoardConfig Config, IntelBoardRunRecord RunRecord);

	private readonly IntelBoardConfig _config;

	private readonly IntelBoardRunRecord _runRecord;

	private readonly IIntelBoardAppFlow _flow;

	/// <summary>
	/// 初始化情报板应用。
	/// </summary>
	public IntelBoardApp(ZContext context, IntelBoardConfig? config = null, IntelBoardRunRecord? runRecord = null, IIntelBoardAppFlow? flow = null, IIntelBoardOperationServices? operationServices = null)
		: this(context, ResolveDependencies(context, config, runRecord), flow, operationServices)
	{
	}

	private IntelBoardApp(ZContext context, IntelBoardAppDependencies dependencies, IIntelBoardAppFlow? flow, IIntelBoardOperationServices? operationServices)
		: base(context, "intel_board", dependencies.RunRecord, "情报板")
	{
		_config = dependencies.Config;
		_runRecord = dependencies.RunRecord;
		_flow = flow ?? new OperationIntelBoardAppFlow(operationServices);
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		base.Context.ScreenContext.EnterScope("intel_board");
		try
		{
			return await _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			base.Context.ScreenContext.ExitScope();
		}
	}

	/// <inheritdoc />
	public override Task OnPauseAsync(CancellationToken cancellationToken)
	{
		_flow.Pause(base.Context);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override Task OnResumeAsync(CancellationToken cancellationToken)
	{
		_flow.Resume(base.Context);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_flow.Stop(base.Context);
		return Task.CompletedTask;
	}

	private static IntelBoardAppDependencies ResolveDependencies(ZContext context, IntelBoardConfig? config, IntelBoardRunRecord? runRecord)
	{
		int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
		IntelBoardConfig config2 = config ?? IntelBoardConfig.Load(context.Environment, valueOrDefault, "one_dragon");
		IntelBoardRunRecord runRecord2 = runRecord ?? IntelBoardRunRecord.Load(context.Environment, valueOrDefault, config2, context.GameAccountConfig.GameRefreshHourOffset);
		return new IntelBoardAppDependencies(config2, runRecord2);
	}
}
