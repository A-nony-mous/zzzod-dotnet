using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地应用的运行期生命周期。
/// </summary>
public interface ILostVoidAppLifecycle
{
	/// <summary>暂停当前迷失之地流程。</summary>
	void Pause(ZContext context);

	/// <summary>恢复当前迷失之地流程。</summary>
	void Resume(ZContext context);

	/// <summary>停止当前迷失之地流程。</summary>
	void Stop(ZContext context);
}
