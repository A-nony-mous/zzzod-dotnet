using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店应用 factory。
/// </summary>
public sealed class CoffeeAppFactory : ZApplicationFactory
{
	/// <summary>
	/// 初始化 factory。
	/// </summary>
	public CoffeeAppFactory(ZContext context)
		: base(context, new ZApplicationFactoryMetadata("coffee", "咖啡店", "one_dragon", NeedNotify: true))
	{
	}

	/// <inheritdoc />
	public override IApplication CreateApplication(int instanceIndex, string groupId)
	{
		CoffeeConfig config = (CoffeeConfig)GetConfig(instanceIndex, groupId);
		ChargePlanConfig chargePlanConfig = ChargePlanConfig.Load(base.Context.Environment, instanceIndex, "one_dragon");
		return new CoffeeApp(base.Context, config, chargePlanConfig, (ZApplicationRunRecord)GetRunRecord(instanceIndex));
	}

	/// <inheritdoc />
	public override IApplicationConfig GetConfig(int instanceIndex, string groupId)
	{
		return CoffeeConfig.Load(base.Context.Environment, instanceIndex, groupId);
	}

	/// <inheritdoc />
	public override IApplicationRunRecord GetRunRecord(int instanceIndex)
	{
		return CoffeeRunRecord.Load(base.Context.Environment, instanceIndex, base.Context.GameAccountConfig.GameRefreshHourOffset);
	}
}
