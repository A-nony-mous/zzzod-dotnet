using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 吼吼饼铺应用 factory。
/// </summary>
public sealed class HouHouBakeryFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public HouHouBakeryFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("hou_hou_bakery", "吼吼饼铺", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		return new HouHouBakeryApp(base.Context, (HouHouBakeryRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return HouHouBakeryRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
