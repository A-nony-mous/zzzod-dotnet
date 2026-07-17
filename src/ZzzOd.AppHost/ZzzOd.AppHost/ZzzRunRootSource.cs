namespace ZzzOd.AppHost;

/// <summary>
/// 运行根目录来源。
/// </summary>
public enum ZzzRunRootSource
{
	/// <summary>
	/// 启动参数 <c>--run-root</c>。
	/// </summary>
	CommandLine,
	/// <summary>
	/// 环境变量 <c>ZZZOD_RUN_ROOT</c>。
	/// </summary>
	Environment,
	/// <summary>
	/// 应用程序所在目录。
	/// </summary>
	ApplicationBaseDirectory
}
