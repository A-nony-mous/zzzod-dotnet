using Xunit;
using ZzzOd.RealGameE2E;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// 实机 E2E 命令行解析测试。
/// </summary>
public sealed class RealGameE2ECommandLineTests
{
	/// <summary>
	/// run-root 位于子命令前时不应占用子命令或参数。
	/// </summary>
	[Fact]
	public void Parse_SeparatesRunRootBeforeCommand()
	{
		RealGameE2ECommandLine commandLine = RealGameE2ECommandLine.Parse(["--run-root", "D:\\staging", "run-app", "coffee"]);

		Assert.Equal("run-app", commandLine.Command);
		Assert.Equal(["coffee"], commandLine.CommandArguments);
		Assert.Equal(["--run-root", "D:\\staging"], commandLine.RunRootArguments);
	}

	/// <summary>
	/// run-root 位于子命令后时也不应被当作应用参数。
	/// </summary>
	[Fact]
	public void Parse_SeparatesRunRootAfterCommand()
	{
		RealGameE2ECommandLine commandLine = RealGameE2ECommandLine.Parse(["probe-click-point", "coffee", "12", "34", "--run-root=D:\\staging"]);

		Assert.Equal("probe-click-point", commandLine.Command);
		Assert.Equal(["coffee", "12", "34"], commandLine.CommandArguments);
		Assert.Equal(["--run-root=D:\\staging"], commandLine.RunRootArguments);
	}

	/// <summary>
	/// 未指定子命令时维持 preflight 默认值。
	/// </summary>
	[Fact]
	public void Parse_UsesPreflightWhenOnlyRunRootIsSpecified()
	{
		RealGameE2ECommandLine commandLine = RealGameE2ECommandLine.Parse(["--run-root", "D:\\staging"]);

		Assert.Equal("preflight", commandLine.Command);
		Assert.Empty(commandLine.CommandArguments);
	}

	/// <summary>
	/// run-root 缺少路径时应在解析阶段失败。
	/// </summary>
	[Fact]
	public void Parse_RejectsMissingRunRootValue()
	{
		Assert.Throws<ArgumentException>(() => RealGameE2ECommandLine.Parse(["--run-root"]));
	}
}
