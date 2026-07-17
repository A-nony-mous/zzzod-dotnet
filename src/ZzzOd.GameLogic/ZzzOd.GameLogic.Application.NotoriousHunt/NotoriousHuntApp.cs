using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎应用。
/// </summary>
public sealed class NotoriousHuntApp : ZApplication
{
	private readonly NotoriousHuntConfig _config;

	private readonly NotoriousHuntRunRecord _runRecord;

	private readonly INotoriousHuntAppFlow _flow;

	/// <summary>
	/// 初始化恶名狩猎应用。
	/// </summary>
	public NotoriousHuntApp(ZContext context, NotoriousHuntConfig? config = null, NotoriousHuntRunRecord? runRecord = null, INotoriousHuntAppFlow? flow = null)
		: base(context, "notorious_hunt", runRecord, "恶名狩猎")
	{
		_config = config ?? NotoriousHuntConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
		_runRecord = runRecord ?? new NotoriousHuntRunRecord(_config, context.GameAccountConfig.GameRefreshHourOffset);
		_flow = flow ?? new OperationNotoriousHuntAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, _config, _runRecord, cancellationToken);
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
		return base.OnResumeAsync(cancellationToken);
	}

	/// <inheritdoc />
	public override Task OnStopAsync(CancellationToken cancellationToken)
	{
		_flow.Stop(base.Context);
		return Task.CompletedTask;
	}
}
