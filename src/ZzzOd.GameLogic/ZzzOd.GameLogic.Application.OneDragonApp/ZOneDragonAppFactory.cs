using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// 一条龙应用 factory。
/// </summary>
public sealed class ZOneDragonAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化一条龙应用 factory。
	/// </summary>
	public ZOneDragonAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("one_dragon", "一条龙", "one_dragon"))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new ZOneDragonApp(base.Context, instanceIndex, groupId, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}
}
