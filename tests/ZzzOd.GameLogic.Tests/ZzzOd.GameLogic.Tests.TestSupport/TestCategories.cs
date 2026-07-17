namespace ZzzOd.GameLogic.Tests.TestSupport;

/// <summary>
/// xUnit 测试分类约定。
/// </summary>
public static class TestCategories
{
	/// <summary>xUnit trait 分类键。</summary>
	public const string Category = "Category";

	/// <summary>需要真实游戏、设备、窗口或人工状态的端到端测试。</summary>
	public const string E2E = "E2E";

	/// <summary>默认测试命令使用的非 E2E 过滤表达式。</summary>
	public const string DefaultNonE2EFilter = "Category!=E2E";
}
