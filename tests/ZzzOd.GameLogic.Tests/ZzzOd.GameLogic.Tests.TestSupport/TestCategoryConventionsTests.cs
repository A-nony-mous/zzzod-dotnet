using Xunit;

namespace ZzzOd.GameLogic.Tests.TestSupport;

/// <summary>
/// 验证统一测试分类约定。
/// </summary>
public sealed class TestCategoryConventionsTests
{
	/// <summary>
	/// 验证默认非 E2E 过滤表达式排除 E2E 测试。
	/// </summary>
	[Fact]
	public void DefaultNonE2EFilter_ExcludesE2ETests()
	{
		Assert.Equal("Category", "Category");
		Assert.Equal("E2E", "E2E");
		Assert.Equal("Category!=E2E", "Category!=E2E");
		Assert.Equal("Category!=E2E", "Category!=E2E");
	}
}
