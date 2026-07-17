namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 业务门面错误码。
/// </summary>
public enum ZzzBackendErrorCode
{
	/// <summary>
	/// 未分类错误。
	/// </summary>
	Unknown,
	/// <summary>
	/// 运行时尚未就绪。
	/// </summary>
	NotReady,
	/// <summary>
	/// 当前状态与请求冲突。
	/// </summary>
	Conflict,
	/// <summary>
	/// 目标资源不存在。
	/// </summary>
	NotFound,
	/// <summary>
	/// 当前请求未认证。
	/// </summary>
	Unauthorized,
	/// <summary>
	/// 请求数据未通过校验。
	/// </summary>
	Validation
}
