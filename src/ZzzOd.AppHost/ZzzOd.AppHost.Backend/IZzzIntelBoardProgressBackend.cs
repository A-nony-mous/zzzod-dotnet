namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 情报板真实运行记录命令。
/// </summary>
public interface IZzzIntelBoardProgressBackend
{
	/// <summary>
	/// 重置指定实例的真实情报板周期进度。
	/// </summary>
	ZzzBackendResult<bool> ResetIntelBoardProgress(int? instanceIndex = null);
}
