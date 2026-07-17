using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地 runner 的当前层生命周期。
/// </summary>
public interface ILostVoidRunnerLifecycle
{
	/// <summary>暂停当前运行层。</summary>
	void Pause(ZContext context);

	/// <summary>恢复当前运行层。</summary>
	void Resume(ZContext context);

	/// <summary>停止当前运行层。</summary>
	void Stop(ZContext context);
}
