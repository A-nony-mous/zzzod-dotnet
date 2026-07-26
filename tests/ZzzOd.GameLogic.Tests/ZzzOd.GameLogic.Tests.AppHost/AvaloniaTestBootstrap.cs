using System.Runtime.CompilerServices;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 测试程序集加载时先在专用 UI 线程完成 Avalonia 初始化，
/// 避免并行测试线程抢先访问调度器导致 UI 线程归属冲突。
/// </summary>
internal static class AvaloniaTestBootstrap
{
	/// <summary>
	/// 在任何测试执行前初始化共享 Avalonia UI 线程。
	/// </summary>
	[ModuleInitializer]
	internal static void Initialize()
	{
		try
		{
			GuiParityAndFacadeTests.RunOnUiThread(static () =>
			{
			});
		}
		catch
		{
			// 初始化失败时由依赖 UI 线程的测试自行报告具体异常。
		}
	}
}
