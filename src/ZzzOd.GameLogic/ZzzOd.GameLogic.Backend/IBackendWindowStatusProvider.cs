namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 为 backend 提供窗口状态投影。
/// </summary>
public interface IBackendWindowStatusProvider
{
	/// <summary>
	/// 获取当前窗口状态。
	/// </summary>
	/// <returns>窗口状态。</returns>
	WindowStatus GetWindowStatus();
}
