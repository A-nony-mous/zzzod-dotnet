using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 默认自动战斗 Operation 流程。
/// </summary>
public sealed class OperationAutoBattleAppFlow : IAutoBattleAppFlow
{
	private AutoBattleAppOperation? _operation;

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
	{
		_operation = new AutoBattleAppOperation(context);
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
