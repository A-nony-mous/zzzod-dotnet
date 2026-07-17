namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 健康检查结果。
/// </summary>
/// <param name="Mode">宿主模式。</param>
/// <param name="Version">程序集版本。</param>
/// <param name="RunRoot">运行根目录。</param>
/// <param name="ApiEnabled">API 是否启用。</param>
/// <param name="ContextReady">业务上下文是否可运行。</param>
/// <param name="ActiveInstanceIndex">当前实例编号。</param>
public sealed record ZzzHealthDto(ZzzHostMode Mode, string Version, string RunRoot, bool ApiEnabled, bool ContextReady, int ActiveInstanceIndex);
