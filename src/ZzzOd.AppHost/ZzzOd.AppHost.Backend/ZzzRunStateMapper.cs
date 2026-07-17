using OneDragon.Core.Abstractions.Operations;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 运行状态映射器。
/// </summary>
public static class ZzzRunStateMapper
{
	/// <summary>
	/// 将业务运行上下文状态映射为对外运行状态。
	/// </summary>
	/// <param name="contextState">业务运行上下文状态。</param>
	/// <param name="terminalState">当前终态。</param>
	/// <returns>对外运行状态。</returns>
	public static ZzzRunState Map(ApplicationRunContextState? contextState, ZzzRunState terminalState)
	{
		if (1 == 0)
		{
		}
		ZzzRunState result = contextState switch
		{
			ApplicationRunContextState.Running => ZzzRunState.Running, 
			ApplicationRunContextState.Pause => ZzzRunState.Paused, 
			_ => terminalState, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
