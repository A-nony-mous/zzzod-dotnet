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
		: base(context, AutoBattleAppConstants.AppId, null, AutoBattleAppConstants.AppName)
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
		// 与 OnPauseAsync 对称：恢复自动战斗不应触发基类的窗口激活等副作用，因此不调用基类实现。
		_flow.Resume(base.Context);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public override async Task OnStopAsync(CancellationToken cancellationToken)
	{
		try
		{
			_flow.Stop(base.Context);
		}
		finally
		{
			await base.OnStopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
