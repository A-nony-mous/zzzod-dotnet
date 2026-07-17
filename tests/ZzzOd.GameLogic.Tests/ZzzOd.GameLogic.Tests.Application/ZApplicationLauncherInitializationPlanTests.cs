using System.Linq;
using Xunit;
using ZzzOd.GameLogic.Application;

namespace ZzzOd.GameLogic.Tests.Application;

/// <summary>
/// 测试 ZZZ 应用启动器初始化计划。
/// </summary>
public sealed class ZApplicationLauncherInitializationPlanTests
{
	[Fact]
	public void Steps_ShouldMatchPythonLauncherAndContextOrder()
	{
		ZApplicationLauncherInitializationStage[] actual = ZApplicationLauncherInitializationPlan.Steps.Select((ZApplicationLauncherInitializationStep step) => step.Stage).ToArray();
		Assert.Equal<ZApplicationLauncherInitializationStage[]>(new ZApplicationLauncherInitializationStage[11]
		{
			ZApplicationLauncherInitializationStage.CreateContext,
			ZApplicationLauncherInitializationStage.RegisterBuiltInApplications,
			ZApplicationLauncherInitializationStage.SetDefaultApplicationGroup,
			ZApplicationLauncherInitializationStage.InitializeOcrProfile,
			ZApplicationLauncherInitializationStage.ReloadScreenDefinitions,
			ZApplicationLauncherInitializationStage.ReloadInstanceConfig,
			ZApplicationLauncherInitializationStage.InitializeController,
			ZApplicationLauncherInitializationStage.InitializeForApplication,
			ZApplicationLauncherInitializationStage.CheckRunRecords,
			ZApplicationLauncherInitializationStage.InitializePushNotifications,
			ZApplicationLauncherInitializationStage.InitializeTelemetry
		}, actual);
	}

	[Fact]
	public void Steps_ShouldNamePythonOrCSharpSourceForEveryStage()
	{
		Assert.All(ZApplicationLauncherInitializationPlan.Steps, delegate(ZApplicationLauncherInitializationStep step)
		{
			Assert.False(string.IsNullOrWhiteSpace(step.Source));
			Assert.False(string.IsNullOrWhiteSpace(step.Description));
		});
	}
}
