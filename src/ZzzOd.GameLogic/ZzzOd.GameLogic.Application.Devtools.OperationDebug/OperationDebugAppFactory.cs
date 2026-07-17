using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Devtools.OperationDebug;

/// <summary>
/// 指令调试应用 factory。
/// </summary>
public sealed class OperationDebugAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public OperationDebugAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("operation_debug", "指令调试", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new OperationDebugApp(base.Context, OperationDebugConfig.Load(base.Context.Environment, instanceIndex, groupId), (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return OperationDebugConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}
}
