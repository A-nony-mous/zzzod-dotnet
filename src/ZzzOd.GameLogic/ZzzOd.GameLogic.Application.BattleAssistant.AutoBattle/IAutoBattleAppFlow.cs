using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;

/// <summary>
/// 自动战斗应用流程。
/// </summary>
public interface IAutoBattleAppFlow
{
	/// <summary>运行自动战斗流程。</summary>
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);

	/// <summary>暂停。</summary>
	void Pause(ZContext context);

	/// <summary>恢复。</summary>
	void Resume(ZContext context);

	/// <summary>停止。</summary>
	void Stop(ZContext context);
}
