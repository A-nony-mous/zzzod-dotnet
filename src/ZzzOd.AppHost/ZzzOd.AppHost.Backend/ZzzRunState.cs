namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 对 GUI、REST 和 WebSocket 暴露的统一运行状态。
/// </summary>
public enum ZzzRunState
{
	/// <summary>
	/// 空闲。
	/// </summary>
	Idle,
	/// <summary>
	/// 正在启动。
	/// </summary>
	Starting,
	/// <summary>
	/// 正在运行。
	/// </summary>
	Running,
	/// <summary>
	/// 已暂停。
	/// </summary>
	Paused,
	/// <summary>
	/// 正在停止。
	/// </summary>
	Stopping,
	/// <summary>
	/// 上次运行成功。
	/// </summary>
	Succeeded,
	/// <summary>
	/// 上次运行失败。
	/// </summary>
	Failed,
	/// <summary>
	/// 用户取消。
	/// </summary>
	Cancelled
}
