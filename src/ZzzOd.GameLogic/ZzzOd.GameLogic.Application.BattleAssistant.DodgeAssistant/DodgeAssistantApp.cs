using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;

/// <summary>
/// 闪避助手应用。
/// </summary>
public sealed class DodgeAssistantApp : ZApplication
{
	private readonly IDodgeAssistantFlow _flow;

	/// <summary>
	/// 初始化闪避助手应用。
	/// </summary>
	public DodgeAssistantApp(ZContext context, IDodgeAssistantFlow? flow = null)
		: base(context, "dodge_assistant", null, "闪避助手")
	{
		_flow = flow ?? new OperationDodgeAssistantFlow();
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
