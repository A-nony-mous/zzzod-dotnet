namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 业务门面调用结果。
/// </summary>
/// <typeparam name="T">返回数据类型。</typeparam>
public sealed record ZzzBackendResult<T>
{
	public bool Success { get; }

	public T? Value { get; }

	public ZzzBackendErrorCode? ErrorCode { get; }

	public string? Error { get; }

	private ZzzBackendResult(bool success, T? value, ZzzBackendErrorCode? errorCode, string? error)
	{
		Success = success;
		Value = value;
		ErrorCode = errorCode;
		Error = error;
	}

	/// <summary>
	/// 创建成功结果。
	/// </summary>
	/// <param name="value">成功数据。</param>
	/// <returns>调用结果。</returns>
	public static ZzzBackendResult<T> Ok(T value)
	{
		return new ZzzBackendResult<T>(success: true, value, null, null);
	}

	/// <summary>
	/// 创建失败结果。
	/// </summary>
	/// <param name="errorCode">错误码。</param>
	/// <param name="error">错误文本。</param>
	/// <returns>调用结果。</returns>
	public static ZzzBackendResult<T> Fail(ZzzBackendErrorCode errorCode, string error)
	{
		return new ZzzBackendResult<T>(success: false, default(T), errorCode, error);
	}
}
