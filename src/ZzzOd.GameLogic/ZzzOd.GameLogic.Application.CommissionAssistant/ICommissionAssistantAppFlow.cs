using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 委托助手应用流程。
/// </summary>
public interface ICommissionAssistantAppFlow
{
	/// <summary>运行应用。</summary>
	Task<OperationResult> RunAsync(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, CancellationToken cancellationToken);

	/// <summary>暂停。</summary>
	void Pause(ZContext context, CommissionAssistantRuntimeState state);

	/// <summary>恢复。</summary>
	void Resume(ZContext context, CommissionAssistantRuntimeState state);

	/// <summary>停止。</summary>
	void Stop(ZContext context, CommissionAssistantRuntimeState state);
}
