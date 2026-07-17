using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 默认委托助手 Operation 流程。
/// </summary>
public sealed class OperationCommissionAssistantAppFlow : ICommissionAssistantAppFlow
{
	private CommissionAssistantOperation? _operation;

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, CancellationToken cancellationToken)
	{
		_operation = new CommissionAssistantOperation(context, config, state);
		return _operation.ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public void Pause(ZContext context, CommissionAssistantRuntimeState state)
	{
		if (state.RunMode != 0)
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}

	/// <inheritdoc />
	public void Resume(ZContext context, CommissionAssistantRuntimeState state)
	{
		if (state.RunMode != 0)
		{
			context.AutoBattleContext.ResumeAutoBattle();
		}
	}

	/// <inheritdoc />
	public void Stop(ZContext context, CommissionAssistantRuntimeState state)
	{
		context.AutoBattleContext.StopAutoBattle();
	}
}
