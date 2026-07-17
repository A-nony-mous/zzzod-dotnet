using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.IntelBoard;

/// <summary>
/// 情报板应用流程。
/// </summary>
public interface IIntelBoardAppFlow
{
	/// <summary>
	/// 运行情报板流程。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, IntelBoardConfig config, IntelBoardRunRecord runRecord, CancellationToken cancellationToken);

	/// <summary>暂停。</summary>
	void Pause(ZContext context);

	/// <summary>恢复。</summary>
	void Resume(ZContext context);

	/// <summary>停止。</summary>
	void Stop(ZContext context);
}
