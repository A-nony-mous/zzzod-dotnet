using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;

/// <summary>
/// 闪避助手流程。
/// </summary>
public interface IDodgeAssistantFlow
{
	Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken);

	void Pause(ZContext context);

	void Resume(ZContext context);

	void Stop(ZContext context);
}
