using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;

/// <summary>
/// 闪避截图应用 factory。
/// </summary>
public sealed class ScreenshotHelperAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public ScreenshotHelperAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("screenshot_helper", "闪避截图", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new ScreenshotHelperApp(base.Context, ScreenshotHelperConfig.Load(base.Context.Environment, instanceIndex, groupId), (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return ScreenshotHelperConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}
}
