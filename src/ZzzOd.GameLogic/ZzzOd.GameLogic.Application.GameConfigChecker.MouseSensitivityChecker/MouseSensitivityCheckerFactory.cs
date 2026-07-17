using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;

/// <summary>
/// 鼠标灵敏度检测 factory。
/// </summary>
public sealed class MouseSensitivityCheckerFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public MouseSensitivityCheckerFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("mouse_sensitivity_checker", "鼠标灵敏度检测", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new MouseSensitivityCheckerApp(base.Context, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}
}
