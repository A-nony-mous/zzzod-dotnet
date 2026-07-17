using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 自动战斗应用。
/// </summary>
public sealed class AutoBattleApp : ZApplication
{
	private readonly IAutoBattleAppFlow _flow;

	/// <summary>
	/// 初始化自动战斗应用。
	/// </summary>
	public AutoBattleApp(ZContext context, IAutoBattleAppFlow? flow = null)
		: base(context, "auto_battle", null, "自动战斗")
	{
		_flow = flow ?? new OperationAutoBattleAppFlow();
	}

	/// <inheritdoc />
	protected override Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		return _flow.RunAsync(base.Context, cancellationToken);
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
