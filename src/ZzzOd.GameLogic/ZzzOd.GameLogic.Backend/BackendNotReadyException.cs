using System;

namespace ZzzOd.GameLogic.Backend;

/// <summary>
/// 后端所依赖的上下文尚未就绪。
/// </summary>
public sealed class BackendNotReadyException : InvalidOperationException
{
	/// <summary>
	/// 初始化异常。
	/// </summary>
	/// <param name="message">异常消息。</param>
	public BackendNotReadyException(string message)
		: base(message)
	{
	}
}
