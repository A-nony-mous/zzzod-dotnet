using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地路线 runner。
/// </summary>
public interface IWorldPatrolRouteRunner
{
	/// <summary>当前是否正在执行路线。</summary>
	bool IsRunning { get; }

	/// <summary>运行单条路线。</summary>
	Task<OperationResult> RunRouteAsync(ZContext context, WorldPatrolConfig config, WorldPatrolRoute route, bool isRestarted, CancellationToken cancellationToken);

	/// <summary>暂停路线。</summary>
	void Pause();

	/// <summary>恢复路线。</summary>
	void Resume();

	/// <summary>停止路线。</summary>
	void Stop();
}
