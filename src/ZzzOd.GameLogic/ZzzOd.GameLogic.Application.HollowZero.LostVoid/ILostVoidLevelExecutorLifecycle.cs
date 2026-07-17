using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

public interface ILostVoidLevelExecutorLifecycle
{
	/// <summary>暂停当前层。</summary>
	void Pause(ZContext context);

	/// <summary>恢复当前层。</summary>
	void Resume(ZContext context);

	/// <summary>停止当前层。</summary>
	void Stop(ZContext context);
}
