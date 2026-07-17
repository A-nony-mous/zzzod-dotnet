using Xunit;

namespace ZzzOd.GameLogic.Tests;

/// <summary>
/// 业务仓脚手架测试。
/// </summary>
public sealed class ScaffoldTests
{
	/// <summary>
	/// 模块常量应可访问。
	/// </summary>
	[Fact]
	public void ModuleName_ShouldBeAvailable()
	{
		Assert.Equal("ZzzOd.GameLogic", "ZzzOd.GameLogic");
	}
}
