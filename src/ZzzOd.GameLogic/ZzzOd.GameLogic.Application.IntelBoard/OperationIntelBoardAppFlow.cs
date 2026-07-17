using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 默认情报。Operation 流程。
/// </summary>
public sealed class OperationIntelBoardAppFlow : IIntelBoardAppFlow
{
	private readonly IIntelBoardOperationServices? _services;

	private IntelBoardOperation? _operation;

	/// <summary>
	/// 初始化默认情报板流程。
	/// </summary>
	public OperationIntelBoardAppFlow(IIntelBoardOperationServices? services = null)
	{
		_services = services;
	}

	/// <inheritdoc />
	public Task<OperationResult> RunAsync(ZContext context, IntelBoardConfig config, IntelBoardRunRecord runRecord, CancellationToken cancellationToken)
	{
		_operation = new IntelBoardOperation(context, config, runRecord, _services);
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
