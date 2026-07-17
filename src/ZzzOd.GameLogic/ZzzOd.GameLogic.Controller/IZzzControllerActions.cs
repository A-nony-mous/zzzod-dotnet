using System;

namespace ZzzOd.GameLogic.Controller;

/// <summary>
/// ZZZ 业务层需要的可记录控制器动作。
/// </summary>
public interface IZzzControllerActions
{
	/// <summary>向前移动。</summary>
	void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false);

	/// <summary>向后移动。</summary>
	void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false);

	/// <summary>向左移动。</summary>
	void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false);

	/// <summary>向右移动。</summary>
	void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false);

	/// <summary>交互。</summary>
	void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false);

	/// <summary>按横向距离转向。</summary>
	void TurnByDistance(float distance);
}
