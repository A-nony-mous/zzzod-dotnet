using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 默认恶名狩猎 Operation 流程。
/// </summary>
public sealed class OperationNotoriousHuntAppFlow : INotoriousHuntAppFlow
{
	private NotoriousHuntOperation? _operation;

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, NotoriousHuntConfig config, NotoriousHuntRunRecord runRecord, CancellationToken cancellationToken)
	{
		_operation = new NotoriousHuntOperation(context, config, runRecord);
		return _operation.ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public void Pause(ZContext context)
	{
		context.AutoBattleContext.StopAutoBattle();
	}

	/// <inheritdoc />
	public void Resume(ZContext context)
	{
		_operation?.ResumeAutoBattle();
	}

	/// <inheritdoc />
	public void Stop(ZContext context)
	{
		context.AutoBattleContext.StopAutoBattle();
	}
}
