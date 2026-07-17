using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.NotoriousHunt;

/// <summary>
/// 恶名狩猎应用流程。
/// </summary>
public interface INotoriousHuntAppFlow
{
	/// <summary>
	/// 运行恶名狩猎计划。
	/// </summary>
	Task<OperationResult> RunAsync(ZContext context, NotoriousHuntConfig config, NotoriousHuntRunRecord runRecord, CancellationToken cancellationToken);

	/// <summary>暂停。</summary>
	void Pause(ZContext context);

	/// <summary>恢复。</summary>
	void Resume(ZContext context);

	/// <summary>停止。</summary>
	void Stop(ZContext context);
}
