using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;

/// <summary>
/// 默认闪避助手 Operation 流程。
/// </summary>
public sealed class OperationDodgeAssistantFlow : IDodgeAssistantFlow
{
	private DodgeAssistantOperation? _operation;

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		_operation = new DodgeAssistantOperation(context);
		return _operation.ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public void Pause(ZContext context)
	{
		_operation?.PauseAutoBattle();
	}

	/// <inheritdoc />
	public void Resume(ZContext context)
	{
		_operation?.ResumeAutoBattle();
	}

	/// <inheritdoc />
	public void Stop(ZContext context)
	{
		if (_operation != null)
		{
			_operation.StopAutoBattle();
		}
		else
		{
			context.AutoBattleContext.StopAutoBattle();
		}
	}
}
