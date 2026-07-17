namespace ZzzOd.AppHost;

/// <summary>
/// 运行根目录解析结果。
/// </summary>
/// <param name="RunRoot">运行根目录。</param>
/// <param name="Source">路径来源。</param>
public sealed record ZzzRunRootResolution(ZzzRunRoot RunRoot, ZzzRunRootSource Source);
